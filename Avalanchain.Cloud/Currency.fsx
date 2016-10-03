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

#load "ChainStream.fs"
#load "ChainFlow.fs"

// Note: Before running, choose your cluster version at the top of this script.
// If necessary, edit AzureCluster.fsx to enter your connection strings.

open System
open System.Linq
open System.IO
open MBrace.Core
open MBrace.Flow
open MBrace.Library
open Chessie.ErrorHandling
open Avalanchain.Quorum
open Avalanchain.EventStream
open Avalanchain.NodeContext
open FSharp.Control
open Nessos.Streams
open MBrace.Runtime
open Avalanchain
open Avalanchain.Cloud

type PubKey = string
type PrivKey = string

type Address = {
    PublicKey: string
}
type Amount = decimal

type Transaction = {
    From: (Address * Amount)[]
    To: (Address * Amount)[] 
}

//type Transaction2 = {
//    From: Address
//    To: Address 
//    Amount: Amount 
//}

type Balances = {
    Balances: Map<Address, Amount>
    InvalidTransactions: Transaction list
}
with static member Empty = { Balances = Map.empty; InvalidTransactions = []}

//type Wallet = 


let tryApply (balances: Balances) transaction =
    let mutable bls = balances.Balances
    for t in transaction.From do bls <- bls.Add(fst t, bls.[fst t] - snd t)
    for t in transaction.To do bls <- bls.Add(fst t, bls.[fst t] + snd t)
    if bls |> Seq.exists (fun b -> b.Value < 0m) then { balances with InvalidTransactions = transaction :: balances.InvalidTransactions }
    else { balances with Balances = bls }

//    let newBlsFrom = transaction.From |> Array.fold (fun bls t -> bls.Balances.Add(fst t, bls.Balances.[fst t] - snd t)) balances
//    let newBls = transaction.To |> Array.fold (fun bls t -> bls.Balances.Add(fst t, bls.Balances.[fst t] + snd t)) newBlsFrom

let cluster = Config.GetCluster() 
//cluster.KillAllWorkers()

let nodeContextDict = cloud { return! CloudDictionary.New<NodeContext>("nodes") } |> cluster.Run
let clusterContext = ClusterContext(nodeContextDict)


let sink, transactions = 
    ChainFlow.ofSink<Transaction> clusterContext 10000u 
    |> cluster.Run

let balances = 
    ChainFlow.ofChain transactions
    |> ChainFlow.fold 1000u (tryApply) Balances.Empty
    |> cluster.Run
   
let invalidTransactions = 
    ChainFlow.ofChain balances 
    |> ChainFlow.map 1000u (fun b -> b.InvalidTransactions)
    |> cluster.Run

let transactionsPos = transactions.Position() |> cluster.Run

let transactionsCurrent = transactions.Current() |> cluster.Run
let transactionsAll = transactions.GetFramesPage 0UL 1000000u |> cluster.Run
transactionsAll.Length


let newAccount() = { PublicKey = Guid.NewGuid().ToString() }

let newAccounts = [| for i in 0u .. 100u do yield newAccount() |]

let randomTransaction accounts = 
    let rnd = new Random()
    let amount = rnd.Next 100
    {
        
    }

let trans = [|for i in 0UL .. 99999UL do yield "item" + i.ToString()|]

let str = [|for i in 0UL .. 99999UL do yield "item" + i.ToString()|]
            |> ChainFlow.ofArray clusterContext 10000u

