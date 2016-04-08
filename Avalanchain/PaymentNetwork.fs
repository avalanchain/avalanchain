namespace Avalanchain

module PaymentNetwork =

    open System
    open System.Linq
    open System.IO
    open Chessie.ErrorHandling
    open Avalanchain.Quorum
    open Avalanchain.EventStream
    open Avalanchain.NodeContext
    open FSharp.Control
    open Avalanchain.SecKeys

    type PaymentAccountRef = {
        Address: string
    }

    type PaymentAmount = decimal

    type PaymentTransaction = {
        From: PaymentAccountRef
        To: (PaymentAccountRef * PaymentAmount)[]
    }

//    type GenesisTransaction = {
//        To: PaymentAccountRef * PaymentAmount
//    }
//
//    type PaymentBalance = {
//        Genesis: GenesisTransaction
//
//        Amount: PaymentAmount
//    }

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
        TimeStamp: DateTimeOffset
    }

    type PaymentAccount = {
        Ref: PaymentAccountRef
        PublicKey: SigningPublicKey
        Name: string
        CryptoContext: CryptoContext
    }

    [<Interface>]
    type ITransactionStorage =
        abstract member All: unit -> PaymentBalances * StoredTransaction seq // Initial balances + transactions
        abstract member Submit: PaymentTransaction -> StoredTransaction
        abstract member AccountState: PaymentAccountRef -> PaymentAmount option * StoredTransaction seq // Initial balances + account transactions
        abstract member PaymentBalances: unit -> PaymentBalances
        abstract member Accounts: unit -> PaymentAccount list
        abstract member NewAccount: unit -> PaymentAccount


    let signatureChecker transaction =
        ok(transaction) // TODO: Add check

    let applyTransaction (balances: PaymentBalances) transaction : StoredTransaction =
        let total = transaction.To |> Array.sumBy (fun v -> snd v)
        match balances.Balances.TryFind(transaction.From) with 
        | None -> { Result = fail(FromAccountNotExists transaction.From); Balances = balances; TimeStamp = DateTimeOffset.UtcNow }
        | Some(value) -> 
            match value with
            | v when v < total -> { Result = fail(NotEnoughFunds ({ Expected = total; Available = v })); Balances = balances; TimeStamp = DateTimeOffset.UtcNow }
            | v -> 
                let rec applyTos (blns: PaymentBalancesData) tos : StoredTransaction = 
                    match tos with
                    | [] -> { Result = ok(transaction); Balances = { Balances = blns }; TimeStamp = DateTimeOffset.UtcNow }
                    | t :: _ when snd t < 0m -> { Result = fail(UnexpectedNegativeAmount (snd t)); Balances = balances; TimeStamp = DateTimeOffset.UtcNow }
                    | t :: ts -> 
                        let accountRef = fst t
                        let existingBalance = blns |> Map.tryFind accountRef
                        let newToBlns = match existingBalance with
                                        | None -> blns.Add(accountRef, snd t)
                                        | Some eb -> blns.Add(accountRef, (snd t) + eb)
                        let newFromBlns = newToBlns.Add(transaction.From, value - total)
                        applyTos newFromBlns ts
                applyTos (balances.Balances) (transaction.To |> List.ofArray) 
                
                

//
//    let createPaymentFlow (cluster: ChainClusterClient) (inputStream: CloudStream<SignedProof<PaymentTransaction>>) =
//        
//        let balances = 
//            ChainFlow.ofStream inputStream
//
//
//        balances

    let newAccount name =
        let cctx = cryptoContextNamed name

        let accountRef = {
            Address = cctx.Address
        }

        let account = {
            Ref = accountRef
            PublicKey = cctx.SigningPublicKey
            Name = name
            CryptoContext = cctx
        }
        account


    type TransactionStorage (accounts_, initialBalances_) =
        let mutable accounts = accounts_
        let mutable initialBalances = initialBalances_
        let mutable storedTransactions = []
        let balances() = if storedTransactions |> List.isEmpty then initialBalances else storedTransactions.Head.Balances
        interface ITransactionStorage with
            member x.Accounts(): PaymentAccount list = accounts
            member x.NewAccount(): PaymentAccount = 
                let account = newAccount (Guid.NewGuid().ToString())
                accounts <- account :: accounts
                initialBalances <- { Balances = initialBalances.Balances.Add(account.Ref, 0M) }
                account
            
            member x.All(): PaymentBalances * seq<StoredTransaction> = initialBalances, (storedTransactions |> List.rev |> Seq.ofList) 
            member x.AccountState(ref: PaymentAccountRef): PaymentAmount option * seq<StoredTransaction> = 
                initialBalances.Balances.TryFind ref, (storedTransactions |> List.rev |> Seq.ofList)
            member x.Submit(transaction: PaymentTransaction): StoredTransaction = 
                let newTransaction = transaction |> applyTransaction (balances())
                storedTransactions <- newTransaction :: storedTransactions
                newTransaction
            member x.PaymentBalances() = balances()
         
        
    let rec tradingBot (storage: ITransactionStorage) (random: Random): Async<unit> = async {
        let balances = storage.PaymentBalances().Balances |> Array.ofSeq

        let fromAcc = balances.[random.Next(balances.Length)]
        let toAcc = balances.[random.Next(balances.Length)]

        storage.Submit {
            From = fromAcc.Key
            To = [| (toAcc.Key, (random.NextDouble() * float(fromAcc.Value) |> decimal)) |]
        } |> ignore
    
        do! Async.Sleep(random.Next(100, 2000))

        return! tradingBot storage random
    }

    let accounts = [for i in 0 .. 199 do yield (newAccount (Guid.NewGuid().ToString()))]
    let balances = accounts |> List.map (fun a -> a.Ref, 1000M) |> Map.ofList |> fun b -> { PaymentBalances.Balances = b }
    let transactionStorage = TransactionStorage(accounts, balances) :> ITransactionStorage
    let bot = tradingBot (transactionStorage) (new Random())
                        |> Async.StartAsTask
