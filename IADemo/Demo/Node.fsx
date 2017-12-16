#load "../.paket/load/net461/main.group.fsx"

// #load "lib/ws.fs"

open System
open System.IO
#if INTERACTIVE
let cd = Path.Combine(__SOURCE_DIRECTORY__, "bin/Debug/net461")
System.IO.Directory.SetCurrentDirectory(cd)
#I "bin/Debug/net461"
#endif


open Akka.Actor
open Akka.Configuration
open Akka.Cluster
open Akka.Cluster.Tools.Singleton
open Akka.Cluster.Tools.PublishSubscribe
open Akka.Cluster.Sharding
open Akka.Persistence
open Akka.Streams
open Akka.Streams.Dsl
open Reactive.Streams

open Hyperion

open Akkling
open Akkling.Persistence
open Akkling.Cluster
open Akkling.Cluster.Sharding
open Akkling.Streams

    
module Network =
    type Endpoint = {
        IP: string
        Port: uint16
    }

open Network

type ACNode = {
    Endpoint: Endpoint
    System: ActorSystem
    Mat: ActorMaterializer
    OverflowStrategy: OverflowStrategy
    MaxBuffer: int
    Mediator: unit -> IActorRef 
}

// type DistPubSubMessage<'T> =
//     | Message of 'T

