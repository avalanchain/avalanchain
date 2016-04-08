namespace Avalanchain.Web.Models

open Avalanchain.PaymentNetwork
open System
open Chessie.ErrorHandling

//[<CLIMutable>]
//type Node = {   
//    Path : string
//    StreamsCount : uint32 
//}


[<CLIMutable>]
type AccountRef = {   
    address: string
}

[<CLIMutable>]
type Account = {   
    ref: AccountRef
    publicKey: byte[]
    name: string
    balance: PaymentAmount
}

[<CLIMutable>]
type Transaction = {
    fromAcc: AccountRef
    toAcc: AccountRef
    amount: PaymentAmount
}

[<CLIMutable>]
type Balance = {
    address: string 
    amount: PaymentAmount
}

[<CLIMutable>]
type Balances = {
    //initial: Map<AccountRef, PaymentAmount>
    balances: Balance[]
}

[<CLIMutable>]
type StoredTransaction = {
    result: Result<Transaction, TransactionRejectionStatus>
    timeStamp: DateTimeOffset
}

[<CLIMutable>]
type AccountDetail = {   
    ref: AccountRef
    publicKey: byte[]
    name: string
    balance: PaymentAmount
    transactions: StoredTransaction[]
}

