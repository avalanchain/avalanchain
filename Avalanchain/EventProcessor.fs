module Avalanchain.EventProcessor

open System

open FSharp.Interop.Dynamic
open FSharp.Core.Fluent
open Chessie.ErrorHandling

open SecKeys
open SecPrimitives
open RefsAndPathes
open StreamEvent
open Projection
open EventStream

type ProofIt<'TState, 'TData> = Hashed<EventStreamRef> -> Nonce -> Hashed<StreamState<'TState>> -> HashedEvent<'TData> -> Hashed<ExecutionProof>

let proofIt 
    signer dataHasher 
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
    let proof = { Data = sd; Signature = signature }
    dataHasher(proof)    

let processEvent 
    (cryptoContext: CryptoContext) 
    serializers 
    (dataHasher: DataHasher<Event<'TData>>) 
    (permissionsChecker: HashedEvent<'TData> -> DataResult<unit>) 
    (proofIt: ProofIt<'TState, 'TData>)
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
        | Ok (_, _) -> ok event
        | Bad msgs -> fail (PermissionsFailure msgs)

    let project (streamFrame: EventStreamFrame<'TState, 'TData> option) (event: HashedEvent<'TData>) =
        let projection = streamDef.Value.Projection.F 
        let state = match streamFrame with
                    | Some sf -> sf.State.Value.Value
                    | None -> Unchecked.defaultof<'TState>
        try
            let res = projection.Invoke(state, event.Value.Data)
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

    let buildNewStream (state: StreamState<'TState>) =
        let nonce = state.Nonce
        let merkledEvent = toMerkled serializers.event cryptoContext.Hasher (streamFrame |> Option.bind (fun sf -> Some sf.Event.Merkle)) hashedEvent.Value
        let merkledState = toMerkled serializers.state cryptoContext.Hasher (streamFrame |> Option.bind (fun sf -> Some sf.State.Merkle)) state
        let newStreamFrame = {
                                Def = streamDef
                                TimeStamp = DateTimeOffset.UtcNow
                                Event = merkledEvent
                                State = merkledState 
                                Nonce = nonce
                                Proofs = [proofIt streamDef.Value.Ref nonce merkledState.HashedValue hashedEvent] |> Set.ofList
                                // StreamStatus = streamFrame.StreamStatus
                            }
        ok newStreamFrame

    let run =
        checkIntegrity
        >> bind checkPermissions
        >> bind (project streamFrame)
        >> bind buildNewStream

    run hashedEvent 
 
