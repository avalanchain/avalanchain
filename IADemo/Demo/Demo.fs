module Avalanchain.Demo

open System
open System.IO
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

open Avalanchain
open Avalanchain.Node
open Avalanchain.Node.Network
open Avalanchain.Chat

open Chains
open ChainDefs
open DData

let testPersistentStream node keyPair = 
    let transactions system pid = 
        [| for i in 0 .. 999 -> "str" + i.ToString() |] 
        |> Source.ofArray
        |> Source.map Offer
        //|> Source.toMat(persistSink 1000L) Keep.none
        |> Source.toMat(persistSink system pid 100L) Keep.none

    transactions node.System "p1" |> Graph.run node.Mat 

    let ids = (node.Journal.Value).CurrentPersistenceIds() 
                |> Source.runWith node.Mat (Sink.Seq()) 
                |> Async.AwaitTask 
                |> Async.RunSynchronously

    let events = 
        currentEventsSource keyPair node.System (ids.[0]) 0L 1000L

    ()


[<EntryPoint>]
let main argv =
    printfn "%A" argv

    printfn "Current dir %s" (Directory.GetCurrentDirectory())
    let cd = Path.Combine(__SOURCE_DIRECTORY__, "bin/Debug/net461")
    Directory.SetCurrentDirectory(cd)
    Directory.CreateDirectory("db") |> ignore

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
    let endpoint2 = { IP = "127.0.0.1"; Port = 5001us }
    // let endpoint3 = { IP = "127.0.0.1"; Port = 5002us }

    let node1 = setupNode "ac1" endpoint1 [endpoint1] (OverflowStrategy.DropNew) 1000 // None
    Threading.Thread.Sleep 1000
    //let ddata = DistributedData.Get node1.System
    //ddata.Replicator <! Subscribe 

    let node2 = setupNode "ac2" endpoint2 [endpoint1] (OverflowStrategy.DropNew) 1000 // None
    Threading.Thread.Sleep 1000

    //testPersistentStream node keyPair

    let payments = PaymentAccounts(node1.System)

    payments.AddAccount { Ref = { Address = "Addr3" }; Name = "Acc3"; PubKey = PublicKey "PUBKEY3" } |> Async.RunSynchronously
    payments.AddAccount { Ref = { Address = "Addr4" }; Name = "Acc4"; PubKey = PublicKey "PUBKEY4" } |> Async.RunSynchronously
    payments.AddAccount { Ref = { Address = "Addr5" }; Name = "Acc5"; PubKey = PublicKey "PUBKEY5" } |> Async.RunSynchronously
    let accounts = payments.Accounts() |> Async.RunSynchronously |> Seq.toArray
    printfn "Accounts: %A" accounts
    let acc = accounts |> Seq.head
    payments.PostTransaction { From = acc.Ref; To = [| { Ref = accounts.[1].Ref; Amount = 100M }; { Ref = accounts.[2].Ref; Amount = 25M } |]; Dt = DateTimeOffset.Now } |> Async.RunSynchronously
    let balances = payments.Balances() |> Async.RunSynchronously
    printfn "Balances: %A" balances
    for b in balances do printfn "Bal %s %A" b.Key.Address (payments.AddressBalance b.Key.Address |> Async.RunSynchronously)

    //testPersistentStream node keyPair

    let payments = PaymentAccounts(node2.System)


//    let chat = ChatAccounts(node1.System)
//    let msgs = chat.ChannelMessages "Chan1" |> Async.RunSynchronously
//    printfn "Messages: %A" msgs
//    let accounts = chat.Accounts() |> Async.RunSynchronously
//    printfn "Accounts: %A" accounts
//    chat.AddAccount { Address = "Addr3"; Name = "Acc3"; PubKey = PublicKey "PUBKEY3" } |> Async.RunSynchronously
//    let accounts = chat.Accounts() |> Async.RunSynchronously
//    printfn "Accounts: %A" accounts
//    let acc = accounts |> Seq.head
//    chat.PostMessage "Chan1" acc.Address "Kuku" |> Async.RunSynchronously
//    let msgs = chat.ChannelMessages "Chan1" |> Async.RunSynchronously
//    printfn "Messages: %A" msgs

//    seq { for i in 1 .. 10000 -> "Kuku " + i.ToString() }
//    |> chat.PostMessages "Chan1" acc.Address 
//    |> Async.RunSynchronously

//    let trans = transactionsSet<string> node1.System

//    trans.Add [] |> Async.RunSynchronously
//    let res = trans.Get() |> Async.RunSynchronously |> ORSet.value

//    printfn "Res0: %A" res
//    printfn "Res0 Count: %A" res.Count

//    trans.Add [for i in 140000 .. 200000 -> i.ToString()]
//    |> Async.RunSynchronously

//    let res = trans.Get() |> Async.RunSynchronously

//    printfn "Res: %A" (res |> Seq.sort)
//    printfn "Res Count: %A" res.Count

    Console.ReadKey() |> ignore

    let accounts = payments.Accounts() |> Async.RunSynchronously |> Seq.toArray
    printfn "Accounts: %A" accounts
    payments.AddAccount { Ref = { Address = "Addr7" }; Name = "Acc7"; PubKey = PublicKey "PUBKEY7" } |> Async.RunSynchronously
    payments.AddAccount { Ref = { Address = "Addr8" }; Name = "Acc8"; PubKey = PublicKey "PUBKEY8" } |> Async.RunSynchronously
    let accounts = payments.Accounts() |> Async.RunSynchronously |> Seq.toArray
    printfn "Accounts: %A" accounts
    let balances = payments.Balances() |> Async.RunSynchronously
    printfn "Balances: %A" balances
    for b in balances do printfn "Bal %s %A" b.Key.Address (payments.AddressBalance b.Key.Address |> Async.RunSynchronously)
    payments.PostTransaction { From = acc.Ref; To = [| { Ref = accounts.[1].Ref; Amount = 150M } |]; Dt = DateTimeOffset.Now } |> Async.RunSynchronously
    let batch = [| for i in 1 .. 1000 ->
                    { From = acc.Ref; To = [| { Ref = accounts.[1].Ref; Amount = 150M } |]; Dt = DateTimeOffset.Now } |]
    for i in 1 .. 10 do
        printfn "Batch %i starts" i
        batch |> payments.PostTransactions |> Async.RunSynchronously
        printfn "Batch %i finished" i
    let transactions = payments.Transactions() |> Async.RunSynchronously
    printfn "Transactions: %A" transactions
    let balances = payments.Balances() |> Async.RunSynchronously
    printfn "Balances: %A" balances


    Console.ReadKey() |> ignore

    //node1.System.Terminate().Wait()
    0


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


    // return an integer exit code
