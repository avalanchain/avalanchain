(*** hide ***)
#load "ThespianCluster.fsx"
//#load "AzureCluster.fsx"

#r "../packages/Chessie.0.4.0/lib/net40/Chessie.dll"
#r "../packages/FSharpx.Collections.1.14.0/lib/net40/FSharpx.Collections.dll"
#r "../packages/FSharp.Quotations.Evaluator.1.0.7/lib/net40/FSharp.Quotations.Evaluator.dll"
#r "../packages/FsPickler.1.7.2/lib/net45/FsPickler.dll"
#r "../packages/FsPickler.Json.1.7.2/lib/net45/FsPickler.Json.dll"
#r "../packages/Base58Check.0.2.0/lib/Net40/Base58Check.dll"
#r "../packages/Streams.0.4.1/lib/net45/Streams.dll"
#r "packages/FSharp.Control.AsyncSeq/lib/net45/FSharp.Control.AsyncSeq.dll"

#r "bin/Debug/Avalanchain.dll"

#load "CloudStream.fs"
#load "ChainFlow.fs"

// Note: Before running, choose your cluster version at the top of this script.
// If necessary, edit AzureCluster.fsx to enter your connection strings.

open System
open System.Linq
open System.IO
open MBrace.Core
open MBrace.Flow
open MBrace.Library
open Avalanchain.NodeContext
open Chessie.ErrorHandling
open Avalanchain.Quorum
open Avalanchain.EventStream
open FSharp.Control
open Nessos.Streams
open MBrace.Runtime
open Avalanchain.Cloud

// Initialize client object to an MBrace cluster
let cluster = Config.GetCluster() 
//cluster.KillAllWorkers()

let send (queue: CloudQueue<'T>) data = queue.Enqueue data

let sendBatch (queue: CloudQueue<'T>) data = queue.EnqueueBatch data

let sendRandomBatch (queue: CloudQueue<string>) m n = 
    let words = [| for i in 0 .. n -> [| for i in 0 .. m -> (m * n) % 256 |> char |] |> (fun chars -> new String(chars)) |]
    sendBatch queue words


//type Confirmation<'T> = {
//    NodeId: string
//    ValueId: ValueId
//    Value: 'T
//    Notifier: 'T -> unit
//}
//and ValueId = string
//
//type ConfirmationResult<'T> =
//    | InvalidConfirmation
//    | ConfirmedSame
//    | ConfirmedDifferent of 'T
//    | NotConfirmedYet
//
//type ConfirmationCounter<'T when 'T: equality> (policy: ExecutionPolicy, validator, policyChecker) =
//    let mutable confirmations = []
//    let mutable invalidConfirmations = []
//    let mutable pendingConfirmations = []
//    let mutable confirmedValue = None
//    member __.Policy = policy
//    member __.AddConfirmation (confirmation: Confirmation<'T>) = 
//        if not <| validator confirmation then 
//            invalidConfirmations <- confirmation :: invalidConfirmations
//            InvalidConfirmation
//        else
//            confirmations <- confirmation :: confirmations
//            match confirmedValue with
//            | Some v -> 
//                if confirmation.Value = v then ConfirmedSame
//                else ConfirmedDifferent v
//            | None ->
//                confirmedValue <- policyChecker policy confirmations // TODO: Add possibility for reconfirmations
//                match confirmedValue with
//                | Some v -> 
//                    for pc in pendingConfirmations do pc.Notifier v // Notifying pendings
//                    if confirmation.Value = v then ConfirmedSame
//                    else ConfirmedDifferent v
//                | None -> 
//                    pendingConfirmations <- confirmation :: pendingConfirmations
//                    NotConfirmedYet
//    member __.Confirmations with get() = confirmations
//    member __.InvalidConfirmations with get() = invalidConfirmations
//    member __.PendingConfirmations with get() = pendingConfirmations
//            
//  
//let ofQueue (queue: CloudQueue<'T>) f = 
//    asyncSeq { 
//        let centroidsSoFar = ResizeArray()
//        while true do
//            match queue.TryDequeue() with
//            | Some d ->                  
//                    yield d
//                    do! Async.Sleep 1
//            | None -> do! Async.Sleep 1
//    }
//    |> AsyncSeq.map(f)   
            







let sink, topChain = ChainFlow.ofSink<string> 10000u |> cluster.Run

let topChainPos = topChain.Position() |> cluster.Run

let topChainCurrent = topChain.Current() |> cluster.Run
let topChainAll = topChain.GetFramesPage 0UL 1000000u |> cluster.Run
topChainAll.Length
              

let chain = ChainFlow.ofStream topChain
            |> ChainFlow.mapFrame 1000u (fun v -> v.Nonce )
            |> ChainFlow.filter 1000u (fun v -> v.ToString() |> Int32.Parse |> fun ch -> ch % 2 = 0 )
            |> ChainFlow.filterFrame 1000u (fun v -> v.Nonce % 2UL = 0UL )
            |> ChainFlow.mapFrame 1000u (fun v -> v.Nonce )
            |> cluster.Run

let chainPos = chain.Position() |> cluster.Run

let chainCurrent = chain.Current() |> cluster.Run

//let chainAll = chain.GetFramesPage 0UL 1000000u |> cluster.Run


sink.PushBatch [|for i in 0UL .. 99999UL do yield "item" + i.ToString()|] |> cluster.Run


let sum = chain 
            |> ChainFlow.ofStream
            |> ChainFlow.sum 1000u 
            |> cluster.Run

let sumPos = chain.Position() |> cluster.Run
let sumCurrent = chain.Current() |> cluster.Run

let sumEverywhere = chain 
                    |> ChainFlow.ofStream
                    |> ChainFlow.sumEverywhere 1000u 
                    |> cluster.Run

let sumEvrPos = [| for node in sumEverywhere -> node.Position() |> cluster.Run |]
let sumEvrCurrent = [| for node in sumEverywhere -> node.Current() |> cluster.Run |]














let str = [|for i in 0UL .. 99999UL do yield "item" + i.ToString()|]
            |> ChainFlow.ofArray 10000u


















