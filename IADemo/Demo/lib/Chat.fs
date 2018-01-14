namespace Avalanchain

module Chat =

    open System
    open System.Collections.Generic
    open Akka.Actor
    open Akka.Configuration
    open Akka.IO
    
    open Akkling
    open Akkling.Cluster

    open Akka.DistributedData
    open Akkling
    open Akkling.DistributedData
    open Akkling.DistributedData.Consistency
    
    open Avalanchain
    open Avalanchain.ChainDefs
    open Avalanchain.DData

    let chainDefs system = ORSetHelper(system, "chainDefs")

    type AccountAddress = string 
    type ChannelAddress = string 

    type ChatAccount = {
        Address: AccountAddress
        Name: string
        PubKey: PublicKey
    }

    type ChatChannel = {
        Address: ChannelAddress
        Name: string
        //Participants: AccountAddress set
    }

    type ChatMessage = {
        Account: AccountAddress
        Channel: ChannelAddress
        Message: string
        Timestamp: DateTimeOffset
    }

    type ChatAccounts(system) = 
        let accounts = ORSetHelper<ChatAccount>(system, "chatAccounts")
        let channels = ORSetHelper<ChatChannel>(system, "chatChannels")
        //do accounts.Add [] |> Async.RunSynchronously // fighting some strange bug here
        let channelParticipants = ORMultiMapHelper<ChannelAddress, AccountAddress>(system, "chatChannelParticipants")
        let channelMessages = ORMultiMapHelper<ChannelAddress, ChatMessage>(system, "chatChannelMessages")
        member __.Accounts() = async {  let! all = accounts.Get()
                                        return all |> ORSet.value }
        member __.Channels() = async {  let! all = channels.Get()
                                        return all |> ORSet.value }
        member __.AddAccount account = accounts.Add [account]
        member __.AddChannel channel = channels.Add [channel]
        member __.GetAccount address = async {  let! all = __.Accounts()
                                                return all |> Seq.tryFind (fun ca -> ca.Address = address) }
        member __.NameForAddress address = async {  let! account = __.GetAccount address
                                                    return account |> Option.map (fun ca -> ca.Name) }
        member __.Participants() = async {  let! participants = channelParticipants.Get() 
                                            return participants |> ORMultiMap.toMap }
        member __.AddParticipant channel address = channelParticipants.AddItem channel address 
        member __.ChannelMessages channel = async { let! participants = channelMessages.Get() 
                                                    return participants |> ORMultiMap.tryFind channel |> Option.map(fun msgs -> msgs |> Seq.sortBy(fun m -> m.Timestamp) |> Seq.toArray) }
        member __.PostMessage channel account message = channelMessages.AddItem channel { Account = account; Channel = channel; Message = message; Timestamp = DateTimeOffset.Now } 
        member __.PostMessages channel account messages = channelMessages.AddItems (messages |> Seq.map (fun m -> channel, { Account = account; Channel = channel; Message = m; Timestamp = DateTimeOffset.Now }))

    
    type PaymentAccountRef = { Address: string }

    type PaymentTransactionRef = { TRef: string }

    type PaymentAmount = decimal

    type PaymentTransaction = {
        From: PaymentAccountRef
        To: PaymentAccountToRef[]
        Dt: DateTimeOffset
    }
    and PaymentAccountToRef = {
        Ref: PaymentAccountRef
        Amount: PaymentAmount
    }

    //let transactionsSet<'T when 'T: null> system = ORSetHelper<'T>(system, "transactions")

    type PaymentAccount = {
        Ref: PaymentAccountRef
        PubKey: PublicKey
        Name: string
        // CryptoContext: CryptoContext
    }

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
        Ref: PaymentTransactionRef
        Result: Result<PaymentTransaction, TransactionRejectionStatus>
        //Balances: PaymentBalances
        TimeStamp: DateTimeOffset
    }

    type PaymentAccounts(system: ActorSystem) = 
        let clusterNode = Akka.Cluster.Cluster.Get system
        let accounts = ORSetHelper<PaymentAccount>(system, "pmtAccounts")
        let transactions = LWWMapHelper<PaymentTransactionRef, StoredTransaction>(system, "pmtTransactions")
        let posCounter = GCounterHelper(system, "pmtCounter_" + clusterNode.SelfUniqueAddress.Uid.ToString())
        let balances = ORMultiMapHelper<PaymentAccountRef, PaymentTransactionRef>(system, "pmtBalances")
        member __.Accounts() = async {  let! all = accounts.Get()
                                        return all |> ORSet.value }
        member __.AddAccount account = accounts.Add [account]
        member __.GetAccount address = async {  let! all = __.Accounts()
                                                return all |> Seq.tryFind (fun ca -> ca.Ref.Address = address) }
        member __.NameForAddress address = async {  let! account = __.GetAccount address
                                                    return account |> Option.map (fun ca -> ca.Name) }
        member __.Transactions() = async {  let! trans = transactions.Get() 
                                            return trans |> LWWMap.value }
        member __.Balances() = async {  let! bals = balances.Get() 
                                        return bals |> ORMultiMap.toMap }
        member __.AddressTransactions address = async { let! bals = balances.Get()  
                                                        let! trans = transactions.Get()
                                                        return bals 
                                                                |> ORMultiMap.tryFind { Address = address } 
                                                                |> Option.map(fun bal -> bal |> Seq.choose (fun b -> trans |> LWWMap.tryFind b) |> Seq.toArray |> Array.sortBy(fun st -> st.TimeStamp))
                                                                //|> Seq.filter (fun t -> t.From = address) 
                                                                //|> Seq.sortBy (fun t -> t.Dt) 
                                                        }

        member __.AddressBalance address = async {  let! transOpt = __.AddressTransactions address  
                                                    return transOpt
                                                            |> Option.map(  Array.choose (fun st -> match st.Result with | Ok t -> Some t | _ -> None)
                                                                            >> Array.fold (fun st t -> st + (if t.From.Address = address then t.To |> Array.sumBy (fun tt -> tt.Amount) else 0M)
                                                                                                          - (t.To |> Array.filter(fun tt -> tt.Ref.Address = address) |> Array.sumBy (fun tt -> tt.Amount))) 0M
                                                                            )
                                                            |> Option.defaultValue 0M
                                                    }

        member __.PostTransactions trans = async {
            let items = [   for transaction in trans ->
                            let st = {  Ref = { TRef = Guid.NewGuid().ToString("N") }
                                        Result = Ok transaction // Add validation
                                        TimeStamp = DateTimeOffset.Now } 
                            st.Ref, st ] 
            do! transactions.AddItems items 
            do! balances.Modify <|  fun balsU ->
                                        let mutable items = items
                                        let mutable bals = balsU
                                        for (rf, st) in items do 
                                            match st.Result with
                                            | Ok trans ->
                                                bals <- bals |> ORMultiMap.addItem clusterNode trans.From rf
                                                bals <- trans.To |> Array.fold (fun bl t -> bl |> ORMultiMap.addItem clusterNode t.Ref rf) bals
                                            | _ -> ()
                                        bals

        }

        member __.PostTransaction transaction = __.PostTransactions [ transaction ]
                        
            //transactions.Add [transaction]
        //member __.PostMessages channel account messages = 
        //    channelMessages.AddItems (messages |> Seq.map (fun m -> channel, { Account = account; Channel = channel; Message = m; Timestamp = DateTimeOffset.Now }))



    //type PaymentBalances(system: ActorSystem, key) =
    //    let cluster = Akka.Cluster.Cluster.Get system
    //    let ddata = DistributedData.Get system

    //    let orsetKey = ORSet.key<'T> key //"chainDefs"    


