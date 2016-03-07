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
open Avalanchain.NodeContext

type ChainNodeExtension =
    inherit ExtensionIdProvider<ChainNode>
        override __.CreateExtension (system: ExtendedActorSystem) = new ChainNode(system :?> ActorSystemImpl)

and ChainNode(system: ActorSystemImpl) =
    let ct = Utils.cryptoContext
    let nodeStore = NodeStore(ct)

    member __.GetNode<'TS, 'TD when 'TD: equality and 'TS: equality>(path: NodePath, executionGroups: ExecutionGroup list) =
        nodeStore.GetNode<'TS, 'TD>(path, executionGroups)

    member __.CryptoContext = ct
    member __.NodeStore = nodeStore

    interface Akka.Actor.IExtension 
    static member Get (system: ActorSystem): ChainNode =
            system.WithExtension<ChainNode, ChainNodeExtension>()

