namespace Avalanchain.Cloud

module PaymentNetwork =

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
    open Avalanchain.Cloud
    open Avalanchain.SecKeys

    type PaymentAccountRef = {
        PublicKey: SigningPublicKey
    }

    type PaymentAmount = decimal

    type PaymentTransaction = {
        From: PaymentAccountRef
        To: (PaymentAccountRef * PaymentAmount)[]
    }

    type HashedPT = SignedProof<PaymentTransaction>

    type PaymentBalances = {
        Balances: PaymentBalancesData
    }
    and PaymentBalancesData = Map<PaymentAccountRef, PaymentAmount>

    type TransactionRejectionStatus =
        | WrongHash 
        | WrongSignature
        | FromAccountNotExists of PaymentAccountRef
        | UnexpectedNegativeAmount of PaymentAmount
        | NotEnoughFunds of NotEnoughFunds
    and NotEnoughFunds = {
        Available: PaymentAmount
        Expected: PaymentAmount
    } 

    type StoredTransaction = {
        Result: Result<PaymentTransaction, TransactionRejectionStatus>
        Balances: PaymentBalances
    }

    let signatureChecker transaction =
        ok(transaction) // TODO: Add check

    let enoughFundsChecker (balances: PaymentBalances) transaction : StoredTransaction =
        let total = transaction.To |> Array.sumBy (fun v -> snd v)
        match balances.Balances.TryFind(transaction.From) with 
        | None -> { Result = fail(FromAccountNotExists transaction.From); Balances = balances }
        | Some(value) -> 
            match value with
            | v when v < total -> { Result = fail(NotEnoughFunds ({ Expected = total; Available = v })); Balances = balances }
            | v -> 
                let rec applyTos (blns: PaymentBalancesData) tos : StoredTransaction = 
                    match tos with
                    | [] -> { Result = ok(transaction); Balances = { Balances = blns} }
                    | t :: _ when snd t < 0m -> { Result = fail(UnexpectedNegativeAmount (snd t)); Balances = balances }
                    | t :: ts -> 
                        let accoundRef = fst t
                        let existingBalance = blns |> Map.tryFind accoundRef
                        let newBlns = match existingBalance with
                                        | None -> blns.Add(accoundRef, snd t)
                                        | Some eb -> blns.Add(accoundRef, (snd t) + eb)
                        applyTos newBlns ts
                applyTos (balances.Balances) (transaction.To |> List.ofArray) 
                
                


    let createPaymentFlow (cluster: ChainClusterClient) (inputStream: CloudStream<SignedProof<PaymentTransaction>>) =
        
        let balances = 
            ChainFlow.ofStream inputStream


        balances