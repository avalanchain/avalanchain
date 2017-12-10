open System

Text.Encoding.UTF8.GetString(Convert.FromBase64String("T2N0MjBvY3QyMA=="))

open System.IO

#I "bin/Debug/net461"

#r "System.Collections.Immutable.dll" 
#r "Akka.dll" 
#r "Hyperion.dll" 
#r "Newtonsoft.Json.dll" 
#r "FSharp.PowerPack.Linq.dll" 
#r "DotNetty.Common.dll" 
#r "DotNetty.Buffers.dll" 
#r "DotNetty.Codecs.dll" 
#r "DotNetty.Handlers.dll" 
#r "DotNetty.Transport.dll" 
#r "FsPickler.dll" 
#r "Google.Protobuf.dll" 
#r "Akka.Remote.dll" 
#r "Akka.Persistence.dll" 
#r "Akka.Cluster.dll" 
#r "Akka.Cluster.Tools.dll" 
#r "Akka.Cluster.Sharding.dll" 
#r "Akka.Serialization.Hyperion.dll" 
#r "Akkling.dll" 
#r "Akkling.Persistence.dll" 
#r "Akkling.Cluster.Sharding.dll" 
#r "Akka.Streams.dll" 
#r "Akkling.Streams.dll" 
#r "Reactive.Streams.dll" 


#if INTERACTIVE
let cd = Path.Combine(__SOURCE_DIRECTORY__, "bin/Debug/net461")
System.IO.Directory.SetCurrentDirectory(cd)
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

let configWithPort port =
    let config = Configuration.parse ("""
        akka {
            actor {
              provider = "Akka.Remote.RemoteActorRefProvider, Akka.Remote"
              serializers {
                hyperion = "Akka.Serialization.HyperionSerializer, Akka.Serialization.Hyperion"
              }
              serialization-bindings {
                "System.Object" = hyperion
              }
            }
          remote {
            helios.tcp {
              public-hostname = "localhost"
              hostname = "localhost"
              port = """ + port.ToString() + """
            }
          }
          cluster {
            auto-down-unreachable-after = 5s
            seed-nodes = [ "akka.tcp://cluster-system@localhost:5000/" ]
          }
          persistence {
            journal.plugin = "akka.persistence.journal.inmem"
            snapshot-store.plugin = "akka.persistence.snapshot-store.local"
          }
        }
        """)
    config.WithFallback(ClusterSingletonManager.DefaultConfig())

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


let system1 = System.create "cluster-system" (configWithPort 5000)
let mat = system1.Materializer()

let distPubSub<'T> system topic (overflowStrategy: OverflowStrategy) (maxBuffer: int) =
    Source.queue overflowStrategy maxBuffer
    |> Source.mapMaterializedValue(fun queue ->
                                    MediatorPublisher<'T>.Props(topic, queue, printfn "%A")
                                    |> Props.From
                                    |> spawnAnonymous system
                                    |> ignore // TODO: Add actor removing
                                    Akka.NotUsed.Instance)

let topic = "distpubsub"
let mediator = DistributedPubSub.Get(system1).Mediator;
mediator.Tell(new Publish(topic, Message "msg 1"));

let source = distPubSub<string> system1 topic OverflowStrategy.DropNew 1000

source
|> Source.runForEach mat (printfn "Piu: %A")
|> Async.Start

let system2 = System.create "cluster-system" (configWithPort 5001)
let mediator2 = DistributedPubSub.Get(system2).Mediator;
mediator2.Tell(new Publish(topic, Message "msg 2"));