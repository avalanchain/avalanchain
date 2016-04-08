#load "ThespianCluster.fsx"

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
#load "PaymentNetwork.fs"

// Note: Before running, choose your cluster version at the top of this script.
// If necessary, edit AzureCluster.fsx to enter your connection strings.

open System
open System.Linq
open System.IO
open MBrace.Core
open MBrace.Flow
open MBrace.Library
open Chessie.ErrorHandling
open Avalanchain.SecKeys
open Avalanchain.Quorum
open Avalanchain.EventStream
open Avalanchain.NodeContext
open FSharp.Control
open Nessos.Streams
open MBrace.Runtime
open Avalanchain.Cloud
open Avalanchain.Cloud.PaymentNetwork


let newAccount name =
    let cctx = cryptoContextNamed name

    let accountRef = {
        PublicKey = cctx.SigningPublicKey
        Address = cctx.Address
    }

    let account = {
        Ref = accountRef
        Name = name
        CryptoContext = cctx
    }
    account


let accounts = [for i in 0 .. 200 do yield (newAccount (i.ToString()))]

let balances1 = accounts |> List.map (fun a -> a.Ref.Address)

let balances = accounts |> List.map (fun a -> a.Ref, 1000M) |> Map.ofList |> fun b -> { PaymentBalances.Balances = b }

type TransactionStorage (initialBalances) =
    let mutable storedTransactions = []
    let balances() = if storedTransactions |> List.isEmpty then initialBalances else storedTransactions.Head.Balances
    interface ITransactionStorage with
        member x.All(): PaymentBalances * seq<StoredTransaction> = initialBalances, (storedTransactions |> List.rev |> Seq.ofList) 
        member x.AccountState(ref: PaymentAccountRef): PaymentAmount option * seq<StoredTransaction> = 
            initialBalances.Balances.TryFind ref, (storedTransactions |> List.rev |> Seq.ofList)
        member x.Add(transaction: PaymentTransaction): StoredTransaction = 
            let newTransaction = transaction |> applyTransaction (balances())
            storedTransactions <- newTransaction :: storedTransactions
            newTransaction
        member x.PaymentBalances() = balances()
         
        
let random = new Random()

let rec tradingBot (storage: ITransactionStorage) (random: Random): Async<unit> = async {
    let balances = storage.PaymentBalances().Balances |> Array.ofSeq

    let fromAcc = balances.[random.Next(balances.Length)]
    let toAcc = balances.[random.Next(balances.Length)]

    storage.Add {
        From = fromAcc.Key
        To = [| (toAcc.Key, (random.NextDouble() * float(fromAcc.Value) |> decimal)) |]
    } |> ignore
    
    do! Async.Sleep(random.Next(100, 2000))

    return! tradingBot storage random
}

let storage = TransactionStorage balances :> ITransactionStorage
let bot = tradingBot (storage) random 
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