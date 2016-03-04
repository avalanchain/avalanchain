module Avalanchain.Cluster.Sharded

open Akka.Actor

open Akka.FSharp
open Akka.FSharp.Actors
open Akka.FSharp.Spawn
open Akka.Cluster
open Akka.Cluster.Sharding
open System
open FSharp.Core

open Messages
open AutomaticCluster
open Akka.Persistence.FSharp
open Akka.Persistence

open Actors
open Avalanchain
open Avalanchain.EventStream
open Avalanchain.Quorum
open Avalanchain.Cluster.Extension
open Node


type ShardedMessageExtractor() =
    interface IMessageExtractor with 
        member __.EntityId(message) = match message with
                                        | :? ShardedMessage as msg -> msg.EntityId
                                        | _ -> null
        member __.ShardId(message) = match message with
                                        | :? ShardedMessage as msg -> msg.ShardId
                                        | _ -> null
        member __.EntityMessage(message) = match message with
                                                | :? ShardedMessage as msg -> msg.Message :> Object
                                                | _ -> null

type PathPrefixes = {
    KeyValue: string
    LightStream: string
    LocalStream: string
    Node: string
}

type ActorSelector = string -> ActorSelection

type ShardedSystem (system, clusterFactory: ActorSystem -> IAutomaticCluster) =
    let automaticCluster = clusterFactory(system)
    let sharding = ClusterSharding.Get(system)
    let chainNode = ChainNode.Get(system)
    let pathPrefixes = {
        KeyValue = "kv"
        LightStream = "stream0"
        LocalStream = "stream"
        Node = "node"
    }

    member __.System = system
    member __.StartShardRegion<'TCommand, 'TEvent, 'TState, 'TFrame, 'TMsg> (messageExtractor, eventSourcingLogic, regionName, options : SpawnOption list) = 
        let expr = <@ fun () -> new ResActor<'TCommand, 'TEvent, 'TState, 'TFrame, 'TMsg>(eventSourcingLogic) @>
        let props = Props.Create (Linq.Expression.ToExpression(expr))
        let appliedProps = applySpawnOptions props options
        sharding.Start(regionName, appliedProps, ClusterShardingSettings.Create(system), messageExtractor)
    member __.StartPersisted<'TCommand, 'TEvent, 'TState, 'TFrame, 'TMsg> (eventSourcingLogic, name, options : SpawnOption list) = 
        let expr = <@ fun () -> new ResActor<'TCommand, 'TEvent, 'TState, 'TFrame, 'TMsg>(eventSourcingLogic) @>
        let props = Props.Create (Linq.Expression.ToExpression(expr))
        let appliedProps = applySpawnOptions props options
        system.ActorOf(appliedProps, name)
    member this.StartShardRegion<'TAdminCommand, 'TBusinessCommand, 'TState, 'TEvent> (regionName, options) = 
        this.StartShardRegion (new ShardedMessageExtractor(), simpleEventSourcingLogic, regionName, options)
    member this.StartPersisted<'TAdminCommand, 'TBusinessCommand, 'TState, 'TEvent> (name, options : SpawnOption list) = 
        this.StartPersisted (simpleEventSourcingLogic, name, options) 

//    member this.StartKeyValue<'TCommand, 'TEvent, 'TState, 'TFrame, 'TMsg> (name, options : SpawnOption list) = 
//        this.StartPersisted(chainNode.KeyValueLogic, pathPrefixes.KeyValue + name, options) // TODO: Change to sharded
//    member this.StartLightStream<'TCommand, 'TEvent, 'TState, 'TFrame, 'TMsg> (name, projection, options : SpawnOption list) = 
//        this.StartPersisted(chainNode.StreamLogic projection, pathPrefixes.LightStream + name, options)
//    member this.StartLocalStream<'TCommand, 'TEvent, 'TState, 'TFrame, 'TMsg when 'TEvent: equality and 'TState: equality> 
//        (streamDef: Hashed<EventStreamDef<'TEvent, 'TState>>, options : SpawnOption list) = 
//        this.StartPersisted(chainNode.StreamLogic2<'TEvent, 'TState> streamDef, pathPrefixes.LocalStream + streamDef.Value.Ref.Value.Path, options)



//    member this.StartClusterStream<'TCommand, 'TEvent, 'TState, 'TFrame, 'TMsg when 'TEvent: equality and 'TState: equality> 
//        (ep: ExecutionPolicy) (streamDef: Hashed<EventStreamDef<'TEvent, 'TState>>) = 
////        let options = 
////            [ 
////                SpawnOption.Deploy (Akka.Actor.Deploy (ClusterScope.Instance))
////                SpawnOption.Router (
////                    new Akka.Cluster.Routing.ClusterRouterPool(
////                        new Akka.Routing.BroadcastPool(8),
////                        new Akka.Cluster.Routing.ClusterRouterPoolSettings(4, true, eg.Value, 2)))
////            ]
//        let routerTypes = processExecutionPolicy (chainNode.GetNodeContext<'TEvent, 'TState>().DataHashers.expDh) [] ep
//        routerTypes |> List.map (toSpawnOption >> fun (rt, options) -> this.StartLocalStream (streamDef, options))


    member this.ActorSelector: ActorSelector = (fun path -> system.ActorSelection(path))

    interface IDisposable with
        member __.Dispose() = automaticCluster.Dispose() // TODO: Implement the pattern properly

 

