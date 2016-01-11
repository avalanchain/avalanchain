module Avalanchain.EventStream

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
open Quorum
open Acl

type EventStreamStatus<'TData> =
    | Offline
    | Online
    | Paused
    | BlockedByEvent of string * MerkledEvent<'TData> * Exception option

type EventStreamDef<'TState, 'TData> = {
    Ref: Hashed<EventStreamRef>
    Projection: Projection<'TState, 'TData>
    EmitsTo: EventStreamRef list
    ExecutionPolicy: ExecutionPolicy 
}
and StreamState<'TState> = { 
    Value: 'TState 
    StreamRef: Hashed<EventStreamRef>
    Nonce: Nonce
}
and HashedState<'TState> = Hashed<StreamState<'TState>> //* EventSpine
and MerkledState<'TState> = Merkled<StreamState<'TState>>
and StateRef = Hash
and Snapshot<'TState> = HashedState<'TState>

type EventStreamFrame<'TState, 'TData> = {
    Def: Hashed<EventStreamDef<'TState, 'TData>>
    TimeStamp: DateTimeOffset
    Event: MerkledEvent<'TData>
    State: MerkledState<'TState>
    Nonce: Nonce
    Proofs: Set<Hashed<ExecutionProof>>
    //StreamStatus: EventStreamStatus<'TData>
} with 
    member inline this.Path = this.Def.Value.Ref.Value.Path
    member inline this.Version = this.Def.Value.Ref.Value.Version
//    member inline this.Hash = this.Def.Value.Ref.Hash
and HashedFrame<'TState, 'TData> = Hashed<EventStreamFrame<'TState, 'TData>> //* EventSpine
and MerkledFrame<'TState, 'TData> = Merkled<EventStreamFrame<'TState, 'TData>>
and FrameRef = Hash


//type EventStream1<'TState, 'TData> = {
//    Def: Hashed<EventStreamDef<'TState, 'TData>>
//    //TimeStamp: DateTimeOffset
//    Steps: Merkled<EventStreamFrame<'TState, 'TData>> list
//    Events: MerkledEvent<'TData>
//    States: Hashed<StreamState<'TState>>
//    LatestNonce: Nonce
//    StreamStatus: EventStreamStatus<'TData>
//} with 
//    member inline this.Path = this.Def.Value.Ref.Value.Path
//    member inline this.Version = this.Def.Value.Ref.Value.Version
////    member inline this.Hash = this.Def.Value.Ref.Hash


type DataAccessIssue = 
    | AccessBlocked
    | DataNotExists of string
    | TemporalTechProblem of string

type DataResult<'T> = Chessie.ErrorHandling.Result<'T, DataAccessIssue>

type EventProcessingMsg = 
    | IntegrityFailure
    | PermissionsFailure of DataAccessIssue list
    | ProcessingFailure of string list
    | SecurityWarning of string
    | ExecutionWarning of string
    
type EventProcessingResult<'TState, 'TData> = Result<EventStreamFrame<'TState, 'TData>, EventProcessingMsg>

type StreamEventProcessor<'TState, 'TData> = 
    Hashed<EventStreamDef<'TState, 'TData>> -> EventStreamFrame<'TState, 'TData> option -> HashedEvent<'TData> -> EventProcessingResult<'TState, 'TData>

type Serializers<'TState, 'TData> = {
    data: Serializer<'TData>
    event: Serializer<Event<'TData>>
    state: Serializer<StreamState<'TState>>
    frame: Serializer<EventStreamFrame<'TState, 'TData>>
    epd: Serializer<ExecutionProofData>
}

[<Interface>]
type IEventStream<'TState, 'TData when 'TData: equality and 'TState: equality> =
    abstract member CurrentFrame : HashedFrame<'TState, 'TData> option with get
    abstract member CurrentState : HashedState<'TState> option with get
    abstract member GetEvent<'TData> : EventRef -> DataResult<HashedEvent<'TData>>
    //abstract member GetEventSpine<'TData> : EventRef -> DataResult<MerkledEvent<'TData>>
    abstract member GetState<'TState> : StateRef -> DataResult<HashedState<'TState>>
    //abstract member GetStateSpine<'TState> : StateRef -> DataResult<MerkledState<'TState>>
    abstract member GetFrame<'TState, 'TData> : FrameRef -> DataResult<HashedFrame<'TState, 'TData>>
    abstract member GetByNonce : Nonce -> DataResult<HashedFrame<'TState, 'TData>>
    abstract member GetFromNonce : Nonce -> DataResult<HashedFrame<'TState, 'TData> seq>
    //abstract member Get<'TData> : Hash -> Hashed<'TData> option
    abstract member Push : HashedEvent<'TData> -> EventProcessingResult<'TState, 'TData>


type EventStream<'TState, 'TData when 'TData: equality and 'TState: equality>
    (def, acl, hasher, dataHasher, eventProcessor: StreamEventProcessor<'TState, 'TData>, frameSynchronizer(*, initFrame *), frameSerializer) =

    let mutable frames = PersistentVector.empty
    let mutable frameRefs = PersistentHashMap.empty
    let mutable eventRefs = PersistentHashMap.empty
    let mutable stateRefs = PersistentHashMap.empty
    let mutable merkledFrame = Option.None 

    //member private this.toMerkled<'T> = 
    //member this.CurrentFrame = frames.Head

    // TODO: Add Acl checks
    interface IEventStream<'TState, 'TData> with 
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
                newFrame