#I "../packages/Newtonsoft.Json.8.0.2/lib/net45"
#r "../packages/FSharp.Interop.Dynamic.3.0.0.0/lib/portable-net45+sl50+win/FSharp.Interop.Dynamic.dll"
#r "../packages/FSharp.Core.Fluent-4.0.1.0.0.5/lib/portable-net45+netcore45+wpa81+wp8+MonoAndroid1+MonoTouch1/FSharp.Core.Fluent-4.0.dll"
#r "../packages/FSharpx.Extras.1.10.3/lib/40/FSharpx.Extras.dll"
#r "../packages/FSharpx.Async.1.12.0/lib/net40/FSharpx.Async.dll"
#r "../packages/FSharpx.Collections.1.14.0/lib/net40/FSharpx.Collections.dll"
#r "../packages/Chessie.0.4.0/lib/net40/Chessie.dll"
#r "../packages/FSharp.Quotations.Evaluator.1.0.7/lib/net40/FSharp.Quotations.Evaluator.dll"
#r "../packages/FsPickler.1.7.2/lib/net45/FsPickler.dll"
#r "../packages/FsPickler.Json.1.7.2/lib/net45/FsPickler.Json.dll"
#r "../packages/Base58Check.0.2.0/lib/Net40/Base58Check.dll"


#load "SecPrimitives.fs"
#load "SecKeys.fs"
#load "RefsAndPathes.fs"
#load "StreamEvent.fs"
#load "Projection.fs"
#load "Quorum.fs"
#load "Acl.fs"
#load "Utils.fs"
#load "EventStream.fs"
#load "EventProcessor.fs"
#load "FrameSynchronizer.fs"
#load "NodeContext.fs"

#load "PaymentNetwork.fs"

// Note: Before running, choose your cluster version at the top of this script.
// If necessary, edit AzureCluster.fsx to enter your connection strings.

open System
open System.Linq
open System.IO
open Chessie.ErrorHandling
open Avalanchain.SecKeys
open Avalanchain.Quorum
open Avalanchain.EventStream
open Avalanchain.NodeContext
open FSharp.Control
open Avalanchain.PaymentNetwork


let accounts = [for i in 0 .. 200 do yield (newAccount (i.ToString()))]

let balances1 = accounts |> List.map (fun a -> a.Ref.Address)

let balances = accounts |> List.map (fun a -> a.Ref, 1000M) |> Map.ofList |> fun b -> { PaymentBalances.Balances = b }


let storage = TransactionStorage balances :> ITransactionStorage
let bot = tradingBot (storage) (new Random())
            |> Async.StartAsTask


printfn "%A" (storage.PaymentBalances().Balances |> Seq.map(fun kv -> kv.Value) |> Seq.filter(fun v -> v < 1000M) |> Seq.toList)

printfn "%A" (storage.PaymentBalances().Balances 
                |> Seq.map(fun kv -> kv.Value) 
                |> Seq.filter(fun v -> v = 1000M) 
                |> Seq.toList)

printfn "%A" (storage.PaymentBalances().Balances |> Seq.map(fun kv -> kv.Value) |> Seq.toList)

printfn "%A" (storage.PaymentBalances().Balances |> Seq.map(fun kv -> kv.Key) |> Seq.toList |> List.length)


//let rec tradingBot1 (storage: ITransactionStorage) (random: Random): unit = 
//    let balances = storage.PaymentBalances().Balances |> Array.ofSeq
//
//    let fromAcc = balances.[random.Next(balances.Length)]
//    let toAcc = balances.[random.Next(balances.Length)]
//
//    storage.Add {
//        From = fromAcc.Key
//        To = [| (toAcc.Key, (random.NextDouble() * float(fromAcc.Value) |> decimal)) |]
//    } |> ignore
//    
//    Threading.Thread.Sleep(random.Next(100, 2000))
//
//    tradingBot1 storage random
//
//tradingBot1 (TransactionStorage balances) random 
//
//printfn "%A"