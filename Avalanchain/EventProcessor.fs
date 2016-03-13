module Avalanchain.EventProcessor

open System

open FSharp.Interop.Dynamic
open FSharp.Core.Fluent
open FSharpx.Collections
open Chessie.ErrorHandling

open SecKeys
open SecPrimitives
open RefsAndPathes
open StreamEvent
open Projection
open EventStream

type Proofer<'TState, 'TData when 'TData: equality and 'TState: equality> = 
    Hashed<EventStreamRef> -> Nonce -> Hashed<StreamState<'TState>> -> HashedEvent<'TData> -> Hashed<ExecutionProof>

let proofer 
    signer 
    dataHasher 
    (streamRef: Hashed<EventStreamRef>) 
    nonce 
    (hashedState: Hashed<StreamState<'TState>>)
    (hashedEvent: HashedEvent<'TData>) : Hashed<ExecutionProof> =
    let sd = {
        StreamRefHash = streamRef.Hash
        Nonce = nonce
        EventHash = hashedEvent.Hash
        StateHash = hashedState.Hash
    }
    let signature = signer sd//(Unsigned (sdSerializer sd)) 
    let proof = { ExecutionProof.Data = sd; Signature = signature }
    dataHasher(proof)    

let processEvent 
    (cryptoContext: CryptoContext) 
    serializers 
    (dataHasher: DataHasher<Event<'TData>>) 
    (permissionsChecker: HashedEvent<'TData> -> DataResult<unit>) 
    (proofer: Proofer<'TState, 'TData>)
    (streamDef: Hashed<EventStreamDef<'TState, 'TData>>)
    (streamFrame: EventStreamFrame<'TState, 'TData> option) 
    (hashedEvent: HashedEvent<'TData>) : EventProcessingResult<'TState, 'TData> =
    // TODO: optimize redundant serializations

    let checkIntegrity (event: HashedEvent<'TData>) = 
        let rehashed = dataHasher event.Value
        if (rehashed.Hash = event.Hash) 
        then ok event
        else fail IntegrityFailure 
    
    let checkPermissions event = 
        match permissionsChecker event with 
        | Ok (_) -> ok event
        | Bad msgs -> fail (PermissionsFailure msgs)

    let project (streamFrame: EventStreamFrame<'TState, 'TData> option) (event: HashedEvent<'TData>) =
        let (projection: 'TState -> 'TData -> ProjectionResult<'TState>) = streamDef.Value.Projection.F 
        try
            let res = 
                match streamFrame with
                    | Some sf -> projection sf.State.Value.Value event.Value.Data
                    | None -> projection streamDef.Value.InitialState event.Value.Data
            match res with
            | Ok (newState, msgs) -> 
                let ns = { 
                    Value = newState 
                    StreamRef = streamDef.Value.Ref
                    Nonce = match streamFrame with
                            | Some sf -> sf.Nonce + 1UL 
                            | None -> 0UL 
                }
                Ok (ns, msgs |> List.map ExecutionWarning)
            | Bad msgs -> fail (ProcessingFailure msgs)
        with
            | ex -> fail (ProcessingFailure([sprintf "Error projection execution : '%s'" (ex.ToString())]))

    let buildNewFrame (state: StreamState<'TState>) =
        let nonce = state.Nonce
        let merkledEvent = toMerkled serializers.event cryptoContext.Hasher (streamFrame |> Option.bind (fun sf -> Some sf.Event.Merkle)) hashedEvent.Value
        let merkledState = toMerkled serializers.state cryptoContext.Hasher (streamFrame |> Option.bind (fun sf -> Some sf.State.Merkle)) state
        let newStreamFrame = {
                                //Def = streamDef
                                Ref = streamDef.Value.Ref
                                DefHash = streamDef.Hash
                                TimeStamp = DateTimeOffset.UtcNow
                                Event = merkledEvent
                                State = merkledState 
                                Nonce = nonce
                                Proofs = [proofer streamDef.Value.Ref nonce merkledState.HashedValue hashedEvent] |> Set.ofList
                                // StreamStatus = streamFrame.StreamStatus
                            }
        ok newStreamFrame

    let run =
        checkIntegrity
        >> bind checkPermissions
        >> bind (project streamFrame)
        >> bind buildNewFrame

    run hashedEvent 
 

type EventStream<'TState, 'TData when 'TData: equality and 'TState: equality>
    (def, hasher, dataHasher, eventProcessor: StreamEventProcessor<'TState, 'TData>(*, frameSynchronizer, initFrame *), frameSerializer) =

    let mutable frames = PersistentVector.empty
    let mutable frameRefs = PersistentHashMap.empty
    let mutable eventRefs = PersistentHashMap.empty
    let mutable stateRefs = PersistentHashMap.empty
    let mutable merkledFrame = Option.None 
    let mutable subscribers = [] 

    // TODO: Add Acl checks
    interface IEventStream<'TState, 'TData> with
//        member this.Subscribe(subscriber) = subscribers <- subscribers.Add (subscriber.Ref, subscriber)
//        member this.Unsubscribe(subscriber) = subscribers <- subscribers.Remove (subscriber.Ref)
        
        member this.Def with get() = def
        member this.Ref with get() = def.Value.Ref
        member this.GetReader reader: EventStreamReader<'TState> = 
            subscribers <- reader :: subscribers
            {
                Reader = reader
                ExecutionPolicy = def.Value.ExecutionPolicy
            }
            
        member this.CurrentFrame with get() = frames.tryHead()
        member this.CurrentState with get() = frames.tryHead() |> Option.bind (fun x -> Some x.Value.State.HashedValue)
        member this.GetEvent<'TData> eventRef = 
            if eventRefs.ContainsKey(eventRef) then ok (eventRefs.[eventRef])
            else fail (DataNotExists(eventRef.ToString()))
        member this.GetState<'TState> stateRef =
            if stateRefs.ContainsKey(stateRef) then ok (stateRefs.[stateRef])
            else fail (DataNotExists(stateRef.ToString()))
        member this.GetFrame<'TState, 'TData> frameRef =
            if frameRefs.ContainsKey(frameRef) then ok (frameRefs.[frameRef])
            else fail (DataNotExists(frameRef.ToString()))
        member this.GetByNonce nonce = 
            if frames.length > int(nonce) then ok frames.[int(nonce)]
            else fail (DataNotExists(nonce.ToString()))
        member this.GetFromNonce nonce =
            if frames.length > int(nonce) then ok (seq { for i = int(nonce) to frames.length do yield frames.[i] })
            else fail (DataNotExists(nonce.ToString()))
        member this.Push hashedEvent = 
            let currentFrame = (this :> IEventStream<'TState, 'TData>).CurrentFrame.bind(fun f -> Some f.Value)
            let newFrame = eventProcessor def currentFrame hashedEvent
            match newFrame with
            | Bad _ -> newFrame
            | Ok (frame, msgs) -> 
                let hashedFrame = dataHasher frame
                frames <- frames.Conj hashedFrame
                frameRefs <- frameRefs.Add (hashedFrame.Hash, hashedFrame)
                eventRefs <- eventRefs.Add (hashedFrame.Value.Event.HashedValue.Hash, hashedFrame.Value.Event.HashedValue)
                stateRefs <- stateRefs.Add (hashedFrame.Value.State.HashedValue.Hash, hashedFrame.Value.State.HashedValue)
                merkledFrame <- Some (toMerkled frameSerializer hasher (merkledFrame.bind(fun f -> Some f.Merkle)) frame)
                //subscribers |> Seq.iter (fun s -> s hashedFrame.Value.State.HashedValue.Value.Value)
                newFrame
