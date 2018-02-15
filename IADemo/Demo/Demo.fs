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
open DistPubSub
open Avalanchain.Chat

open Chains
open ChainDefs
open DData
open System.Threading

let testPersistentStream node endpoint keyPair = 
    let topic = "acTokens"
    let nid = Guid.NewGuid().ToString("N")
    let rnd = Random()
    let source = distPubSubSource<PersistCommand<JwtToken<string>>> node.System topic OverflowStrategy.DropNew 1000000

    let printer = source
                |> Source.alsoToMat (persistSink<JwtToken<string>> node.System "processed" 500L) Keep.none
                //|> Source.runForEach (node.Mat) (printfn "Received AC Token: %A")
                |> Source.runForEach (node.Mat) ignore
                |> Async.Start

    printfn "Press any key when ready, 'C' for exit"
    
    let mutable iteration = 0L
    while ['C'; 'c'] |> List.contains (Console.ReadKey() |> fun c -> c.KeyChar) |> not do
        let transactions system pid = 
            [| for i in 1L .. 1000L -> iteration * 1000L + i |] 
            |> Source.ofArray
            |> Source.map (fun i -> sprintf """{ "ccyPair": "%s", "rate": "%.4f", "node": "%s_%d", "dt": "%s" }""" "USDAUD" (0.8 + (rnd.NextDouble() - 0.5)/100.) nid endpoint.Port (DateTimeOffset.Now.ToString()) |> toChainItemToken keyPair i |> Offer)
            |> Source.alsoTo (persistSink system pid 500L)
            |> Source.toMat(distPubSubSink node.System topic (Offer "complete")) Keep.none

        transactions node.System "orders" |> Graph.run node.Mat 
        iteration <- iteration + 1L

    printer


[<EntryPoint>]
let main argv =
    printfn "%A" argv

    printfn "Current dir %s" (Directory.GetCurrentDirectory())
    let cd = Path.Combine(__SOURCE_DIRECTORY__, "bin/Debug/net461")
    Directory.SetCurrentDirectory(cd)

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
    printfn "ChainDefs: %A" chainDefs

    let endpoint1 = { IP = "127.0.0.1"; Port = 5000us }
    let endpoint2 = { IP = "127.0.0.1"; Port = 0us }

    let isMaster = argv.Length = 0

    let endpoint =  if isMaster then endpoint1 
                    else
                        let (parsed, port) = argv.[0] |> UInt16.TryParse 
                        { endpoint2 with Port = if parsed then port else (new Random()).Next(6000, 7000) |> uint16 }

    let node = setupNode "ac" endpoint [endpoint1] (OverflowStrategy.DropNew) 1000 // None
    Threading.Thread.Sleep 3000

    let pr = testPersistentStream node endpoint keyPair 
    
    Console.ReadKey() |> ignore

    
    let ddTest() =
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

        let payments1 = PaymentAccounts(node1.System)

        payments1.AddAccount { Ref = { Address = "Addr3" }; Name = "Acc3"; PubKey = PublicKey "PUBKEY3" } |> Async.RunSynchronously
        payments1.AddAccount { Ref = { Address = "Addr4" }; Name = "Acc4"; PubKey = PublicKey "PUBKEY4" } |> Async.RunSynchronously
        payments1.AddAccount { Ref = { Address = "Addr5" }; Name = "Acc5"; PubKey = PublicKey "PUBKEY5" } |> Async.RunSynchronously
        let accounts = payments1.Accounts() |> Async.RunSynchronously |> Seq.toArray
        printfn "Accounts: %A" accounts
        let acc = accounts |> Seq.head
        payments1.PostTransaction { From = acc.Ref; To = [| { Ref = accounts.[1].Ref; Amount = 100M }; { Ref = accounts.[2].Ref; Amount = 25M } |]; Dt = DateTimeOffset.Now } |> Async.RunSynchronously
        let balances = payments1.Balances() |> Async.RunSynchronously
        printfn "Balances: %A" balances
        for b in balances do printfn "Bal %s %A" b.Key.Address (payments1.AddressBalance b.Key.Address |> Async.RunSynchronously)

        //testPersistentStream node keyPair

        let payments2 = PaymentAccounts(node2.System)


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

        let accounts = payments2.Accounts() |> Async.RunSynchronously |> Seq.toArray
        printfn "Accounts: %A" accounts
        payments2.AddAccount { Ref = { Address = "Addr7" }; Name = "Acc7"; PubKey = PublicKey "PUBKEY7" } |> Async.RunSynchronously
        payments2.AddAccount { Ref = { Address = "Addr8" }; Name = "Acc8"; PubKey = PublicKey "PUBKEY8" } |> Async.RunSynchronously
        let accounts = payments2.Accounts() |> Async.RunSynchronously |> Seq.toArray
        printfn "Accounts: %A" accounts
        let balances = payments2.Balances() |> Async.RunSynchronously
        printfn "Balances: %A" balances
        for b in balances do printfn "Bal %s %A" b.Key.Address (payments2.AddressBalance b.Key.Address |> Async.RunSynchronously)
        payments2.PostTransaction { From = acc.Ref; To = [| { Ref = accounts.[1].Ref; Amount = 150M } |]; Dt = DateTimeOffset.Now } |> Async.RunSynchronously
        let batch = [| for i in 1 .. 1000 ->
                        { From = acc.Ref; To = [| { Ref = accounts.[1].Ref; Amount = 150M } |]; Dt = DateTimeOffset.Now } |]
        for i in 1 .. 10 do
            printfn "Batch %i starts" i
            batch |> payments2.PostTransactions |> Async.RunSynchronously
            printfn "Batch %i finished" i
        let transactions = payments2.Transactions() |> Async.RunSynchronously
        printfn "Transactions: %A" transactions
        let balances = payments2.Balances() |> Async.RunSynchronously
        printfn "Balances: %A" balances


        Console.ReadKey() |> ignore

        //node1.System.Terminate().Wait()
        0

    0