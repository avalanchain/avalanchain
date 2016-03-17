(*** hide ***)
#load "ThespianCluster.fsx"
//#load "AzureCluster.fsx"

#r "../packages/Chessie.0.4.0/lib/net40/Chessie.dll"
#r "../packages/FSharpx.Collections.1.14.0/lib/net40/FSharpx.Collections.dll"
#r "../packages/FSharp.Quotations.Evaluator.1.0.7/lib/net40/FSharp.Quotations.Evaluator.dll"
#r "../packages/FsPickler.1.7.2/lib/net45/FsPickler.dll"
#r "../packages/FsPickler.Json.1.7.2/lib/net45/FsPickler.Json.dll"
#r "../packages/Base58Check.0.2.0/lib/Net40/Base58Check.dll"
#r "../packages/Streams.0.4.0/lib/net45/Streams.dll"
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
open Nessos.Streams

// Initialize client object to an MBrace cluster
let cluster = Config.GetCluster() 
//cluster.KillAllWorkers()

let send (queue: CloudQueue<'T>) data = queue.Enqueue data

let sendBatch (queue: CloudQueue<'T>) data = queue.EnqueueBatch data

let sendRandomBatch (queue: CloudQueue<string>) m n = 
    let words = [| for i in 0 .. n -> [| for i in 0 .. m -> (m * n) % 256 |> char |] |> (fun chars -> new String(chars)) |]
    sendBatch queue words


////type PersistedReplayable<'T>() =
////    let id = Guid.NewGuid().ToString()
////    let position = CloudAtom.New<uint64> (0UL, "pointer", id)
////    //member __.
////
//let createPersistedReplayable<'T>() = cloud {
//    let id = Guid.NewGuid().ToString()
//    let! queue = CloudQueue.New<'T>()
//    let! persistedFlow = 
//        //(queue, 1) 
//        //|> CloudFlow.OfCloudQueue
//        [| "aaa"; "bbb"; "ccc"|]
//        |> CloudFlow.OfArray
//        |> CloudFlow.persist StorageLevel.MemoryAndDisk
//    return queue, persistedFlow
//}
//
//
//let (queue1, replayable) = createPersistedReplayable<string>() |> cluster.Run
//
//
//
//cluster.ShowProcesses()
//
//
//let dict = replayable.ToEnumerable().ToArray()

////////////////////////////////////////////


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
            
type StreamFrame<'T> = {
    Nonce: uint64
    Value: 'T
}
        
type CloudStream<'T> = {
    Id: string
    Position: unit -> Cloud<int64>
    Item: uint64 -> Cloud<'T option>
    GetPage: uint64 -> uint32 -> Cloud<'T[]>
    GetFramesPage: uint64 -> uint32 -> Cloud<StreamFrame<'T>[]>
    FlowProcess: ICloudProcess<unit>
}

module ChunkedCloudStream =
    type State<'T> = {
