module Avalanchain.Cluster.Sharded

open Akka.Actor
open Akka.Cluster
open Akka.Cluster.Sharding
open Akka.FSharp
open Akka.FSharp.Actors
open Akka.FSharp.Spawn
open System
open FSharp.Core

open Messages
open AutomaticCluster
open Akka.Persistence.FSharp
open Akka.Persistence

open Actors


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

type ActorSelector = string -> ActorSelection

type ShardedSystem (system, clusterFactory: ActorSystem -> IAutomaticCluster) =
    let automaticCluster = clusterFactory(system)
    let sharding = ClusterSharding.Get(system)
    member __.System = system
    member __.StartShardRegion<'Message, 'TAdminCommand, 'TBusinessCommand, 'TState, 'TEvent> (messageExtractor, eventSourcingLogic, regionName, options : SpawnOption list) = 
        let expr = <@ fun () -> new ResActor<'TAdminCommand, 'TBusinessCommand, 'TState, 'TEvent>(eventSourcingLogic) @>
        let props = Props.Create (Linq.Expression.ToExpression(expr))
        let appliedProps = applySpawnOptions props options
        sharding.Start(regionName, appliedProps, ClusterShardingSettings.Create(system), messageExtractor)
    member __.StartPersisted<'Message, 'TAdminCommand, 'TBusinessCommand, 'TState, 'TEvent> (eventSourcingLogic, name, options : SpawnOption list) = 
        let expr = <@ fun () -> new ResActor<'TAdminCommand, 'TBusinessCommand, 'TState, 'TEvent>(eventSourcingLogic) @>
        let props = Props.Create (Linq.Expression.ToExpression(expr))
        let appliedProps = applySpawnOptions props options
        system.ActorOf(appliedProps, name)
    member this.StartShardRegion<'Message, 'TAdminCommand, 'TBusinessCommand, 'TState, 'TEvent> (regionName, options) = 
        this.StartShardRegion (new ShardedMessageExtractor(), simpleEventSourcingLogic, regionName, options)
    member this.StartPersisted<'Message, 'TAdminCommand, 'TBusinessCommand, 'TState, 'TEvent> (name, options : SpawnOption list) = 
        this.StartPersisted (simpleEventSourcingLogic, name, options) 
    member this.ActorSelector: ActorSelector = (fun path -> system.ActorSelection(path))

    interface IDisposable with
        member __.Dispose() = automaticCluster.Dispose() // TODO: Implement the pattern properly

 
