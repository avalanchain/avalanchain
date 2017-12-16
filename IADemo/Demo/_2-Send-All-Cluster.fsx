//#load "../.paket/load/net461/main.group.fsx"

// #load "lib/ws.fs"

#r "../packages/FSharp.Data/lib/portable-net45+netcore45/FSharp.Data.DesignTime.dll" 

open System
open System.IO
#if INTERACTIVE
let cd = Path.Combine(__SOURCE_DIRECTORY__, "bin/Debug/net461")
let [<Literal>] ac_include = __SOURCE_DIRECTORY__ + "/AC_include.fsx"
let skipFiles = [ "mscorlib.dll"; "FSharp.Core.dll"; "e_sqlite3.dll" ]
Directory.EnumerateFiles(cd)
|> Seq.filter (fun fn -> fn.EndsWith ".dll" && (skipFiles |> List.exists (fn.Contains) |> not))
|> Seq.map (fun fn -> fn.Replace("\\", "/") |> sprintf "#r \"%s\"")
|> Seq.toArray
|> fun lines -> String.Join("\n", lines)
|> fun txt -> File.WriteAllText(ac_include, txt)

Directory.SetCurrentDirectory(__SOURCE_DIRECTORY__)
#load "AC_include.fsx"
Directory.SetCurrentDirectory(cd)
#endif


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

open Akka.DistributedData
open Akkling.DistributedData
open Akkling.DistributedData.Consistency

    
module Network =
    type Endpoint = {
        IP: string
        Port: uint16
    }

open Network

open Microsoft.FSharp.Quotations
open Microsoft.FSharp.Linq.QuotationEvaluation


let create (name : string) (config : Akka.Configuration.Config) : ActorSystem = 
    let system = ActorSystem.Create(name, config)
    let extendedSystem = system :?> ExtendedActorSystem
    let exprSerializer = Akkling.Serialization.ExprSerializer(extendedSystem)
    let akka_pubsub = Akka.Cluster.Tools.PublishSubscribe.Serialization.DistributedPubSubMessageSerializer(extendedSystem)
    let hyperion = Akka.Serialization.HyperionSerializer(extendedSystem)           // I don't know why, but without this system cannot instantiate serializer
    system.Serialization.AddSerializer("expr", exprSerializer)
    system.Serialization.AddSerializationMap(typeof<Expr>, exprSerializer)
    // system.Serialization.AddSerializer("akka-pubsub", akka_pubsub)
    // system.Serialization.AddSerializationMap(typeof<Akka.Cluster.Tools.PublishSubscribe.IDistributedPubSubMessage>, akka_pubsub)
    system.Serialization.AddSerializer("hyperion", hyperion)
    system.Serialization.AddSerializationMap(typeof<Object>, hyperion)
    // system.Serialization.AddSerializationMap(typeof<Akka.Cluster.Tools.PublishSubscribe.Internal.SendToOneSubscriber>, akka_pubsub)
    system