//        States: CloudValue<State<'T>>[]
//        StateSize: uint64
        ChunkSize: uint64
        Chunks: CloudValue<'T[]>[]
        //Chunks: 'T[][]
        Tail: 'T[]
        Last: 'T
    }
    with 
        member inline private this.ChunkedSize = this.ChunkSize * uint64(this.Chunks.LongLength)
        member inline this.Size = this.ChunkedSize + uint64(this.Tail.LongLength)

        member this.GetPage nonce pageSize : Cloud<'T[]> = cloud {
            return! local {
                return
                    if nonce >= this.Size then [||]
                    else 
                        let size = Math.Min(pageSize, (this.Size - nonce))
                        let fromTailStart = Math.Max(0L, (int64(nonce) - int64(this.ChunkedSize))) |> uint64
                        let fromTailEnd = Math.Max(0L, (int64(nonce + size) - int64(this.ChunkedSize))) |> uint64
                        let startChunk = nonce / this.ChunkSize |> int32
                        let endChunk = Math.Min((nonce + size) / this.ChunkSize, uint64(this.Chunks.LongLength) - 1UL) |> int32

                        let chunks = [|for i in startChunk .. endChunk -> this.Chunks.[i |> int].GetValueAsync()|] 
                                        |> Async.Parallel 
                                        |> Async.RunSynchronously

                        [| 
                            for i in 0 .. 1 .. (endChunk - startChunk) do 
                                let chunkStart = (if nonce > (uint64(i + startChunk) * this.ChunkSize) then nonce - (uint64(i + startChunk) * this.ChunkSize) else 0UL) |> int32
                                let chunkEnd = if (nonce + size) < (uint64(i + startChunk + 1) * this.ChunkSize) 
                                                then (nonce + size - (uint64(i + startChunk) * this.ChunkSize) |> int32)
                                                else (this.ChunkSize |> int32)
                                for j in chunkStart .. 1 .. chunkEnd - 1 do yield (chunks.[i].[j |> int])
                        
                            if fromTailEnd > fromTailStart then
                                for i in fromTailStart .. fromTailEnd - 1UL do 
                                    yield this.Tail.[i |> int]
                        |]
            }
        }
        member inline this.Item with get(i: uint64) : Cloud<'T option> = 
                                        cloud { 
                                            let! arr = this.GetPage i 1UL
                                            return if arr |> Array.isEmpty then None else Some arr.[0]
                                        }

        static member Create (chunkSize: uint32) (data: 'T[]) = cloud {
            return! local {
                let cs = int(chunkSize)
                let chunkCount = (data.Length / cs)
                let chunkedSize = chunkCount * cs
                let chunks = Array.zeroCreate chunkCount
                for i in 0 .. 1 .. chunkCount - 1 do
                    let! chunk = CloudValue.New([| for j in 0 .. 1 .. cs - 1 do yield data.[i * cs + j] |]) 
                    chunks.[i] <- chunk
                
                return {
                    ChunkSize = chunkSize |> uint64
                    Chunks = chunks
                    Tail = [| for i in chunkCount * cs .. 1 .. data.Length - 1 do yield data.[i] |]
                    Last = if data |> Array.isEmpty then Unchecked.defaultof<'T> else (data |> Array.last) 
                }
            }
        }

        member this.Add (data: 'T) : Cloud<State<'T>> = cloud {
            return! local {
                if (uint64(this.Tail.Length) >= this.ChunkSize) then failwith (sprintf "Corrupted state! Tail length (%d) is greater than chunk size (%d)." this.Tail.Length this.ChunkSize)
                    

                let newTail = (Array.append this.Tail [| data |])
                
                if uint64(newTail.Length) = this.ChunkSize then
                    let! chunk = CloudValue.New(newTail, StorageLevel.MemoryAndDisk)
                    return {
                        ChunkSize = this.ChunkSize
                        Chunks = (Array.append this.Chunks [| chunk |])
                        Tail = [||]
                        Last = data
                    }
                else
                    return {
                        ChunkSize = this.ChunkSize
                        Chunks = this.Chunks
                        Tail = newTail
                        Last = data
                    }
            }
        }

        member this.AddRange (data: 'T[]) : Cloud<State<'T>> = cloud {
            let rec adder (state: State<'T>) remaining = local {
                match remaining with 
                | [||] -> 
                    return state 
                | x -> 
                    let (h, t) = x |> Array.splitAt 1 
                    let! st = (state.Add h.[0]) |> Cloud.AsLocal
                    return! adder st t
            }
            return! adder this data

        }
//
//        member this.AddRangeF (data: ('T -> 'T)[]) : Cloud<State<'T>> = cloud {
//            let rec adder (state: State<'T>) (remaining: ('T -> 'T)[]) = local {
//                match remaining with 
//                | [||] -> 
//                    return state 
//                | x -> 
//                    let (h, t) = x |> Array.splitAt 1 
//                    let! st = (state.Add (state.Last |> (h.[0]))) |> Cloud.AsLocal
//                    return! adder st t
//            }
//            return! adder this data
//
//        }

let cc = ChunkedCloudStream.State<string>.Create 10ul [||] |> cluster.RunLocally

let a = cloud { return! cc.GetPage 10UL 100000UL } |> cluster.RunLocally


//let st = [| for i in 0 .. 10000 do yield i |]
//            |> ChunkedCloudStream.State.Create 500
//            |> cluster.Run
//            
//let a = st.GetFrom 0UL 10000001UL |> cluster.Run
//let al = a.Length
//let notseq = Array.zip (a |> Array.take (a.Length - 1)) (a |> Array.skip 1) 
//                |> Array.filter (fun (a, b) -> a + 1 <> b)
//
//let aa = st.Add 123212 |> cluster.Run
//
//let aa1 = aa.AddRange [| for i in 0 .. 10000 do yield i + 200000 |] |> cluster.Run
//
//let aa2 = aa.Add 123212 |> cluster.Run
//
//aa1.Size
//
//[|0; 1; 2|] |> Array.splitAt 1

//    type ChunkedCloudStream<'T> (chunkSize: uint64) = 
//        let! position = CloudAtom.New<int64>(-1L, "position", streamId)


let enqueueStream<'T> (getter: (unit -> LocalCloud<StreamFrame<'T>>) -> LocalCloud<StreamFrame<'T>[]>) (initialValue: 'T option) maxBatchSize = 
    cloud { 
        let! streamId = CloudAtom.CreateRandomContainerName() // TODO: Replace with node/stream pubkey
        let! initialState = ChunkedCloudStream.State.Create maxBatchSize (match initialValue with Some v -> [| { Nonce = 0UL; Value = v } |] | None -> [||])
        let! stateAtom = CloudAtom.New<ChunkedCloudStream.State<StreamFrame<'T>>>(initialState, "state", streamId)
        //let! positionAtom = CloudAtom.New<int64>(-1L, "position", streamId)
        //let positionGetter () = cloud { return! positionAtom.GetValueAsync() |> Cloud.OfAsync }
        let positionGetter () = cloud { let! state = stateAtom.GetValueAsync() |> Cloud.OfAsync
                                        return (state.Size |> int64) - 1L}
        let lastGetter () = local { let! state = stateAtom.GetValueAsync() |> Cloud.OfAsync
                                    return state.Last }
        let! flowProcess = 
            let rec loop () = local { 
                try 
                    let! msgs = getter lastGetter //|> Cloud.AsLocal
                    //let msgs = [| {Nonce = 0UL; Value = Unchecked.defaultof<'T>} |]
                    if msgs.Length > 0 then
                        let! currentState = stateAtom.GetValueAsync() |> Cloud.OfAsync // TODO: Rethink possible race
                             //msgs |> Array.mapi (fun i v -> { Nonce = uint64(int64(i) + pos); Value = v })
                        let! newState = currentState.AddRange msgs |> Cloud.AsLocal
                        do! stateAtom.ForceAsync(newState) |> Cloud.OfAsync  // The order of State and Position updates is important!
    //                    do! positionAtom.ForceAsync(int64(newState.Size) - 1L) |> Cloud.OfAsync

                        do! Cloud.Sleep 1 // Required in order not to block downstreams
                        return! loop ()
                    else 
                        do! Cloud.Sleep 100
                        return! loop ()
                with 
                    //| e -> Environment.Exit(-1) //Cloud.Logf "%A" e
                    | e -> printfn "Exception!!! %A" e 
            } 
            loop () |> Cloud.CreateProcess

        let getFramesPage = (fun nonce page -> cloud { 
                                    let! v = stateAtom.GetValueAsync() |> Cloud.OfAsync
                                    return! v.GetPage nonce (uint64(page)) })
        return {
            Id = streamId
            Position = positionGetter
            Item = (fun nonce -> cloud { 
                                    let! pos = positionGetter()
                                    if int64(nonce) <= pos then 
                                        let! v = stateAtom.GetValueAsync() |> Cloud.OfAsync
                                        let! vv = (v.[nonce])
                                        return vv |> Option.bind (fun vvv -> Some vvv.Value) 
                                    else return None })
            GetPage = (fun nonce page -> cloud { 
                                    return! local {
                                        let! v = getFramesPage nonce page |> Cloud.AsLocal
                                        return v |> Array.map (fun kv -> kv.Value) }})
            GetFramesPage = getFramesPage
            FlowProcess = flowProcess
        }
    }

