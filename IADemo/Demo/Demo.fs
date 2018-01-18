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
                |> Source.runForEach (node.Mat) (printfn "Received AC Token: %A")
                |> Async.Start

    printfn "Press any key when ready, 'C' for exit"
    
    let mutable iteration = 0L
    while ['C'; 'c'] |> List.contains (Console.ReadKey() |> fun c -> c.KeyChar) |> not do
        let transactions system pid = 
            [| for i in 1L .. 1000L -> iteration * 1000L + i |] 
            |> Source.ofArray
            |> Source.map (fun i -> sprintf """{ "ccyPair": "%s", "rate": "%.4f", "node": "%s_%d", "dt": "%s" }""" "USDAUD" (0.8 + (rnd.NextDouble() - 0.5)/100.) nid endpoint.Port (DateTimeOffset.Now.ToString()) |> toChainToken keyPair i |> Offer)
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

    0