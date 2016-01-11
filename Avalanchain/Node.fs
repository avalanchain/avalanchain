module Avalanchain.Node

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
open EventStream
open FrameSynchronizer


// Bag of Streams
type EventStreamBag<'TState, 'TData when 'TData: equality and 'TState: equality>(streams: IEventStream<'TState, 'TData> list) = 
    let mutable streamMap = Map(streams.map(fun s -> s.Def.Value.Ref, s))
    // Checks if all toEmit streams have corresponding definitions
    let allEmitsExist = 
        let notDefinedEmitStream = 
            streams.
                collect(fun s -> s.Def.Value.EmitsTo).
                tryFind(fun es -> streamMap.tryFind(fun sm -> es.Hash = sm.Key.Hash).IsNone)
        if notDefinedEmitStream.IsSome
        then failwith (sprintf "Stream {%s} for Emitting is not defined" notDefinedEmitStream.Value.Value.Path)

    member this.Streams = streamMap
    member this.Add (stream: IEventStream<'TState, 'TData>) = streamMap.Add (stream.Def.Value.Ref, stream)
    member this.Delete (streamRef: Hashed<EventStreamRef>) = streamMap.Remove streamRef
    member this.Item (streamRef: Hashed<EventStreamRef>) = streamMap.[streamRef]

type Topology<'TState, 'TData when 'TData: equality and 'TState: equality> = IEventStream<'TState, 'TData> list // TODO: Probably replace with some Tree<'T>?

type Node<'TState, 'TData when 'TData: equality and 'TState: equality> = {
    Path: NodePath
    CryptoContext: CryptoContext
    Serializers: Serializers<'TState, 'TData>
    Streams: EventStreamBag<'TState, 'TData>
    FrameSynchronizationContextBuilder: ExecutionGroup -> ExecutionPolicy -> FrameSynchronizationContext<'TState, 'TData>

    // OwnIPAddress: IPAddress
    ExecutionGroups: ExecutionGroup list 
}