module DistPubSub =
    type [<CLIMutable>] DistPubSubMessage<'T> =
        { Message: 'T }

    let (|SubscribeAck|_|) (msg: obj) : Akka.Cluster.Tools.PublishSubscribe.SubscribeAck option =
        match msg with
        | :? Akka.Cluster.Tools.PublishSubscribe.SubscribeAck as e -> Some e
        | _ -> None
    let (|UnsubscribeAck|_|) (msg: obj) : Akka.Cluster.Tools.PublishSubscribe.UnsubscribeAck option =
        match msg with
        | :? Akka.Cluster.Tools.PublishSubscribe.UnsubscribeAck as e -> Some e
        | _ -> None
        

    type MediatorPublisher<'T>(topic: string, queue: ISourceQueue<'T>, log: string -> unit) as actor =
        inherit Akka.Actor.ActorBase()
        do log "Initing"
        let mdr = typed (DistributedPubSub.Get(ActorBase.Context.System).Mediator)
        do mdr <! Subscribe(topic, actor.Self)
        do log "Initialized"
        override actor.Receive (msg: obj) =
            match msg with
            | SubscribeAck _ ->
                log (sprintf "Actor subscribed to topic: %s" topic)
                true
            | UnsubscribeAck _ ->
                log (sprintf "Actor unsubscribed from topic: %s" topic)
                true
            | :? DistPubSubMessage<'T> as dps ->
                // match dps with
                // | Message m ->
                //     printfn "Received: %A" msg
                //     queue.AsyncOffer(m) |!> (typed ActorBase.Context.Self)
                //     true
                log (sprintf "Received: %A" msg)
                queue.AsyncOffer(dps.Message) |!> (typed ActorBase.Context.Self)
                true
            | :? IQueueOfferResult as qr ->
                match qr with
                | :? QueueOfferResult.Enqueued -> true
                | :? QueueOfferResult.Dropped -> failwith "Message dropped"
                | :? QueueOfferResult.Failure as f -> failwithf "Failed with exception: %A" f.Cause
                | :? QueueOfferResult.QueueClosed -> failwith "Queue closed"
                | _ -> false
            | _ ->
                log (sprintf "Unhandled: %A" msg)
                actor.Unhandled msg
                false
        static member Props (topic: string, queue: ISourceQueue<'T>, log: string -> unit) = Props.Create<MediatorPublisher<'T>>(topic, queue, log)

    let distPubSubSource<'T> system topic (overflowStrategy: OverflowStrategy) (maxBuffer: int) =
        Source.queue overflowStrategy maxBuffer
        |> Source.mapMaterializedValue(fun queue ->
                                        MediatorPublisher<'T>.Props(topic, queue, printfn "%A")
                                        |> Props.From
                                        |> spawnAnonymous system
                                        |> ignore // TODO: Add actor removing
                                        Akka.NotUsed.Instance)

    let topic = "distpubsub"
    let distPubSubSink<'T, 'mat> system topic = 
        let mediator = DistributedPubSub.Get(system).Mediator |> typed
        Flow.id<'T, 'mat>
        |> Flow.toMat(Sink.forEach(fun (msg: 'T) -> mediator <! (Publish(topic, { Message = msg } )))) Keep.left
        

let setupNode endpoint (seedNodes: Endpoint list) (overflowStrategy: OverflowStrategy) (maxBuffer: int) = //(distTopic: string option) =
    let systemName = "ac"
    let seedNodes = seedNodes 
                    |> List.map (fun ep -> sprintf "\"akka.tcp://%s@%s:%d/\"" systemName ep.IP ep.Port) 
                    |> fun l -> "[" + String.Join(", ", l) + "]"
    printfn "%s" seedNodes
    let config = 
        sprintf """
            akka {
                actor {
                    provider = "Akka.Cluster.ClusterActorRefProvider, Akka.Cluster"
                    serializers {
                        hyperion = "Akka.Serialization.HyperionSerializer, Akka.Serialization.Hyperion"
                    }
                    serialization-bindings {
                        "System.Object" = hyperion
                    }
                }
                remote {
                    helios.tcp {
                    public-hostname = "%s"
                    hostname = "%s"
                    port = %d
                    maximum-frame-size = 40000000b
                    }
                }
                cluster {
                    auto-down-unreachable-after = 5s
                    seed-nodes = %s
                    distributed-data {
                        max-delta-elements = 10000
                    }

                    pub-sub {
                        # Actor name of the mediator actor, /system/distributedPubSubMediator
                        name = distributedPubSubMediator

                        # Start the mediator on members tagged with this role.
                        # All members are used if undefined or empty.
                        #role = ""

                        # The routing logic to use for 'Send'
                        # Possible values: random, round-robin, broadcast
                        routing-logic = broadcast

                        # How often the DistributedPubSubMediator should send out gossip information
                        gossip-interval = 1s

                        # Removed entries are pruned after this duration
                        removed-time-to-live = 120s

                        # Maximum number of elements to transfer in one message when synchronizing the registries.
                        # Next chunk will be transferred in next round of gossip.
                        max-delta-elements = 3000
                    }            
                }
                persistence {
                    journal.plugin = "akka.persistence.journal.inmem"
                    snapshot-store.plugin = "akka.persistence.snapshot-store.local"
                }
            }
            """ endpoint.IP endpoint.IP endpoint.Port seedNodes
            |> Configuration.parse
    
    let system = config//.WithFallback (DistributedPubSub.DefaultConfig())
                    |> System.create systemName 
    {   Endpoint = endpoint
        System = system
        Mat = system.Materializer()
        OverflowStrategy = overflowStrategy
        MaxBuffer = maxBuffer
        Mediator = fun () -> DistributedPubSub.Get(system).Mediator
         }            



let endpoint1 = { IP = "127.0.0.1"; Port = 5000us }
let endpoint2 = { IP = "127.0.0.1"; Port = 5001us }
let endpoint3 = { IP = "127.0.0.1"; Port = 5002us }

let node1 = setupNode endpoint1 [endpoint1] (OverflowStrategy.DropNew) 1000 // None
Threading.Thread.Sleep 5000


///////

open Akka.Cluster
open Akka.DistributedData
open Akkling
open Akkling.DistributedData
open Akkling.DistributedData.Consistency

// let system = System.create "system" <| Configuration.parse """
// akka.actor.provider = "Akka.Cluster.ClusterActorRefProvider, Akka.Cluster"
// akka.remote.helios.tcp {
//     hostname = "127.0.0.1"
//     port = 4551
// }
// """
let cluster = Cluster.Get node1.System
let ddata = DistributedData.Get node1.System

// some helper functions
let (++) set e = ORSet.add cluster e set

// initialize set
let set = [ 1; 2; 3 ] |> List.fold (++) ORSet.empty

let key = ORSet.key "test-set"

// write that up in replicator under key 'test-set'
ddata.AsyncUpdate(key, set, writeLocal)
|> Async.RunSynchronously

// read data 
async {
    let! reply = ddata.AsyncGet(key, readLocal)
    match reply with
    | Some value -> printfn "Data for key %A: %A" key value
    | None -> printfn "Data for key '%A' not found" key
} |> Async.RunSynchronously

// delete data 
ddata.AsyncDelete(key, writeLocal) |> Async.RunSynchronously

//////

let node2 = setupNode endpoint2 [endpoint2]
Threading.Thread.Sleep 2000

let node3 = setupNode endpoint3 [endpoint1; endpoint2]



let source = distPubSub<string> node1 topic OverflowStrategy.DropNew 1000000

source
|> Source.runForEach mat (printfn "Piu: %A")
|> Async.Start

let mediator2 = DistributedPubSub.Get(node2).Mediator
mediator2.Tell(Publish(topic, { Message = "msg 2" }))


let mediator3 = DistributedPubSub.Get(node3).Mediator
mediator3.Tell(Publish(topic, { Message = "msg 3" } ))

let source3 = distPubSub<string> node3 topic OverflowStrategy.DropNew 1000000

source3
|> Source.runForEach mat (printfn "Piu: %A")
|> Async.Start
