namespace Avalanchain.Web.Controllers

open System.Net
open System.Net.Http
open System.Web.Http
open System.Linq

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

    let storedTransToModel st = {
        result = st.Result |> lift (fun t ->
        {
            fromAcc = { address = t.From.Address }
            toAcc = { address = (t.To.[0] |> fst).Address }
            amount = t.To.[0] |> snd 
        })
        timeStamp = st.TimeStamp
    }

    /// Gets a single value at the specified index.
    [<Route("test/t1")>]
    [<HttpGet>]
    member x.TestT1() =
        [for i in 0 .. 10 do yield "test " + i.ToString()]

    /// Gets a single value at the specified index.
    [<Route("account/all")>]
    [<HttpGet>]
    member x.AccountsGet() =
        let balances = PaymentNetwork.transactionStorage.PaymentBalances()
        PaymentNetwork.transactionStorage.Accounts() 
        |> List.map (fun a -> a |> accountToModel (match balances.Balances.TryFind(a.Ref) with Some(b) -> b | None -> 0M))

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
                            |> Seq.map(storedTransToModel) 
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
        PaymentNetwork.transactionStorage.Submit trans
        |> storedTransToModel
    

    /// Gets a single value at the specified index.
    [<Route("transaction/last")>]
    [<HttpGet>]
    member x.TransactionLast(pageSize: uint32) = 
        let _, transactions = PaymentNetwork.transactionStorage.All()
        transactions.Take(pageSize |> int).ToArray()
        |> Array.rev
        |> Array.map(storedTransToModel)
        
























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
