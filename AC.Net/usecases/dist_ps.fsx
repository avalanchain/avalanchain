open System
open System.IO
#if INTERACTIVE
let cd = Path.Combine(__SOURCE_DIRECTORY__, "../bin/Avalanchain")
System.IO.Directory.SetCurrentDirectory(cd)
#endif

#r "../bin/Avalanchain/System.Collections.Immutable.dll"
#r "../bin/Avalanchain/Akka.dll"
#r "../bin/Avalanchain/FSharp.Control.AsyncSeq.dll"
#r "../bin/Avalanchain/Akka.Cluster.dll"
#r "../bin/Avalanchain/Akka.Cluster.Tools.dll"
#r "../bin/Avalanchain/Akka.Cluster.Sharding.dll"
#r "../bin/Avalanchain/Newtonsoft.Json.dll"
#r "../bin/Avalanchain/FSharp.PowerPack.Linq.dll"
#r "../bin/Avalanchain/DotNetty.Common.dll"
#r "../bin/Avalanchain/DotNetty.Buffers.dll"
#r "../bin/Avalanchain/DotNetty.Codecs.dll"
#r "../bin/Avalanchain/DotNetty.Handlers.dll"
#r "../bin/Avalanchain/DotNetty.Transport.dll"
#r "../bin/Avalanchain/FsPickler.dll"
#r "../bin/Avalanchain/Google.ProtocolBuffers.dll"
#r "../bin/Avalanchain/Google.ProtocolBuffers.Serialization.dll"
#r "../bin/Avalanchain/Akka.Persistence.dll"
#r "../bin/Avalanchain/Akka.Remote.dll"
#r "../bin/Avalanchain/Google.ProtocolBuffers.dll"
#r "../bin/Avalanchain/Akka.Cluster.dll"
#r "../bin/Avalanchain/Akka.DistributedData.dll"
#r "../bin/Avalanchain/Akka.Serialization.Hyperion.dll"
#r "../bin/Avalanchain/Akkling.dll"
#r "../bin/Avalanchain/Akkling.DistributedData.dll"
#r "../bin/Avalanchain/Akkling.Persistence.dll"
#r "../bin/Avalanchain/Akkling.Cluster.Sharding.dll"
#r "../bin/Avalanchain/Akka.Streams.dll"
#r "../bin/Avalanchain/Akkling.Streams.dll"
#r "../bin/Avalanchain/Reactive.Streams.dll"
#r "../bin/Avalanchain/Hyperion.dll"


open System.Collections.Immutable
open FSharp.Control

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
        }
        persistence {
            journal.plugin = "akka.persistence.journal.inmem"
            snapshot-store.plugin = "akka.persistence.snapshot-store.local"
        }
    }
    """ endpoint.IP endpoint.IP endpoint.Port seedNodes
    |> Configuration.parse
    |> System.create systemName 


let (|SubscribeAck|_|) (msg: obj) : Akka.Cluster.Tools.PublishSubscribe.SubscribeAck option =
    match msg with
    | :? Akka.Cluster.Tools.PublishSubscribe.SubscribeAck as e -> Some e
    | _ -> None
let (|UnsubscribeAck|_|) (msg: obj) : Akka.Cluster.Tools.PublishSubscribe.UnsubscribeAck option =
    match msg with
    | :? Akka.Cluster.Tools.PublishSubscribe.UnsubscribeAck as e -> Some e
    | _ -> None


type DistPubSubMessage<'T> =
    | Message of 'T

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
            match dps with
            | Message m ->
                printfn "Received: %A" msg
                queue.AsyncOffer(m) |!> (typed ActorBase.Context.Self)
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
let node1 = setupNode endpoint1 [endpoint1; endpoint2]
Threading.Thread.Sleep 5000
let node2 = setupNode endpoint2 [endpoint1; endpoint2]
Threading.Thread.Sleep 2000


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
mediator.Tell(Publish(topic, Message "msg 1"))

let source = distPubSub<string> node1 topic OverflowStrategy.DropNew 1000000

source
|> Source.runForEach mat (printfn "Piu: %A")
|> Async.Start

let mediator2 = DistributedPubSub.Get(node2).Mediator
mediator2.Tell(Publish(topic, Message "msg 2"))


