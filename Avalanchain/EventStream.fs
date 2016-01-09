module Avalanchain.EventStream

open System

open FSharp.Interop.Dynamic
open FSharp.Core.Fluent
open Chessie.ErrorHandling

open SecKeys
open SecPrimitives
open RefsAndPathes
open StreamEvent
open Projection
open Quorum

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
and HashedAggregate<'TState> = Hashed<StreamState<'TState>> * EventSpine
and Snapshot<'TState> = HashedAggregate<'TState>

type EventStreamFrame<'TState, 'TData> = {
    Def: Hashed<EventStreamDef<'TState, 'TData>>
    TimeStamp: DateTimeOffset
    Event: MerkledEvent<'TData>
    State: Merkled<StreamState<'TState>>
    Nonce: Nonce
    Proofs: Set<Hashed<ExecutionProof>>
    //StreamStatus: EventStreamStatus<'TData>
} with 
    member inline this.Path = this.Def.Value.Ref.Value.Path
    member inline this.Version = this.Def.Value.Ref.Value.Version
//    member inline this.Hash = this.Def.Value.Ref.Hash

type EventStream<'TState, 'TData> = {
    Def: Hashed<EventStreamDef<'TState, 'TData>>
    //TimeStamp: DateTimeOffset
    Steps: Merkled<EventStreamFrame<'TState, 'TData>> list
    Events: MerkledEvent<'TData>
    States: Hashed<StreamState<'TState>>
    LatestNonce: Nonce
    StreamStatus: EventStreamStatus<'TData>
} with 
    member inline this.Path = this.Def.Value.Ref.Value.Path
    member inline this.Version = this.Def.Value.Ref.Value.Version
//    member inline this.Hash = this.Def.Value.Ref.Hash




type StreamEventProcessor<'TState, 'TData> = 
    EventStreamFrame<'TState, 'TData> -> Event<'TData> -> Result<EventStreamFrame<'TState, 'TData>, string>

type Serializers<'TData, 'TState> = {
    data: Serializer<'TData>
    event: Serializer<Event<'TData>>
    state: Serializer<StreamState<'TState>>
    epd: Serializer<ExecutionProofData>
}

