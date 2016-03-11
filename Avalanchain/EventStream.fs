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

type EventStreamDef<'TState, 'TData when 'TData: equality and 'TState: equality> = {
    Ref: Hashed<EventStreamRef>
    Projection: Projection<'TState, 'TData>
    InitialState: 'TState
    //EmitsTo: Hashed<EventStreamRef> list //TODO: Add EmitTo
    ExecutionPolicy: ExecutionPolicy 
}
and StreamState<'TState when 'TState: equality> = { 
    Value: 'TState 
    StreamRef: Hashed<EventStreamRef>
    Nonce: Nonce
}
and EventStreamReader<'TState when 'TState: equality> = {
    Reader: 'TState -> unit
    //EmitsTo: Hashed<EventStreamRef> list //TODO: Add EmitTo
    ExecutionPolicy: ExecutionPolicy 
}
and HashedState<'TState when 'TState: equality> = Hashed<StreamState<'TState>> //* EventSpine
and MerkledState<'TState when 'TState: equality> = Merkled<StreamState<'TState>>
and StateRef = Hash
and Snapshot<'TState when 'TState: equality> = HashedState<'TState>

type EventStreamFrame<'TState, 'TData when 'TData: equality and 'TState: equality> = {
    //Def: Hashed<EventStreamDef<'TState, 'TData>>
    Ref: Hashed<EventStreamRef>
    DefHash: Hash
    TimeStamp: DateTimeOffset
    Event: MerkledEvent<'TData>
    State: MerkledState<'TState>
    Nonce: Nonce
    Proofs: Set<Hashed<ExecutionProof>>
    //StreamStatus: EventStreamStatus<'TData>
} with 
    //member inline this.Path = this.Def.Value.Ref.Value.Path
    //member inline this.Version = this.Def.Value.Ref.Value.Version
    member inline this.Path = this.Ref.Value.Path
    member inline this.Version = this.Ref.Value.Version
//    member inline this.Hash = this.Def.Value.Ref.Hash
and HashedFrame<'TState, 'TData when 'TData: equality and 'TState: equality> = Hashed<EventStreamFrame<'TState, 'TData>> //* EventSpine
and MerkledFrame<'TState, 'TData when 'TData: equality and 'TState: equality> = Merkled<EventStreamFrame<'TState, 'TData>>
and FrameRef = Hash

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
    
type EventProcessingResult<'TState, 'TData when 'TData: equality and 'TState: equality> = Result<EventStreamFrame<'TState, 'TData>, EventProcessingMsg>

type StreamEventProcessor<'TState, 'TData when 'TData: equality and 'TState: equality> = 
    Hashed<EventStreamDef<'TState, 'TData>> -> EventStreamFrame<'TState, 'TData> option -> HashedEvent<'TData> -> EventProcessingResult<'TState, 'TData>

type Serializers<'TState, 'TData when 'TData: equality and 'TState: equality> = {
    streamRef: Serializer<EventStreamRef>
    streamDef: Serializer<EventStreamDef<'TState, 'TData>>
    data: Serializer<'TData>
    event: Serializer<Event<'TData>>
    state: Serializer<StreamState<'TState>>
    frame: Serializer<EventStreamFrame<'TState, 'TData>>
    epd: Serializer<ExecutionProofData>
    ep: Serializer<ExecutionProof>
    exp: Serializer<ExecutionPolicy>
    projection: Serializer<Projection<'TState, 'TData>>
    nodeRef: Serializer<NodeRefData>
}

type DataHashers<'TState, 'TData when 'TData: equality and 'TState: equality> = {
    streamRefDh: DataHasher<EventStreamRef>
    streamDefDh: DataHasher<EventStreamDef<'TState, 'TData>>
    dataDh: DataHasher<'TData>
    eventDh: DataHasher<Event<'TData>>
    stateDh: DataHasher<StreamState<'TState>>
    frameDh: DataHasher<EventStreamFrame<'TState, 'TData>>
    epdDh: DataHasher<ExecutionProofData>
    epDh: DataHasher<ExecutionProof>
    expDh: DataHasher<ExecutionPolicy>
    projectionDh: DataHasher<Projection<'TState, 'TData>>
    nodeRefDh: DataHasher<NodeRefData>
}

let picklerSerializers = {
    streamRef = picklerSerializer
    streamDef = picklerSerializer
    data = picklerSerializer
    event = picklerSerializer
    state = picklerSerializer
    frame = picklerSerializer
    epd = picklerSerializer
    ep = picklerSerializer
    exp = picklerSerializer
    projection = picklerSerializer
    nodeRef = picklerSerializer
}

let dataHashers<'TState, 'TData when 'TData: equality and 'TState: equality> ct (serializers: Serializers<'TState, 'TData>) = {
    streamRefDh = dataHasher serializers.streamRef ct
    streamDefDh = dataHasher serializers.streamDef ct
    dataDh = dataHasher serializers.data ct
    eventDh = dataHasher serializers.event ct
    stateDh = dataHasher serializers.state ct
    frameDh = dataHasher serializers.frame ct
    epdDh = dataHasher serializers.epd ct
    epDh = dataHasher serializers.ep ct
    expDh = dataHasher serializers.exp ct
    projectionDh = dataHasher serializers.projection ct
    nodeRefDh = dataHasher serializers.nodeRef ct
}

[<Interface>]
type IEventStream<'TState, 'TData when 'TData: equality and 'TState: equality> =
    abstract member Ref : Hashed<EventStreamRef> with get
    abstract member Def : Hashed<EventStreamDef<'TState, 'TData>> with get
    abstract member GetReader : ('TState -> unit) -> EventStreamReader<'TState> 
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
    //abstract member Push : 'TData -> EventProcessingResult<'TState, 'TData>
    // TODO: Change this to more functional approach
//    abstract member Subscribe : IEventStream<'TState, 'TData> -> unit
//    abstract member Unsubscribe : IEventStream<'TState, 'TData> -> unit


