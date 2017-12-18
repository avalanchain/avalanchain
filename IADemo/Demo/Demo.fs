module Avalanchain.Demo

open System
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

open Akka.Cluster
open Akka.DistributedData
open Akkling
open Akkling.DistributedData
open Akkling.DistributedData.Consistency

open Avalanchain.Node
open Avalanchain.Node.Network

open Chains
open ChainDefs
open DData

[<EntryPoint>]
let main argv =
    printfn "%A" argv

    let keyStorage = fun kid -> { PublicKey = PublicKey "pubKey"; PrivateKey = PrivateKey "privKey"; Kid = kid }

    let keyPair = keyStorage 0us

    let newChainDef() = {
        algo = Asym(ES384)
        uid = Guid.NewGuid()
        chainType = ChainType.New
        encryption = Encryption.None
        compression = Compression.None
    }

    let chainDefs = [ for _ in 0 .. 19 -> newChainDef() |> toChainDefToken keyPair ]

    // let cdToken = chainDef |> toChainDefToken keyPair 
    // cdToken.Payload

    // let cdTokenDerived = chain chainDef2

    let endpoint1 = { IP = "127.0.0.1"; Port = 5000us }
    // let endpoint2 = { IP = "127.0.0.1"; Port = 5001us }
    // let endpoint3 = { IP = "127.0.0.1"; Port = 5002us }

    let node1 = setupNode "ac1" endpoint1 [endpoint1] (OverflowStrategy.DropNew) 1000 // None
    Threading.Thread.Sleep 1000

    let transactions = 
        [| for i in 0 .. 999 -> "str" + i.ToString() |] 
        |> Source.ofArray
        |> Source.map (Offer >> Command)
        |> Source.toMat(persistSink 1000L) Keep.none

    let ret = transactions |> Graph.run node1.Mat 

    let ids = (readJournal node1.System).CurrentPersistenceIds() |> Source.runWith node1.Mat (Sink.Seq()) |> Async.AwaitTask |> Async.RunSynchronously

    let events = 
        currentEventsSource keyPair node1.System (ids.[0]) 0L 100L

    printfn "Events: %A" events

    //let trs = transactions<string> node1.System
    //trs.Clear() |> Async.RunSynchronously
    //[ for i in 0 .. 999 -> "str" + i.ToString() ] 
    //|> trs.Add 
    //|> Async.RunSynchronously
    //let trsI1 = trs.Get() |> Async.RunSynchronously
    //printfn "trs1 %A"


    //let node2 = setupNode endpoint2 [endpoint2]
    //Threading.Thread.Sleep 2000

    //let node3 = setupNode endpoint3 [endpoint1; endpoint2]



    //let source = distPubSub<string> node1 topic OverflowStrategy.DropNew 1000000

    //source
    //|> Source.runForEach mat (printfn "Piu: %A")
    //|> Async.Start

    //let mediator2 = DistributedPubSub.Get(node2).Mediator
    //mediator2.Tell(Publish(topic, { Message = "msg 2" }))


    //let mediator3 = DistributedPubSub.Get(node3).Mediator
    //mediator3.Tell(Publish(topic, { Message = "msg 3" } ))

    //let source3 = distPubSub<string> node3 topic OverflowStrategy.DropNew 1000000

    //source3
    //|> Source.runForEach mat (printfn "Piu: %A")
    //|> Async.Start


    0 // return an integer exit code