let streamOfQueue<'T> (queue: CloudQueue<'T>) initialValue maxBatchSize = cloud {
        let getter getLast = local {let! last = getLast()
                                    let! newValue = queue.DequeueBatchAsync(int(maxBatchSize)) |> Cloud.OfAsync
                                    return newValue |> Array.mapi (fun i v -> { Nonce = last.Nonce + uint64(i); Value = v }) }
        return! enqueueStream getter initialValue maxBatchSize
    }

let streamOfStreamFM<'TS, 'TD> (stream: CloudStream<'TD>) initialValue maxBatchSize (preFilter: StreamFrame<'TD> -> bool) (foldF: 'TS -> StreamFrame<'TD> -> 'TS) = 
    cloud { 
        let getter getLast = local {
            let! last = getLast()
            let! msgs = stream.GetFramesPage (last.Nonce + 1UL) maxBatchSize |> Cloud.AsLocal
            return [| 
                        let filtered = msgs.Take(maxBatchSize |> int).Where(preFilter).ToArray() // Should be done with Linq as Array.take fails on too short arrays
                        let mutable state = last.Value
                        let mutable i = last.Nonce
                        for m in filtered do
                            state <- foldF state m
                            i <- i + 1UL
                            yield { Nonce = i; Value = state }
                    |]
        }
        return! enqueueStream getter initialValue maxBatchSize
    }

let streamOfStream<'TS, 'TD> (stream: CloudStream<'TD>) initialValue maxBatchSize (f: 'TS -> StreamFrame<'TD> -> 'TS) = 
    streamOfStreamFM<'TS, 'TD> stream initialValue maxBatchSize (fun _ -> true) f


type StreamSink<'T> = {
    Push: 'T -> Cloud<unit> // TODO: replace with enqueueing result
    PushBatch: 'T[] -> Cloud<unit>
    CurrentState: unit -> Cloud<uint64 * ('T * uint64)[]>
}

let streamOfSink<'T> initialValue maxBatchSize = cloud {
        let! streamId = CloudAtom.CreateRandomContainerName()
        let! sinkAtom = CloudAtom.New<uint64 * ('T * uint64)[]>((0UL, [||]), "sink", streamId)
        let getter getLast = local { 
                                    return! local {
                                        let! last = getLast()
                                        let! (pos, msgs) = sinkAtom.GetValueAsync() |> Cloud.OfAsync
                                        let batch = msgs.Take(maxBatchSize |> int).ToArray()
                                        if (Array.isEmpty batch) then 
                                            return [||]
                                        else
                                            let newPos = batch |> Array.last |> snd
                                            do! sinkAtom.UpdateAsync(fun (p, t) -> 
                                                                        let newT = t |> Array.skipWhile (fun x -> snd x <= newPos) 
                                                                        (newPos, newT)
                                                                    ) |> Cloud.OfAsync
                                            return batch |> Array.mapi (fun i m -> { Nonce = last.Nonce + uint64(i); Value = fst m })
                                    }
                                }
        let sink = {
            Push = (fun (t: 'T) -> cloud { do! Cloud.OfAsync <| sinkAtom.UpdateAsync(fun (pos, et) -> (pos, Array.append et [| (t, pos + uint64(et.Length) + 1UL) |] )) })
            PushBatch = (fun t -> cloud { do! Cloud.OfAsync <| sinkAtom.UpdateAsync(fun (pos, et) -> (pos, t |> Array.mapi (fun i e -> (e, pos + uint64(i + et.Length) + 1UL)) |> Array.append et )) })
            CurrentState = (fun () -> cloud { return! sinkAtom.GetValueAsync() |> Cloud.OfAsync })
        }
        let! stream = enqueueStream getter initialValue maxBatchSize
        return (sink, stream)
    }

type ChainStream<'T> = Cloud<CloudStream<'T>> -> unit

module ChainStream =
    let inline ofArray chunkSize (source: 'T[]) : Cloud<CloudStream<'T>> = cloud {
        let! (sink, sr) = streamOfSink None chunkSize
        do! sink.PushBatch source
        return sr          
    }

    let inline ofQueue chunkSize (queue: CloudQueue<'T>) : Cloud<CloudStream<'T>> = 
        streamOfQueue<'T> queue None chunkSize          

    let inline filter chunkSize (predicate: 'TD -> bool) (stream: CloudStream<'TD>) : Cloud<CloudStream<'TD>> =
        streamOfStreamFM<'TD, 'TD> stream None chunkSize (fun df -> predicate df.Value) (fun t d -> d.Value)

    let inline map chunkSize (mapF: 'TD -> 'TS) (stream: CloudStream<'TD>) : Cloud<CloudStream<'TS>> =
        streamOfStreamFM<'TS, 'TD> stream None chunkSize (fun _ -> true) (fun _ d -> mapF d.Value)

    let inline toArray (stream: CloudStream<'TD>) : Cloud<'TD []> = // TODO: add toObservable
        stream.GetPage 0UL UInt32.MaxValue

    let inline fold chunkSize (foldF: 'TS -> 'TD -> 'TS) (state: 'TS) (stream: CloudStream<'TD>) =
        streamOfStreamFM<'TS, 'TD> stream (Some state) chunkSize (fun _ -> true) (fun t d -> foldF t d.Value)

    let inline reduce chunkSize (reducer: 'T -> 'T -> 'T) (stream:CloudStream<'T>) =
        streamOfStreamFM<'T, 'T> stream None chunkSize (fun _ -> true) (fun t d -> reducer t d.Value)
//
//    let inline sum (stream : Stream< ^T>) : ^T
//          when ^T : (static member Zero : ^T)
//          and ^T : (static member (+) : ^T * ^T -> ^T) =
//       fold (+) LanguagePrimitives.GenericZero stream

[|for i in 0UL .. 99999UL do yield "item" + i.ToString()|]
|> ChainStream.ofArray 10000u




let (sink, sr) = streamOfSink (Some "") 10000u |> cluster.Run
let srPos = sr.Position() |> cluster.Run
//let srAll = sr.GetPage 0UL 1000000u |> cluster.Run |> Seq.toArray
//
//srAll.Length


//for i in 0UL .. 999UL do sink.Push ("item" + i.ToString()) |> cluster.Run

for i in 0UL .. 9UL do
    sink.PushBatch [|for i in 0UL .. 99999UL do yield "item" + i.ToString()|] |> cluster.Run


let st = sink.CurrentState() |> cluster.Run

st |> fst
(st |> snd).Length

[|0;1;2;3;4;5;6|] |> Array.take 5

////////////////
    
//let queue = CloudQueue.New<string>() |> cluster.Run
////let streamRef = enqueueFlow queue (fun d -> local {Cloud.Logf "data - '%A'" d |> ignore} |> ignore ) |> cluster.CreateProcess
////send queue "aaaaaa1"
//let streamRef = streamOfQueue queue (Some "_init_") 1000u |> cluster.CreateProcess
//streamRef.ShowInfo()
//let res = streamRef.Result
//let pos = res.Position() |> cluster.Run
////let all = res.GetFramesPage 0UL 1000u |> cluster.Run |> Seq.toArray
//
////all.Length
//
//res.FlowProcess.Status
//
//cluster.GetAllProcesses()
//
////for i in 0UL .. 999UL do send queue ("item" + i.ToString())
//
//for i in 0UL .. 100UL do
//    sendBatch queue [|for i in 0UL .. 999UL do yield "item" + i.ToString()|]
//
//
//queue.Dequeue(5000)

let nestedRef = streamOfStream sr (Some 0UL) 1000u (fun _ sf -> sf.Nonce) |> cluster.CreateProcess
//send queue "aaaaaa1"
nestedRef.ShowInfo()
let res2 = nestedRef.Result
let pos2 = res2.Position() |> cluster.Run
let all2 = res2.GetFramesPage 99000UL 1000u |> cluster.Run |> Seq.toArray

all2.Length
//all2.[0].Value

res2.FlowProcess.Status


let nestedRef3 = streamOfStream<string, uint64> res2 (Some "str - ") 3000u (fun prev sf -> prev.Substring(0, 6) + sf.Value.ToString() ) |> cluster.CreateProcess
//send queue "aaaaaa1"
nestedRef3.ShowInfo()
let res3 = nestedRef3.Result
let pos3 = res3.Position() |> cluster.Run
let all3 = res3.GetPage 0UL 1000000u |> cluster.Run |> Seq.toArray

all3.Length


let everywhereStream<'TS, 'TD> (stream: CloudStream<'TD>) initialValue maxBatchSize (f: 'TS -> StreamFrame<'TD> -> 'TS) = 
    cloud {
        return! streamOfStream stream initialValue maxBatchSize f 
    } 
    |> Cloud.ParallelEverywhere
    

let evrRef = everywhereStream<string, string> res3 (Some "aa") 2000u (fun _ sf -> "everywhere " + sf.Value) |> cluster.CreateProcess
//send queue "aaaaaa1"
evrRef.ShowInfo()
let evr = evrRef.Result
let posevr0 = evr.[0].Position() |> cluster.Run
let posevr1 = evr.[1].Position() |> cluster.Run
let posevr2 = evr.[2].Position() |> cluster.Run
let posevr3 = evr.[3].Position() |> cluster.Run
let allevr0 = evr.[0].GetFramesPage 0UL 1000000u |> cluster.Run |> Seq.toArray
let allevr1 = evr.[1].GetFramesPage 0UL 1000000u |> cluster.Run |> Seq.toArray
let allevr2 = evr.[2].GetFramesPage 0UL 1000000u |> cluster.Run |> Seq.toArray
let allevr3 = evr.[3].GetFramesPage 0UL 1000000u |> cluster.Run |> Seq.toArray

allevr0.Length
allevr1.Length
allevr2.Length
allevr3.Length

//let a = allevr0 |> Array.map(fun f -> f.Nonce)
//let notseq = Array.zip (a |> Array.take (a.Length - 1)) (a |> Array.skip 1) 
//                |> Array.filter (fun (a, b) -> a + 1UL <> b)



/////////////////////////


//let queueToFile queue = 
//    queue 
//    |> CloudFlow.OfCloudQueue 
//    |> CloudFlow.To


let queue1 = CloudQueue.New<string>() |> cluster.Run

open System.Linq
[|1|].Take(5).ToArray() |> Seq.take 2



//let createLocalStream<'TS, 'TD> (stream: CloudStream<StreamFrame<'TD>>) (f: StreamFrame<'TD> -> 'TS) = 
//    cloud { 
////        let! sync = CloudAtom.New<string>("")
////        sync.
//        let! streamId = CloudAtom.CreateRandomContainerName() // TODO: Replace with node/stream pubkey
//        let! dict = CloudDictionary.New<StreamFrame<'TS>>(streamId + "-data")
//
//        let rec loop position = cloud {
//            let! newPosition = stream.Position() 
//            if newPosition > position then
//                let! missing = stream.GetFrom((position + 1L) |> uint64) 
//                return! local {
//                    let mutable lastPosition = position
//                    for d in missing do 
//                        dict.ForceAdd(d.Nonce.ToString(), { Nonce = d.Nonce; Value = f (d) }) 
//                        lastPosition <- Math.Max(lastPosition, position)
//                    return! loop lastPosition
//                }
//            else
//                do! Async.Sleep 100 |> Cloud.OfAsync
//                return! loop position
//        }
//
//        let! flowProcess = (loop -1L) |> Cloud.CreateProcess 
//
//        return buildStreamDef streamId dict flowProcess
//    }
//
//let nestedRef = createLocalStream res (fun sf -> sf.Nonce + 1000000UL) |> cluster.CreateProcess
////send queue "aaaaaa1"
//streamRef.ShowInfo()
//let res2 = nestedRef.Result
//let pos2 = res2.Position() |> Async.RunSynchronously
//let all2 = res2.GetFrom 0UL |> Async.RunSynchronously |> Seq.toArray
//
//all2.Length
//all2.[0].Value
//
//res2.FlowProcess.Status
//
//let nestedRef3 = createLocalStream<string, uint64> res2 (fun sf -> "str - " + sf.Value.ToString() ) |> cluster.CreateProcess
////send queue "aaaaaa1"
//nestedRef3.ShowInfo()
//let res3 = nestedRef3.Result
//let pos3 = res3.Position() |> Async.RunSynchronously
//let all3 = res3.GetFrom 0UL |> Async.RunSynchronously |> Seq.toArray
//
//all3.Length
//
//res3.FlowProcess.Status
//
//
//let createEverywhereStream<'TS, 'TD> (stream: CloudStream<StreamFrame<'TD>>) (f: StreamFrame<'TD> -> 'TS) = 
//    cloud {
//        return! createLocalStream stream f 
//    } 
//    |> Cloud.ParallelEverywhere
//    
//
//let evrRef = createEverywhereStream<string, string> res3 (fun sf -> "everywhere " + sf.Value) |> cluster.CreateProcess
////send queue "aaaaaa1"
//evrRef.ShowInfo()
//let evr = evrRef.Result
//let posevr0 = evr.[0].Position() |> Async.RunSynchronously
//let posevr1 = evr.[1].Position() |> Async.RunSynchronously
//let posevr2 = evr.[2].Position() |> Async.RunSynchronously
//let posevr3 = evr.[3].Position() |> Async.RunSynchronously
//let allevr0 = evr.[0].GetFrom 0UL |> Async.RunSynchronously |> Seq.toArray
//let allevr1 = evr.[1].GetFrom 0UL |> Async.RunSynchronously |> Seq.toArray
//let allevr2 = evr.[2].GetFrom 0UL |> Async.RunSynchronously |> Seq.toArray
//let allevr3 = evr.[3].GetFrom 0UL |> Async.RunSynchronously |> Seq.toArray
//
//allevr0.Length
//allevr1.Length
//allevr2.Length
//allevr3.Length
//
//evr.[0].FlowProcess.Status
//
//
//////////////////////////////////////////////////////////
//
//
//
//let createSingleStream<'T> (queue: CloudQueue<'T>) maxBatchSize emit = 
//    cloud { 
//        return! local {
//            let! streamId = CloudAtom.CreateRandomContainerName() // TODO: Replace with node/stream pubkey
//            let! data = CloudDictionary.New<string>(streamId + "data")
//
//            while true do 
//                let! msgs = Cloud.OfAsync <| queue.DequeueBatchAsync(maxBatchSize)
//                let! batch = CloudValue.NewArray<'T> (msgs, StorageLevel.MemoryAndDisk)
//                let! newPosition = Cloud.OfAsync <| data.GetCountAsync()
//                do! Cloud.OfAsync <| data.ForceAddAsync (newPosition.ToString(), batch.Id)
//                emit msgs
//            return streamId 
//        }
//    }
//
//
//////////////////////////////////////////////
//
//
//let writeToStream data =
//    let ct = Avalanchain.Utils.cryptoContext
//    let nodeStore = NodeStore ct
//    let node = nodeStore.GetNode<string, string> ("/", [])
//    let stream = node.CreateStream "s1" 0u <@ fun s d -> ok (s + s) @> "" ExecutionPolicy.Pass
//    let res = 
//        match stream with
//        | Ok (s, _) -> 
//            try 
//                let ret = node.Push s.Ref data
//                printfn "%A" ret
//                ret
//            with
//                | e -> 
//                    printfn "%A" e
//                    fail (ProcessingFailure(e.Message :: [e.StackTrace]))
//        | Bad(_) -> failwith "Not implemented yet"
//    res
//
//
//
//let queue = CloudQueue.New<string>() |> cluster.Run
//
//(** Next, you start a cloud task to send 100 messages to the queue: *)
//let sendTask = 
//    cloud { for i in [ 0 .. 100000 ] do 
//                queue.Enqueue (sprintf "hello%d" i) }
//    |> cluster.CreateProcess
//
//sendTask.ShowInfo() 
//
//
//    //persistedFlow.ToEnumerable
////                |> fun x -> x.
////                |> PersistedCloudFlow.
////                |> CloudFlow.toArray
//
////    let! desc = local {
////
////        //let parti
////
////        let! persistedFlow = flow |> CloudFlow.persist StorageLevel.MemoryAndDisk
////
////        //persistedFlow.
////
////    //    |> CloudFlow.map (fun n -> Sieve.getPrimes n)
////    //    |> CloudFlow.map (fun primes -> sprintf "calculated %d primes: %A" primes.Length primes)
////    //    |> CloudFlow.toArray
////    //    |> cluster.CreateProcess 
////
////    }
////    return desc
//
////    return 1
////}
//
////type 
//
//(** Next, you start a cloud task to wait for the 100 messages: *)
//let createSingleStream maxBatchSize emit = 
//    cloud { 
//        return! local {
//            let! streamId = CloudAtom.CreateRandomContainerName() // TODO: Replace with node/stream pubkey
//            //let! pointer = CloudAtom.GetById<uint64> ("pointer", streamId)
//            let! pointer = CloudAtom.New<uint64> (0UL, "pointer", streamId)
//            let! data = CloudDictionary.New<string>(streamId + "data")
//            //let! outQueue = CloudQueue.New<string>()
//
//            //let results = new ResizeArray<_>()
//            while true do 
//                let! msgs = Cloud.OfAsync <| queue.DequeueBatchAsync(maxBatchSize)
//                let! batch = CloudValue.NewArray<string> (msgs, StorageLevel.MemoryAndDisk)
//                data.ForceAdd (pointer.Value.ToString(), batch.Id)
//                let! currentPointer = CloudAtom.Increment pointer 
//                emit msgs
//                //outQueue.EnqueueBatch msgs
//                    //return writeToStream msg
//                    //return msg
//                //results.Add msg
//            //return results.ToArray() 
//            return streamId 
//        }
//    }
//    //|> Cloud.ParallelEverywhere
//    |> cluster.CreateProcess
//
//let receiveTask = createSingleStream 10
//receiveTask.ShowInfo() 
//
//(** Next, you wait for the result of the receiving cloud task: *)
////receiveTask.Result.[0].[0]
//
//(** 
//## Using queues as inputs to reactive data parallel cloud flows
//
//You now learn how to use cloud queues as inputs to a data parallel cloud flow.
//
//*)
//
//cluster.ShowProcesses()