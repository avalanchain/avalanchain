#load "../.paket/load/net461/main.group.fsx"

#load "lib/ws.fs"

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

let setupNode endpoint (seedNodes: Endpoint list) =
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
    
    config//.WithFallback (DistributedPubSub.DefaultConfig())
    |> System.create systemName 


let (|SubscribeAck|_|) (msg: obj) : Akka.Cluster.Tools.PublishSubscribe.SubscribeAck option =
    match msg with
    | :? Akka.Cluster.Tools.PublishSubscribe.SubscribeAck as e -> Some e
    | _ -> None
let (|UnsubscribeAck|_|) (msg: obj) : Akka.Cluster.Tools.PublishSubscribe.UnsubscribeAck option =
    match msg with
    | :? Akka.Cluster.Tools.PublishSubscribe.UnsubscribeAck as e -> Some e
    | _ -> None


// type DistPubSubMessage<'T> =
//     | Message of 'T

type [<CLIMutable>] DistPubSubMessage<'T> =
    { Message: 'T }

type MediatorPublisher<'T>(topic: string, queue: ISourceQueue<'T>, log: string -> unit) as actor =
    inherit Akka.Actor.ActorBase()
    do printfn "%s" "Initing"
    let mdr = typed (DistributedPubSub.Get(ActorBase.Context.System).Mediator)
    do mdr <! new Subscribe(topic, actor.Self)
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
            printfn "Received: %A" msg
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


let endpoint1 = { IP = "127.0.0.1"; Port = 5000us }
let endpoint2 = { IP = "127.0.0.1"; Port = 5001us }
let endpoint3 = { IP = "127.0.0.1"; Port = 5002us }

let node1 = setupNode endpoint1 [endpoint1] //[endpoint1; endpoint2]
Threading.Thread.Sleep 5000
let node2 = setupNode endpoint2 [endpoint2]
Threading.Thread.Sleep 2000

let node3 = setupNode endpoint3 [endpoint1; endpoint2]

let mat = node1.Materializer()

let distPubSub<'T> system topic (overflowStrategy: OverflowStrategy) (maxBuffer: int) =
    Source.queue overflowStrategy maxBuffer
    |> Source.mapMaterializedValue(fun queue ->
                                    MediatorPublisher<'T>.Props(topic, queue, printfn "%A")
                                    |> Props.From
                                    |> spawnAnonymous system
                                    |> ignore // TODO: Add actor removing
                                    Akka.NotUsed.Instance)

let topic = "distpubsub"
let mediator = DistributedPubSub.Get(node1).Mediator
mediator.Tell(Publish(topic, { Message = "msg 1" } ))

let source = distPubSub<string> node1 topic OverflowStrategy.DropNew 1000000

source
|> Source.runForEach mat (printfn "Piu: %A")
|> Async.Start

let mediator2 = DistributedPubSub.Get(node2).Mediator
mediator2.Tell(Publish(topic, { Message = "msg 2" }))


let mediator3 = DistributedPubSub.Get(node3).Mediator
mediator3.Tell(Publish(topic, { Message = "msg 3" } ))

let source3 = distPubSub<string> node1 topic OverflowStrategy.DropNew 1000000

source3
|> Source.runForEach mat (printfn "Piu: %A")
|> Async.Start
