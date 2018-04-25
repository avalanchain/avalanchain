namespace Avalanchain.Common

module MatchingEngine = 

    open System
    open System.Threading
    open System.Collections.Generic
    open System.Collections.Concurrent
    //open FSharpx.Collections
    open Akka.Streams.Dsl
    
    open Avalanchain.Core
    open Crypto

    type MatchType = | Partial | Full
    type MarketSide = | Bid | Ask
    with member __.Opposite = if __ = Bid then Ask else Bid
  

    type OrderID = Guid //uint64
    type Pos = uint64
    let newOrderID = Guid.NewGuid

    type ClOrdID = ClOrdID of string
    type Price = decimal<price>
    and [<Measure>] price
    type Quantity = decimal<qty>
    and [<Measure>] qty
    type Total = decimal<price*qty>

    type Symbol = Symbol of string
        with member __.Value = match __ with Symbol s -> s

    type TradingAccount = TradingAccount of string

    type OrderType = 
    // | Market 
    | Limit of Price
    // | Stop 
    // | StopLimit

    [<RequireQualifiedAccess>]
    type OrderCommand = 
    | Create of OrderData
    | Cancel of OrderID

    and OrderEvent =
    | Created of Order
    | CreationFailed of OrderData * Reason: string
    | Matched of Allocation * MatchType
    | Expired of OrderID: OrderID * CreatedAt: Pos * ExpiredAt: Pos
    | Cancelled of OrderID: OrderID
    | CancellationFailed of OrderID: OrderID * Reason: string

    and OrderData = {
        OrderType: OrderType
        Symbol: Symbol 
        MarketSide: MarketSide
        Quantity: Quantity
        ClOrdID: ClOrdID
        Account: TradingAccount
        CreatedTime: DateTimeOffset // TODO: Change to vector clock
    }

    and Allocation = {
        From: OrderID
        To: OrderID
        Time: DateTimeOffset // TODO: Change to vector clock
        Quantity: Quantity
        Price: Price
    }
    and Order = {
        ID: OrderID
        Pos: Pos
        ExpiresAt: Pos
        OrderData: OrderData
        RemainingQuantity: Quantity
        AcceptenceTime: DateTimeOffset
        Allocations: Allocation list
    } with 
        member __.OrderType = __.OrderData.OrderType
        member __.Symbol = __.OrderData.Symbol
        member __.MarketSide = __.OrderData.MarketSide
        member __.ClOrdID = __.OrderData.ClOrdID
        member __.Account = __.OrderData.Account
        member __.OriginalQuantity = __.OrderData.Quantity
        member __.Price = match __.OrderData.OrderType with | Limit price -> price 
        member __.LastUpdateTime = if __.Allocations |> List.isEmpty then __.AcceptenceTime else __.Allocations.Head.Time
        member __.FilledQuantity = __.OriginalQuantity - __.RemainingQuantity
        member __.MatchType = if __.RemainingQuantity <= 0M<qty> then Full else Partial
        member __.FullyAllocated = __.MatchType = Full
        static member Create pos expiresAt orderData = 
            {   ID = newOrderID()
                Pos = pos
                ExpiresAt = expiresAt
                OrderData = orderData
                RemainingQuantity = orderData.Quantity
                AcceptenceTime = DateTimeOffset.UtcNow
                Allocations = [] }
        static member Allocate order1 order2 quantity = 
            let time = DateTimeOffset.UtcNow
            let allocate o1 o2 price =
                let allocation = { From = o1.ID; To = o2.ID; Time = time; Quantity = quantity; Price = price }
                let newOrder = { o1 with    RemainingQuantity = o1.RemainingQuantity - quantity
                                            Allocations = allocation :: o1.Allocations }
                let event = Matched (allocation, newOrder.MatchType)
                newOrder, event
            (allocate order1 order2 order1.Price), (allocate order2 order1 order1.Price)

        member __.HasBetterPriceThan (y: Order) = 
            if __.MarketSide <> y.MarketSide then failwith "Trying to compare prices for orders on different market sides"
            if __.MarketSide = Ask then __.Price < y.Price else __.Price > y.Price


    // type OrderMatch = {
    //     OrderID: OrderID
    //     Price: Price
    //     OriginalOrderQuantity: Quantity
    //     MatchedQuantity: Quantity
    //     RemainingQuantity: Quantity
    //     Symbol: Symbol
    //     MatchType: MatchType
    //     ClOrdID: ClOrdID
    //     MarketSide: MarketSide
    //     Account: TradingAccount
    // }

    type PriceBucket = {
        Price: Price
        OrderQueue: Order list
    }
    with 
        //member __.Update order = { __ with Orders = __.Orders |> Map.add order.ID order }
        member __.PopHead() = { __ with OrderQueue = __.OrderQueue.Tail  }
        member __.MatchFIFO order = 
            let rec matchFIFO o bucket events orders =
                if bucket.OrderQueue.IsEmpty || o.RemainingQuantity <= 0M<qty> then
                    o, bucket, o.RemainingQuantity, events, orders
                else 
                    let head = bucket.OrderQueue.Head
                    let remaining = min o.RemainingQuantity head.RemainingQuantity
                    let (head, e1), (o, e2) = Order.Allocate head o remaining
                    let events = e1 :: e2 :: events
                    if o.FullyAllocated then 
                        if head.FullyAllocated then 
                            o, (bucket.PopHead()), 0M<qty>, events, head :: orders
                        else
                            let newBucket = { bucket with OrderQueue = head :: __.OrderQueue.Tail }
                            o, newBucket, 0M<qty>, events, head :: orders
                    else
                        matchFIFO o (bucket.PopHead()) events (head :: orders)
            matchFIFO order __ [] []
        member __.RemoveExpired pos = __.OrderQueue 
                                        |> List.partition (fun o -> o.ExpiresAt < pos)
                                        |> fun (active, expired) -> if expired.IsEmpty then __, []
                                                                    else { __ with OrderQueue = active }, expired

        static member Create (order: Order) = 
            {   PriceBucket.Price = order.Price 
                OrderQueue = [ order ] }

    let rec private insertOrder (op: Price -> Price -> bool) order skipped remaining = 
        match remaining with
        | [] -> (PriceBucket.Create order :: skipped) |> List.rev
        | pb :: xs ->
            if pb.Price = order.Price then (skipped |> List.rev) @ ({ pb with OrderQueue = pb.OrderQueue @ [ order ] } :: xs)
            elif (op) pb.Price order.Price then insertOrder op order (pb :: skipped) xs
            else (skipped |> List.rev) @ ( (PriceBucket.Create order) :: remaining)

    let rec private matchOrder (op: Price -> Price -> bool) (order: Order) (matched: PriceBucket list) events orders (remaining: PriceBucket list) = 
        match remaining with
        | [] -> order, matched, events, order::orders, remaining
        | pb :: xs ->
            if op pb.Price order.Price then 
                let order, bucket, remainingQuantity, ets, fOrders = pb.MatchFIFO order
                let events = ets @ events
                let orders = fOrders @ orders
                let matched = bucket :: matched
                let remaining = xs
                if remainingQuantity > 0M<qty> then matchOrder op order matched events orders remaining
                else order, matched, events, orders, remaining
            else 
                order, matched, events, orders, remaining

    type OrderStack = {
        BidOrders: PriceBucket list
        AskOrders: PriceBucket list
        PriceStep: Price
    }
    with 
        member __.AddOrder (order: Order) =
                match order.MarketSide with
                | Bid -> 
                    let order, matched, events, fullOrders, remaining = matchOrder (<=) order [] [] [] __.AskOrders
                    let askOrders = matched (*???*) @ remaining |> List.filter (fun pb -> pb.OrderQueue |> List.isEmpty |> not)
                    if order.FullyAllocated then { __ with AskOrders = askOrders }, events, fullOrders
                    else 
                        let bidOrders = insertOrder (>) order [] __.BidOrders |> List.truncate 20 // TODO: Remove truncate
                        { __ with AskOrders = askOrders; BidOrders = bidOrders }, Created order :: events, fullOrders 
                | Ask -> 
                    let order, matched, events, fullOrders, remaining = matchOrder (>=) order [] [] [] __.BidOrders
                    let bidOrders = matched (*???*) @ remaining |> List.filter (fun pb -> pb.OrderQueue |> List.isEmpty |> not)
                    if order.FullyAllocated then { __ with BidOrders = bidOrders }, events, fullOrders
                    else 
                        let askOrders = insertOrder (<) order [] __.AskOrders |> List.truncate 20 // TODO: Remove truncate
                        { __ with AskOrders = askOrders; BidOrders = bidOrders }, Created order :: events, fullOrders 
        member __.RemoveExpired currentPos = 
            let removeExpired (buckets: PriceBucket list): PriceBucket list * Order list = 
                (   buckets 
                    |> List.map (fun b -> b.RemoveExpired currentPos) 
                    |> List.foldBack (fun (pb: PriceBucket, exp: Order list) (pbs: PriceBucket list, exps: Order list) -> 
                                        (pb :: pbs), (exp @ exps))) ([], [])  
            let bidActive, bidExpired = removeExpired __.BidOrders
            let askActive, askExpired = removeExpired __.AskOrders
            let expired = bidExpired @ askExpired 
            if expired.IsEmpty then __, []
            else { __ with  BidOrders = bidActive //|> List.filter (fun pb -> pb.OrderQueue |> List.isEmpty |> not)
                            AskOrders = askActive //|> List.filter (fun pb -> pb.OrderQueue |> List.isEmpty |> not) 
                            }, expired

        static member Create priceStep = { PriceStep = priceStep; BidOrders = []; AskOrders = [] }

    // let compactUnionJsonConverter =  FSharpLu.Json.CompactUnionJsonConverter()

    type OrderStackView = {
        BidOrders: PriceBucketView list
        AskOrders: PriceBucketView list
        PriceStep: Price
    }
    and PriceBucketView = {
        Price: Price
        Quantity: Quantity
        Sum: Total
        Total: Total
    }
    
    let toOrderStackView maxDepth (orderStack: OrderStack) = 
        let foldOrders orders = orders 
                                |> List.truncate maxDepth
                                |> List.mapFold (fun total pb -> 
                                                    let qty = pb.OrderQueue |> List.sumBy (fun o -> o.RemainingQuantity)
                                                    let sum = pb.Price * qty
                                                    {   Price = pb.Price
                                                        Quantity = qty
                                                        Sum = sum
                                                        Total = total + sum }, total + sum) 0M<price*qty> |> fst
        {   BidOrders = orderStack.BidOrders |> foldOrders 
            AskOrders = orderStack.AskOrders |> foldOrders
            PriceStep = orderStack.PriceStep
        }

    let orderData = {
        OrderType = Limit 5M<price>
        Symbol = Symbol "AVC"
        MarketSide = Bid
        Quantity = 10M<qty>
        ClOrdID = ClOrdID "1"
        Account = TradingAccount "TRA-1"
        CreatedTime = DateTimeOffset.UtcNow
    }

    let orderData2 = {
        OrderType = Limit 10M<price>
        Symbol = Symbol "AVC"
        MarketSide = Bid
        Quantity = 10M<qty>
        ClOrdID = ClOrdID "2"
        Account = TradingAccount "TRA-2"
        CreatedTime = DateTimeOffset.UtcNow
    }

    let aorderData = {
        OrderType = Limit 15M<price>
        Symbol = Symbol "AVC"
        MarketSide = Ask
        Quantity = 15M<qty>
        ClOrdID = ClOrdID "3"
        Account = TradingAccount "TRA-3"
        CreatedTime = DateTimeOffset.UtcNow
    }

    let aorderData2 = {
        OrderType = Limit 10M<price>
        Symbol = Symbol "AVC"
        MarketSide = Ask
        Quantity = 5M<qty>
        ClOrdID = ClOrdID "4"
        Account = TradingAccount "TRA-4"
        CreatedTime = DateTimeOffset.UtcNow
    }

    let aorderData3 = {
        OrderType = Limit 2M<price>
        Symbol = Symbol "AVC"
        MarketSide = Ask
        Quantity = 27M<qty>
        ClOrdID = ClOrdID "5"
        Account = TradingAccount "TRA-5"
        CreatedTime = DateTimeOffset.UtcNow
    }


    module Facade = 

        type SymbolStack = {
            Symbol: Symbol
            ExpirationPosLimit: Pos 
            Pos: Pos
            OrderStack: OrderStack
        }
        with static member Create symbol priceStep posLimit = { Symbol = symbol
                                                                ExpirationPosLimit = posLimit
                                                                Pos = 0UL
                                                                OrderStack = OrderStack.Create priceStep }

        type EventLogView<'T> = {
            GetCount: unit -> Async<uint64>
            GetPage: uint64 -> uint32 -> Async<'T[]>
            GetPageToken: uint64 -> uint32 -> Async<JwtToken<'T>[]>
            GetPageJwt: uint64 -> uint32 -> Async<string[]>
            GetLastPage: uint32 -> Async<'T[]>
            GetLastPageToken: uint32 -> Async<JwtToken<'T>[]>
            GetLastPageJwt: uint32 -> Async<string[]>
        }

        type EventLog<'T> = {
            OfferAsync: 'T -> Async<unit> // TODO: Add error handling
            View: EventLogView<'T>
        }
        

        type MatchingServiceLogs = {
            OrderCommands: EventLog<OrderCommand>
            OrderEvents: EventLog<OrderEvent>
            FullOrders: EventLog<Order>
        }

        type MatchingServiceSymbolLogs = Symbol -> MatchingServiceLogs

        type MatchingService(streams: MatchingServiceLogs, symbolStreams: MatchingServiceSymbolLogs, priceStep, posLimit) as __ =
            // let orderCommands = ResizeArray<OrderCommand>()
            // let fullOrders = ResizeArray<Order>()
            // let events = ResizeArray<OrderEvent>()
            let mutable orders = Map.empty<OrderID, Order>
            let mutable symbolStackMap = Map.empty<Symbol, SymbolStack>
            let findSymbolStack symbol = match symbolStackMap.TryFind symbol with
                                            | Some ss -> ss
                                            | None -> SymbolStack.Create symbol priceStep posLimit

            //orderStack = OrderStack.Create priceStep
            let processCommand command expireLimit = async {
                match command with
                | OrderCommand.Create order -> 
                    do! streams.OrderCommands.OfferAsync command
                    let symbolStack = findSymbolStack order.Symbol
                    let newPos = symbolStack.Pos + 1UL
                    let newOrderStack, evts, updatedOrders = order 
                                                                |> Order.Create newPos expireLimit // TODO: Sum with current position
                                                                |> symbolStack.OrderStack.AddOrder 
                    // let newOrderStack, expireOrders = newOrderStack.RemoveExpired newPos
                    // let expireEvts = [for o in expireOrders -> Expired(o.ID, o.Pos, newPos)]
                    // let evts = evts @ expireEvts
                    let newSymbolStack = { symbolStack with OrderStack = newOrderStack; Pos = newPos }
                    let symbolMsStreams = symbolStreams symbolStack.Symbol
                    for o in updatedOrders do 
                        orders <- orders.Add(o.ID, o)
                        if o.FullyAllocated then 
                            do! symbolMsStreams.FullOrders.OfferAsync o
                            do! streams.FullOrders.OfferAsync o
                    do! symbolMsStreams.OrderCommands.OfferAsync command
                    
                    let revEvents = evts |> List.rev
                    for re in revEvents do
                        do! symbolMsStreams.OrderEvents.OfferAsync re
                        do! streams.OrderEvents.OfferAsync re
                    symbolStackMap <- symbolStackMap.Add (newSymbolStack.Symbol, newSymbolStack)
                | OrderCommand.Cancel oid -> failwith "Not supported yet"
            }
            

            member __.SubmitOrder orderCommand: Async<unit> = processCommand orderCommand posLimit

            member __.MainSymbol = Symbol "AVC"
            member __.Symbols with get() = symbolStackMap |> Map.toSeq |> Seq.map fst |> Seq.filter(fun s -> s <> __.MainSymbol)
            member __.SymbolStrings = __.Symbols |> Seq.map(fun (Symbol s) -> s) |> Seq.toArray
            member __.OrderStack symbol: OrderStack = (findSymbolStack symbol).OrderStack
            member __.OrderStackView symbol maxDepth = (findSymbolStack symbol).OrderStack |> toOrderStackView maxDepth

            member __.OrderCommands = streams.OrderCommands.View
            member __.OrderEvents = streams.OrderEvents.View
            member __.FullOrders = streams.FullOrders.View

            member __.SymbolOrderCommands symbol = (symbolStreams symbol).OrderCommands.View //startIndex pageSize = getPage (symbol |> findSymbolStack).Commands startIndex pageSize
            member __.SymbolOrderEvents symbol = (symbolStreams symbol).OrderEvents.View
            member __.SymbolFullOrders symbol = (symbolStreams symbol).FullOrders.View

            member __.Orders (startIndex: uint64) (pageSize: uint32) = 
                async { return orders |> Seq.skip (int startIndex) |> Seq.truncate (int pageSize) |> Seq.map (fun kv -> kv.Value) |> Seq.toArray } // TODO: Find a less expensive way
            member __.OrderById orderID = orders |> Map.tryFind orderID

            member __.OrderById2 (orderID: string) = orders |> Map.toArray |> Array.map (fun kv -> (fst kv).ToString()) |> fun a -> orderID + " | " + String.Join(",", a)


            // static member Instance = MatchingService (1M<price>, 100UL, true)
