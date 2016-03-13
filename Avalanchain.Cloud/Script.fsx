(*** hide ***)
#load "ThespianCluster.fsx"
//#load "AzureCluster.fsx"

#r "../packages/Chessie.0.4.0/lib/net40/Chessie.dll"
#r "../packages/FSharpx.Collections.1.14.0/lib/net40/FSharpx.Collections.dll"
#r "../packages/FSharp.Quotations.Evaluator.1.0.7/lib/net40/FSharp.Quotations.Evaluator.dll"
#r "../packages/FsPickler.1.7.2/lib/net45/FsPickler.dll"
#r "../packages/FsPickler.Json.1.7.2/lib/net45/FsPickler.Json.dll"
#r "../packages/Base58Check.0.2.0/lib/Net40/Base58Check.dll"
#r "packages/FSharp.Control.AsyncSeq/lib/net45/FSharp.Control.AsyncSeq.dll"

#r "bin/Debug/Avalanchain.dll"


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

// Initialize client object to an MBrace cluster
let cluster = Config.GetCluster() 
//cluster.KillAllWorkers()

let send (queue: CloudQueue<'T>) data = queue.Enqueue data

let sendBatch (queue: CloudQueue<'T>) data = queue.EnqueueBatch data

let sendRandomBatch (queue: CloudQueue<string>) m n = 
    let words = [| for i in 0 .. n -> [| for i in 0 .. m -> (m * n) % 256 |> char |] |> (fun chars -> new String(chars)) |]
    sendBatch queue words

//type PersistedReplayable<'T>() =
//    let id = Guid.NewGuid().ToString()
//    let position = CloudAtom.New<uint64> (0UL, "pointer", id)
//    //member __.
//
let createPersistedReplayable<'T>() = cloud {
    let id = Guid.NewGuid().ToString()
    let! queue = CloudQueue.New<'T>()
    let! persistedFlow = 
        //(queue, 1) 
        //|> CloudFlow.OfCloudQueue
        [| "aaa"; "bbb"; "ccc"|]
        |> CloudFlow.OfArray
        |> CloudFlow.persist StorageLevel.MemoryAndDisk
    return queue, persistedFlow
}


let (queue, replayable) = createPersistedReplayable<string>() |> cluster.Run



cluster.ShowProcesses()


let dict = replayable.ToEnumerable().ToArray()

////////////////////////////////////////////


type Confirmation<'T> = {
    NodeId: string
    ValueId: ValueId
    Value: 'T
    Notifier: 'T -> unit
}
and ValueId = string

type ConfirmationResult<'T> =
    | InvalidConfirmation
    | ConfirmedSame
    | ConfirmedDifferent of 'T
    | NotConfirmedYet

type ConfirmationCounter<'T when 'T: equality> (policy: ExecutionPolicy, validator, policyChecker) =
    let mutable confirmations = []
    let mutable invalidConfirmations = []
    let mutable pendingConfirmations = []
    let mutable confirmedValue = None
    member __.Policy = policy
    member __.AddConfirmation (confirmation: Confirmation<'T>) = 
        if not <| validator confirmation then 
            invalidConfirmations <- confirmation :: invalidConfirmations
            InvalidConfirmation
        else
            confirmations <- confirmation :: confirmations
            match confirmedValue with
            | Some v -> 
                if confirmation.Value = v then ConfirmedSame
                else ConfirmedDifferent v
            | None ->
                confirmedValue <- policyChecker policy confirmations // TODO: Add possibility for reconfirmations
                match confirmedValue with
                | Some v -> 
                    for pc in pendingConfirmations do pc.Notifier v // Notifying pendings
                    if confirmation.Value = v then ConfirmedSame
                    else ConfirmedDifferent v
                | None -> 
                    pendingConfirmations <- confirmation :: pendingConfirmations
                    NotConfirmedYet
    member __.Confirmations with get() = confirmations
    member __.InvalidConfirmations with get() = invalidConfirmations
    member __.PendingConfirmations with get() = pendingConfirmations
            
  
let ofQueue (queue: CloudQueue<'T>) f = 
    asyncSeq { 
        let centroidsSoFar = ResizeArray()
        while true do
            match queue.TryDequeue() with
            | Some d ->                  
                    yield d
                    do! Async.Sleep 1
            | None -> do! Async.Sleep 1
    }
    |> AsyncSeq.map(f)   
            
type StreamFrame<'T> = {
    Nonce: uint64
    Value: 'T
}
        
type CloudStream<'T> = {
    Position: Async<int64>
    Item: uint64 -> Async<'T option>
    GetFrom: uint64 -> Async<'T seq>
    FlowProcess: Cloud<LocalCloud<CloudFlow<unit>>>
}

let enqueueFlow<'T> queue (emit: StreamFrame<'T> -> unit) = cloud { 
    let mutable i = 0UL
    let! streamId = CloudAtom.CreateRandomContainerName() // TODO: Replace with node/stream pubkey
    let! dict = CloudDictionary.New<StreamFrame<'T>>(streamId + "-enqueued")
    return! CloudFlow.OfCloudQueue(queue, 4)
            |> CloudFlow.map (fun v -> 
                                    let msg = { Nonce = i + 1UL; Value = v }
                                    i <- i + 1UL
                                    dict.ForceAdd(i.ToString(), msg)
                                    msg)
            |> CloudFlow.iter emit
            |> Cloud.CreateProcess
}

