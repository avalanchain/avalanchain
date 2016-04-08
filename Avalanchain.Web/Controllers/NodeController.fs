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

    let accountToModel (account: PaymentAccount) = {
        ref = { address = account.Ref.Address }
        publicKey = account.PublicKey
        name = account.Name
    }


    /// Gets a single value at the specified index.
    [<Route("account/all")>]
    [<HttpGet>]
    member x.AccountsGet() =
        PaymentNetwork.transactionStorage.Accounts() 
        |> List.map accountToModel

    /// Gets a single value at the specified index.
    [<Route("account/get/{address}")>]
    [<HttpGet>]
    member x.AccountGet(request: HttpRequestMessage, address: string) =
        let accountOpt = PaymentNetwork.transactionStorage.Accounts() 
                            |> List.tryFind(fun a -> a.Ref.Address = address)

        match accountOpt with
        | Some a -> request.CreateResponse(a |> accountToModel)
        | None -> request.CreateResponse(HttpStatusCode.NotFound)


    /// Gets a single value at the specified index.
    [<Route("account/new")>]
    [<HttpPost>]
    member x.AccountNew() = PaymentNetwork.transactionStorage.NewAccount() |> accountToModel

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
    member x.SubmitTransaction(request: HttpRequestMessage, [<FromBody>] transaction: Transaction) =
        let toAcc, amount = transaction.toAcc
        let trans = {
            From = { Address = transaction.fromAcc.address }
            To = [| { Address = toAcc.address }, amount |]
        }
        let submittedTransaction = PaymentNetwork.transactionStorage.Submit trans
        {
            result = (submittedTransaction.Result 
                        |> bind (fun r -> 
                                let toAcc, amount = r.To.[0]
                                ok({
                                    fromAcc = { AccountRef.address = r.From.Address } 
                                    toAcc = { AccountRef.address = toAcc.Address }, amount
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