let setupNode endpoint (seedNodes: Endpoint list) =
    let systemName = "ac"
    let seedNodes = seedNodes 
                    |> List.map (fun ep -> sprintf "\"akka.tcp://%s@%s:%d/\"" systemName ep.IP ep.Port) 
                    |> fun l -> "[" + String.Join(", ", l) + "]"
    printfn "%s" seedNodes
    let config = sprintf """
                    akka {
                        actor {
                            provider = "Akka.Cluster.ClusterActorRefProvider, Akka.Cluster"
                            serializers {
                                akka-pubsub = "Akka.Cluster.Tools.PublishSubscribe.Serialization.DistributedPubSubMessageSerializer, Akka.Cluster.Tools"
                            }                            
                            serialization-bindings {
                                "Akka.Cluster.Tools.PublishSubscribe.IDistributedPubSubMessage, Akka.Cluster.Tools" = akka-pubsub
	                            "Akka.Cluster.Tools.PublishSubscribe.Internal.SendToOneSubscriber, Akka.Cluster.Tools" = akka-pubsub
                            }                            
                            serialization-identifiers {
                                "Akka.Cluster.Tools.PublishSubscribe.Serialization.DistributedPubSubMessageSerializer, Akka.Cluster.Tools" = 21
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
    
    // DistributedPubSub.DefaultConfig() 
    // |> Configuration.fallback config
    // |> System.create systemName 
    create systemName config

let endpoint1 = { IP = "127.0.0.1"; Port = 5000us }


let node1 = setupNode endpoint1 [endpoint1]

let cluster = Cluster.Get node1
let ddata = DistributedData.Get node1

// some helper functions
let (++) set e = ORSet.add cluster e set

// initialize set
let set = [ 1; 2; 3 ] |> List.fold (++) ORSet.empty

let key = ORSet.key<int> "test-set"

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



let (|SubscribeAck|_|) (msg: obj) : Akka.Cluster.Tools.PublishSubscribe.SubscribeAck option =
    match msg with
    | :? Akka.Cluster.Tools.PublishSubscribe.SubscribeAck as e -> Some e
    | _ -> None
let (|UnsubscribeAck|_|) (msg: obj) : Akka.Cluster.Tools.PublishSubscribe.UnsubscribeAck option =
    match msg with
    | :? Akka.Cluster.Tools.PublishSubscribe.UnsubscribeAck as e -> Some e
    | _ -> None


type DistPubSubMessage<'T> =
    { Message: 'T }

type MediatorPublisher<'T>(topic: string, queue: ISourceQueue<'T>, log: string -> unit) as actor =
    inherit Akka.Actor.ActorBase()
    do printfn "%s" "Initing"
    let mdr = typed (DistributedPubSub.Get(ActorBase.Context.System).Mediator)
    do mdr <! Akka.Cluster.Tools.PublishSubscribe.Subscribe(topic, actor.Self)
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

let node1 = setupNode endpoint1 [endpoint1]
Threading.Thread.Sleep 5000
let node2 = setupNode endpoint2 [endpoint1; endpoint2]
Threading.Thread.Sleep 2000

// let psser = Akka.Cluster.Tools.PublishSubscribe.Serialization.DistributedPubSubMessageSerializer(node1 :?> ExtendedActorSystem)
let mat1 = node1.Materializer()
let mat2 = node2.Materializer()

let distPubSub<'T> system topic (overflowStrategy: OverflowStrategy) (maxBuffer: int) =
    Source.queue overflowStrategy maxBuffer
    |> Source.mapMaterializedValue(fun queue ->
                                    MediatorPublisher<'T>.Props(topic, queue, printfn "%A")
                                    |> Props.From
                                    |> spawnAnonymous system
                                    |> ignore // TODO: Add actor removing
                                    Akka.NotUsed.Instance)

let topic = "distpubsub"
let receiver name node mat = 
    distPubSub<string> node topic OverflowStrategy.DropNew 1000000
    |> Source.runForEach mat (printfn "Piu %s: %A" name)
    |> Async.Start

receiver "n1" node1 mat1
receiver "n2" node2 mat2

let mediator = DistributedPubSub.Get(node1).Mediator
mediator.Tell(Publish(topic, { Message = "msg 1" }))

let mediator2 = DistributedPubSub.Get(node2).Mediator
mediator2.Tell(Publish(topic, { Message = "msg 2" }))

////// DD

// Source.o


type Broadcaster<'T> (system, uid, initList: 'T list, doLog) = 
    let cluster = Cluster.Get system
    let ddata = DistributedData.Get system
    // some helper functions
    let (++) set e = ORSet.add cluster e set
    let toSet initSet = List.fold (++) initSet
    // initialize set
    let initSet = initList |> toSet ORSet.empty
    let key = ORSet.key uid 

    // ddata.AsyncUpdate(key, set, writeLocal)
    // |> Async.RunSynchronously

    // read data 
    async {
        let! reply = ddata.AsyncGet(key, readLocal)
        match reply with
        | Some value -> printfn "Data for key %A: %A" key value
        | None -> printfn "Data for key '%A' not found" key
    } |> Async.RunSynchronously

    // delete data 
    ddata.AsyncDelete(key, writeLocal) |> Async.RunSynchronously

    let updateState key (data: 'T) = ddata.AsyncUpdate(key, set, writeLocal)

    member __.Add value: Async<unit option> = updateState key value
        // async {
        // let! reply = (retype replicator) <? get readLocal key
        // match reply.Value with
        // | GetSuccess(k, (data : ORSet<'T>), _) -> return! updateState key (valueList |> toSet data)
        // | NotFound k -> return! updateState key (valueList |> toSet initSet)
        // | DataDeleted k -> printfn "Data for key '%A' has been deleted" k; return None
        // | GetFailure(k, _) -> printfn "Data for key '%A' didn't received in time" k; return None
    // } 

    member __.Read(): Async<IImmutableSet<'T> option> = async {
        let! reply = (retype replicator) <? get readLocal key
        match reply.Value with
        | GetSuccess(k, (data : ORSet<'T>), _) -> 
            let value = data |> ORSet.value
            // printfn "Data for key %A: %A" k value
            return (value |> Some)

        | NotFound k -> printfn "Data for key '%A' not found" k; return None
        | DataDeleted k -> printfn "Data for key '%A' has been deleted" k; return None
        | GetFailure(k, _) -> printfn "Data for key '%A' didn't received in time" k; return None
    } 

let broadcaster<'T> node uid = Broadcaster<'T>(node, uid, [], false)
