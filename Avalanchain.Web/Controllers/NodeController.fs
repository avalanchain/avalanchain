namespace Avalanchain.Web.Controllers

open System.Net
open System.Net.Http
open System.Web.Http

open Avalanchain
open Avalanchain.Web.Models
open Avalanchain.PaymentNetwork
open Chessie.ErrorHandling

/// Retrieves values.
[<RoutePrefix("api")>]
type NodeController() =
    inherit ApiController()


//    /// Gets all values.
//    [<Route("node")>]
//    member x.Get() = {
//            Path = "aaa" //node.Path
//            StreamsCount = 1u //uint32 (node.Streams.StreamMap.Count)
//        }

    let accountToModel balance (account: PaymentAccount) = {
        Account.ref = { address = account.Ref.Address }
        publicKey = account.PublicKey
        name = account.Name
        balance = balance
    }

    /// Gets a single value at the specified index.
    [<Route("account/all")>]
    [<HttpGet>]
    member x.AccountsGet() =
        let balances = PaymentNetwork.transactionStorage.PaymentBalances()
        PaymentNetwork.transactionStorage.Accounts() 
        |> List.map (fun a -> a |> accountToModel balances.Balances.[a.Ref])

    /// Gets a single value at the specified index.
    [<Route("account/get/{address}")>]
    [<HttpGet>]
    member x.AccountGet(address: string) =
        let accountOpt = PaymentNetwork.transactionStorage.Accounts() 
                            |> List.tryFind(fun a -> a.Ref.Address = address)

        accountOpt |> Option.map (fun account -> 
        let balance, transactions = PaymentNetwork.transactionStorage.AccountState(account.Ref)
        {
            AccountDetail.ref = { address = account.Ref.Address }
            publicKey = account.PublicKey
            name = account.Name
            balance = balance.Value
            transactions = transactions 
                            |> Seq.map(fun t1 -> 
                                        {
                                            result = t1.Result |> lift (fun t ->
                                            {
                                                fromAcc = { address = t.From.Address }
                                                toAcc = { address = (t.To.[0] |> fst).Address }
                                                amount = t.To.[0] |> snd 
                                            })
                                            timeStamp = t1.TimeStamp
                                        }) 
                            |> Seq.toArray
        })


    /// Gets a single value at the specified index.
    [<Route("account/new")>]
    [<HttpPost>]
    member x.AccountNew() = PaymentNetwork.transactionStorage.NewAccount() |> accountToModel 0M

    /// Gets a single value at the specified index.
    [<Route("balances/all")>]
    [<HttpGet>]
    member x.BalancesAll() = 
        let balances = PaymentNetwork.transactionStorage.PaymentBalances()
        {
            Balances.balances = 
                balances.Balances 
                |> Seq.map(fun kv -> { Balance.address = kv.Key.Address; amount = kv.Value })
                |> Array.ofSeq
        }

    /// Gets a single value at the specified index.
    [<Route("transaction/submit")>]
    [<HttpPost>]
    member x.SubmitTransaction([<FromBody>] transaction: Transaction) =
        let trans = {
            From = { Address = transaction.fromAcc.address }
            To = [| { Address = transaction.toAcc.address }, transaction.amount |]
        }
        let submittedTransaction = PaymentNetwork.transactionStorage.Submit trans
        {
            result = (submittedTransaction.Result 
                        |> bind (fun r -> 
                                let toAcc, amount = r.To.[0]
                                ok({
                                    fromAcc = { AccountRef.address = r.From.Address } 
                                    toAcc = { AccountRef.address = toAcc.Address }
                                    amount = amount
                                })))
            timeStamp = submittedTransaction.TimeStamp
        }
    
























    //let node = Avalanchain.Node.defaultNode
    

//    /// Gets all values.
//    [<Route("node")>]
//    member x.Get() = {
//            Path = "aaa" //node.Path
//            StreamsCount = 1u //uint32 (node.Streams.StreamMap.Count)
//        }



//    /// Gets a single value at the specified index.
//    [<Route("cars/{id}")>]
//    member x.Get(request: HttpRequestMessage, id: int) =
//        if id >= 0 && values.Length > id then
//            request.CreateResponse(values.[id])
//        else 
//            request.CreateResponse(HttpStatusCode.NotFound)
//
