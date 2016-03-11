(*** hide ***)
#load "ThespianCluster.fsx"
//#load "AzureCluster.fsx"

#r "../packages/Chessie.0.4.0/lib/net40/Chessie.dll"
#r "../packages/FSharpx.Collections.1.14.0/lib/net40/FSharpx.Collections.dll"
#r "../packages/FSharp.Quotations.Evaluator.1.0.7/lib/net40/FSharp.Quotations.Evaluator.dll"
#r "../packages/FsPickler.1.7.2/lib/net45/FsPickler.dll"
#r "../packages/FsPickler.Json.1.7.2/lib/net45/FsPickler.Json.dll"
#r "../packages/Base58Check.0.2.0/lib/Net40/Base58Check.dll"

#r "bin\Debug\Avalanchain.dll"


// Note: Before running, choose your cluster version at the top of this script.
// If necessary, edit AzureCluster.fsx to enter your connection strings.

open System
open System.IO
open MBrace.Core
open MBrace.Flow
open MBrace.Library
open Avalanchain.NodeContext
open Chessie.ErrorHandling
open Avalanchain.Quorum
open Avalanchain.EventStream

// Initialize client object to an MBrace cluster
let cluster = Config.GetCluster() 
//cluster.KillAllWorkers()

let writeToStream data =
    let ct = Avalanchain.Utils.cryptoContext
    let nodeStore = NodeStore ct
    let node = nodeStore.GetNode<string, string> ("/", [])
    let stream = node.CreateStream "s1" 0u <@ fun s d -> ok (s + s) @> "" ExecutionPolicy.Pass
    let res = 
        match stream with
        | Ok (s, _) -> 
            try 
                let ret = node.Push s.Ref data
                printfn "%A" ret
                ret
            with
                | e -> 
                    printfn "%A" e
                    fail (ProcessingFailure(e.Message :: [e.StackTrace]))
        | Bad(_) -> failwith "Not implemented yet"
    res



let queue = CloudQueue.New<string>() |> cluster.Run

(** Next, you start a cloud task to send 100 messages to the queue: *)
let sendTask = 
    cloud { for i in [ 0 .. 100000 ] do 
                queue.Enqueue (sprintf "hello%d" i) }
    |> cluster.CreateProcess

sendTask.ShowInfo() 


(** Next, you start a cloud task to wait for the 100 messages: *)
let receiveTask = 
    cloud { 
        return! local {
            let! streamId = CloudAtom.CreateRandomContainerName() // TODO: Replace with node/stream pubkey
            let! pointer = CloudAtom.GetById<uint64> ("pointer", streamId)
            let results = new ResizeArray<_>()
            for i in [ 0 .. 24999 ] do 
                let msg = queue.Dequeue()
                    //return writeToStream msg
                    //return msg
                results.Add msg
            return results.ToArray() 
        }
    }
    //|> Cloud.ParallelEverywhere
    |> cluster.CreateProcess

receiveTask.ShowInfo() 

(** Next, you wait for the result of the receiving cloud task: *)
//receiveTask.Result.[0].[0]

(** 
## Using queues as inputs to reactive data parallel cloud flows

You now learn how to use cloud queues as inputs to a data parallel cloud flow.

*)

cluster.ShowProcesses()