module Avalanchain.Cluster.Extension

open System.Collections.Generic
open System.Collections.Concurrent

open Akka.Actor
open Akka.Actor.Internal
open Avalanchain
open Avalanchain.Cluster.Actors
open Avalanchain.Cluster.Actors.Stream2
open Chessie.ErrorHandling
open Avalanchain.EventStream
open Akka.FSharp
open Actors.KeyValue
open Actors.Stream
open Avalanchain.RefsAndPathes
open Avalanchain.Quorum

type ChainNodeExtension =
    inherit ExtensionIdProvider<ChainNode>
        override __.CreateExtension (system: ExtendedActorSystem) = new ChainNode(system :?> ActorSystemImpl)

and ChainNode(system: ActorSystemImpl) =
    let ct = Utils.cryptoContext
    let nodeContexts = new ConcurrentDictionary<string, obj>()

    member this.GetNode<'TS, 'TD when 'TD: equality and 'TS: equality>(path: NodePath, executionGroups: ExecutionGroup list) =
        // TODO: Add executionGroups processing
        let key = typedefof<'TD>.FullName + "~" + typedefof<'TS>.FullName + "~" + path
        let nc = 
            nodeContexts.GetOrAdd(key, (fun k -> (NodeContext.buildNode path executionGroups (NodeContext.buildNodeContext<'TD, 'TS>(ct))) :> obj))
        nc :?> NodeContext.Node<'TS, 'TD>

    interface Akka.Actor.IExtension 
    static member Get (system: ActorSystem): ChainNode =
            system.WithExtension<ChainNode, ChainNodeExtension>()

