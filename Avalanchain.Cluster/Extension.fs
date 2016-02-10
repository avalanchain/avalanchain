module Avalanchain.Cluster.Extension

open Akka.Actor
open Akka.Actor.Internal
open Avalanchain.Cluster.Actors

type ChainNodeExtension =
    inherit ExtensionIdProvider<ChainNode>
        override __.CreateExtension (system: ExtendedActorSystem) = new ChainNode(system :?> ActorSystemImpl)

and ChainNode(system: ActorSystemImpl) =
    interface Akka.Actor.IExtension 
    static member Get (system: ActorSystem): ChainNode =
            system.WithExtension<ChainNode, ChainNodeExtension>()

    member this.KeyValueLogic = KeyValue.kvLogic
    member this.StreamLogic = Stream.streamLogic
    member this.StreamLogic2 = Stream2.streamLogic