let createCloudStream<'T> (queue: CloudQueue<'T>) emit = 
    cloud { 
//        let! sync = CloudAtom.New<string>("")
//        sync.
        let! streamId = CloudAtom.CreateRandomContainerName() // TODO: Replace with node/stream pubkey
        let! dict = CloudDictionary.New<StreamFrame<'T>>(streamId + "-data")
        let position = 0UL
        let flow = local {
            let flow: CloudFlow<unit> = 
                (queue, 1) 
                |> CloudFlow.OfCloudQueue
                |> CloudFlow.map (fun v -> { Nonce = position; Value = v })
                |> CloudFlow.map (fun f -> 
                                    dict.ForceAdd(f.Nonce.ToString(), f)
                                    f)
                |> CloudFlow.map (emit)
            return flow
        } 
        let! currentWorker = Cloud.CurrentWorker
        let! flowProcess = Cloud.StartChild (flow, currentWorker)

        let ret = {
            Position = async { let! size = dict.GetCountAsync()
                               return size - 1L }
            Item = (fun nonce -> dict.TryFindAsync(nonce.ToString()))
            GetFrom = (fun nonce -> async { 
                                            let! enumerable = dict.GetEnumerableAsync()  // TODO: Check performance 
                                            return enumerable 
                                                    |> Seq.skip (nonce |> int)
                                                    |> Seq.map (fun kv -> kv.Value) })
            FlowProcess = flowProcess
        }

        return ret 
    }

let streamRef = createCloudStream queue (printfn "data - '%A'") |> cluster.CreateProcess
send queue "aaaaaa1"
streamRef.ShowInfo()

let createSingleStream<'T> (queue: CloudQueue<'T>) maxBatchSize emit = 
    cloud { 
        return! local {
            let! streamId = CloudAtom.CreateRandomContainerName() // TODO: Replace with node/stream pubkey
            let! data = CloudDictionary.New<string>(streamId + "data")

            while true do 
                let! msgs = Cloud.OfAsync <| queue.DequeueBatchAsync(maxBatchSize)
                let! batch = CloudValue.NewArray<'T> (msgs, StorageLevel.MemoryAndDisk)
                let! newPosition = Cloud.OfAsync <| data.GetCountAsync()
                do! Cloud.OfAsync <| data.ForceAddAsync (newPosition.ToString(), batch.Id)
                emit msgs
            return streamId 
        }
    }


////////////////////////////////////////////


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


    //persistedFlow.ToEnumerable
//                |> fun x -> x.
//                |> PersistedCloudFlow.
//                |> CloudFlow.toArray

//    let! desc = local {
//
//        //let parti
//
//        let! persistedFlow = flow |> CloudFlow.persist StorageLevel.MemoryAndDisk
//
//        //persistedFlow.
//
//    //    |> CloudFlow.map (fun n -> Sieve.getPrimes n)
//    //    |> CloudFlow.map (fun primes -> sprintf "calculated %d primes: %A" primes.Length primes)
//    //    |> CloudFlow.toArray
//    //    |> cluster.CreateProcess 
//
//    }
//    return desc

//    return 1
//}

//type 

(** Next, you start a cloud task to wait for the 100 messages: *)
let createSingleStream maxBatchSize emit = 
    cloud { 
        return! local {
            let! streamId = CloudAtom.CreateRandomContainerName() // TODO: Replace with node/stream pubkey
            //let! pointer = CloudAtom.GetById<uint64> ("pointer", streamId)
            let! pointer = CloudAtom.New<uint64> (0UL, "pointer", streamId)
            let! data = CloudDictionary.New<string>(streamId + "data")
            //let! outQueue = CloudQueue.New<string>()

            //let results = new ResizeArray<_>()
            while true do 
                let! msgs = Cloud.OfAsync <| queue.DequeueBatchAsync(maxBatchSize)
                let! batch = CloudValue.NewArray<string> (msgs, StorageLevel.MemoryAndDisk)
                data.ForceAdd (pointer.Value.ToString(), batch.Id)
                let! currentPointer = CloudAtom.Increment pointer 
                emit msgs
                //outQueue.EnqueueBatch msgs
                    //return writeToStream msg
                    //return msg
                //results.Add msg
            //return results.ToArray() 
            return streamId 
        }
    }
    //|> Cloud.ParallelEverywhere
    |> cluster.CreateProcess

let receiveTask = createSingleStream 10
receiveTask.ShowInfo() 

(** Next, you wait for the result of the receiving cloud task: *)
//receiveTask.Result.[0].[0]

(** 
## Using queues as inputs to reactive data parallel cloud flows

You now learn how to use cloud queues as inputs to a data parallel cloud flow.

*)

cluster.ShowProcesses()