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
open Utils

type EventStreamStatus<'TData> =
    | Offline
    | Online
    | Paused
    | BlockedByEvent of string * MerkledEvent<'TData> * Exception option

type EventStreamDef<'TState, 'TData> = {
    Ref: Hashed<EventStreamRef>
    Projection: Projection<'TState, 'TData>
    EmitsTo: Hashed<EventStreamRef> list
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
    projection: Serializer<Projection<'TState, 'TData>>
}

let picklerSerializers = {
    data = picklerSerializer
    event = picklerSerializer
    state = picklerSerializer
    frame = picklerSerializer
    epd = picklerSerializer
    projection = picklerSerializer
}

[<Interface>]
type IEventStream<'TState, 'TData when 'TData: equality and 'TState: equality> =
    abstract member Def : Hashed<EventStreamDef<'TState, 'TData>> with get
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


