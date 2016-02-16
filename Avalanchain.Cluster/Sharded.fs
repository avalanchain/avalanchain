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
open Extension
open Avalanchain
open Avalanchain.EventStream
open Avalanchain.Quorum
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
    StreamRefByHash: string
    StreamDefByHash: string
    EventByHash: string
    StateByHash: string
    FrameByHash: string
    MerkleByHash: string
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

        StreamRefByHash = "srbh"
        StreamDefByHash = "srbh"
        EventByHash = "ebh"
        StateByHash = "sbh"
        FrameByHash = "fbh"
        MerkleByHash = "mbh"
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

    member this.StartKeyValue<'TCommand, 'TEvent, 'TState, 'TFrame, 'TMsg> (name, options : SpawnOption list) = 
        this.StartPersisted(chainNode.KeyValueLogic, pathPrefixes.KeyValue + name, options) // TODO: Change to sharded
    member this.StartLightStream<'TCommand, 'TEvent, 'TState, 'TFrame, 'TMsg> (name, projection, options : SpawnOption list) = 
        this.StartPersisted(chainNode.StreamLogic projection, pathPrefixes.LightStream + name, options)
    member this.StartLocalStream<'TCommand, 'TEvent, 'TState, 'TFrame, 'TMsg when 'TEvent: equality and 'TState: equality> 
        (streamDef: Hashed<EventStreamDef<'TEvent, 'TState>>, options : SpawnOption list) = 
        this.StartPersisted(chainNode.StreamLogic2<'TEvent, 'TState> streamDef, pathPrefixes.LocalStream + streamDef.Value.Ref.Value.Path, options)

    member this.StartClusterStream<'TCommand, 'TEvent, 'TState, 'TFrame, 'TMsg when 'TEvent: equality and 'TState: equality> 
        (ep: ExecutionPolicy) (streamDef: Hashed<EventStreamDef<'TEvent, 'TState>>) = 
//        let options = 
//            [ 
//                SpawnOption.Deploy (Akka.Actor.Deploy (ClusterScope.Instance))
//                SpawnOption.Router (
//                    new Akka.Cluster.Routing.ClusterRouterPool(
//                        new Akka.Routing.BroadcastPool(8),
//                        new Akka.Cluster.Routing.ClusterRouterPoolSettings(4, true, eg.Value, 2)))
//            ]
        let routerTypes = processExecutionPolicy (chainNode.GetNodeContext<'TEvent, 'TState>().DataHashers.expDh) [] ep
        routerTypes |> List.map (toSpawnOption >> fun (rt, options) -> this.StartLocalStream (streamDef, options))


    member this.ActorSelector: ActorSelector = (fun path -> system.ActorSelection(path))

    interface IDisposable with
        member __.Dispose() = automaticCluster.Dispose() // TODO: Implement the pattern properly

 


let produceMessages (system: ActorSystem) (shardRegion: IActorRef) =
    let entitiesCount = 20
    let shardsCount = 10
    let rand = new Random()

    system.Scheduler.Advanced.ScheduleRepeatedly(
        TimeSpan.FromSeconds(1.0), 
        TimeSpan.FromSeconds(0.001), 
        fun () ->
            for i = 0 to 1 do
                let shardId = rand.Next(shardsCount)
                let entityId = rand.Next(entitiesCount)
                //printfn "Message# - %d" i
                shardRegion.Tell({ShardId = shardId.ToString(); EntityId = entityId.ToString(); Message = "hello world"})
    )

let runExample (system: ActorSystem) =
    //let shardedSystem = new ShardedSystem (system, (fun s -> new AutomaticClusterSqlite(s) :> IAutomaticCluster))
    let shardedSystem = new ShardedSystem (system, (fun s -> {new IAutomaticCluster 
                                                                interface IDisposable with member __.Dispose() = ()}))

    let kvRef = shardedSystem.StartKeyValue<double, double, double, double * double, string>("aa", []) 

    System.Threading.Thread.Sleep(2000)
    //Console.Write("Press ENTER to start producing messages...")
    //Console.ReadLine() |> ignore

    produceMessages system kvRef