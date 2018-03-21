﻿namespace Avalanchain.Common
open Avalanchain.Core.PagedLog
open Proto.Persistence.Sqlite
open Microsoft.Data.Sqlite

module MatchingEngine = 

    open System
    open System.Threading
    open System.Collections.Generic
    open System.Collections.Concurrent

    open Proto
    open Proto.Persistence
    open Proto.FSharp

    open Avalanchain.Core
    //open FSharpx.Collections

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
        with member __.Value = match __ with TradingAccount ta -> ta

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
        ClOrdID = ClOrdID "5
        "
        Account = TradingAccount "TRA-5"
        CreatedTime = DateTimeOffset.UtcNow
    }


    module Facade = 
        open System.Linq

        type SymbolStack = {
            Symbol: Symbol
            ExpirationPosLimit: Pos 
            Pos: Pos
            OrderStack: OrderStack
            Commands: PID
            Events: PID
            FullOrders: PID
        }
        with static member Create symbol priceStep posLimit commands events fullOrders = {  Symbol = symbol
                                                                                            ExpirationPosLimit = posLimit
                                                                                            Pos = 0UL
                                                                                            OrderStack = OrderStack.Create priceStep
                                                                                            Commands = commands 
                                                                                            Events = events 
                                                                                            FullOrders = fullOrders }

        type MatchingService(provider: IProvider, priceStep, posLimit, runBot) as __ =

            let hasher = fun _ -> "Hash"
            let toToken = fun _ -> "Token"
            let maxPageSize = 200u

            let mutable symbolStackMap = Map.empty<Symbol, SymbolStack>
            let mutable orders = Map.empty<OrderID, Order>

            let spawnLog prefix = 
                PagedLog.pagedLogHandler hasher toToken maxPageSize provider prefix
                |> Actor.spawnPropsPrefix prefix

            let findSymbolStack (symbol: Symbol) = 
                let commands = spawnLog ("MatchingService_" + symbol.Value + "_Commands")
                let events = spawnLog ("MatchingService_" + symbol.Value + "_Events")
                let fullOrders = spawnLog ("MatchingService_" + symbol.Value + "_FullOrders")
                match symbolStackMap.TryFind symbol with
                | Some ss -> ss
                | None -> SymbolStack.Create symbol priceStep posLimit commands events fullOrders

            //orderStack = OrderStack.Create priceStep
            let orderCommands = spawnLog "MatchingService_Commands"
            let events = spawnLog "MatchingService_Events"
            let fullOrders = spawnLog "MatchingService_FullOrders"
            
            let processCommand command expireLimit = 
                match command with
                | OrderCommand.Create order -> 
                    command |> PagedLog.offer orderCommands
                    let symbolStack = findSymbolStack order.Symbol
                    let newPos = symbolStack.Pos + 1UL
                    let newOrderStack, evts, updatedOrders = order 
                                                                |> Order.Create newPos expireLimit
                                                                |> symbolStack.OrderStack.AddOrder 
                    // let newOrderStack, expireOrders = newOrderStack.RemoveExpired newPos
                    // let expireEvts = [for o in expireOrders -> Expired(o.ID, o.Pos, newPos)]
                    // let evts = evts @ expireEvts
                    let newSymbolStack = { symbolStack with OrderStack = newOrderStack; Pos = newPos }
                    for o in updatedOrders do 
                        orders <- orders.Add(o.ID, o)
                        if o.FullyAllocated then 
                            o |> PagedLog.offer newSymbolStack.FullOrders
                            o |> PagedLog.offer fullOrders
                    command |> PagedLog.offer newSymbolStack.Commands
                    
                    let revEvents = evts |> List.rev
                    for re in revEvents do 
                        re |> PagedLog.offer newSymbolStack.Events
                        re |> PagedLog.offer events
                    symbolStackMap <- symbolStackMap.Add (newSymbolStack.Symbol, newSymbolStack)
                | OrderCommand.Cancel oid -> failwith "Not supported yet"
            
            // TODO: Remove fakes:
            //do for o in [orderData; orderData2; aorderData; aorderData2; aorderData3] do 
            //    for sym in ["AVC"; "USD"; "EUR"; "GBP"; "QUOKA"; "V1"; "V2" ] do 
            //        { o with Symbol = Symbol sym } |> OrderCommand.Create |> processCommand 

            // do if initialize then
            //     let rnd = Random()
            //     for o in [orderData; orderData2; aorderData; aorderData2] do 
            //         for i in 0 .. rnd.Next(200, 2000) do
            //             for sym in ["AVC"; "USD"; "EUR"; "GBP"; "QUOKA"; "V1"; "V2"; "ICO1"; "ICO2"; "ICO3"; "ICO4"; "ICO5"; "ICO6"; "ICO7"; "ICO8"; "ICO9"; "ICO10"; "ICO11"; "ICO12" ] do 
            //                 { o with    Symbol = Symbol sym 
            //                             OrderType = Limit (decimal(rnd.Next(100, 400)) * 1M<price>)
            //                             Quantity = decimal(rnd.Next(2, 4000)) * 1M<qty>
            //                     } |> OrderCommand.Create |> fun c -> processCommand c posLimit
            ///
            let tradingBot(ms: MatchingService, symbols) = 
            // let ms = Facade.MatchingService(5M<price>, 10UL, false)
                let rnd = Random()
                let tradeStep lowCap highCap (dt: DateTime) (dtStep: TimeSpan) symbols count =
                    for i in 1 .. count do
                        let timestamp = dt.Add(TimeSpan(dtStep.Ticks * int64(i))) |> DateTimeOffset
                        for sym in symbols do 
                            let sym = Symbol sym
                            let quantity = decimal(rnd.Next(52, 100)) * 1M<qty>
                            let st = (findSymbolStack sym).OrderStack
                           
                            let medianPrice = decimal(((highCap - lowCap) / st.PriceStep) / 2M |> Math.Round) * st.PriceStep
                            let p, side = match st.BidOrders, st.AskOrders with
                                            | [], [] -> medianPrice, MarketSide.Ask
                                            | bb, aa -> 
                                                let bbl = bb |> List.length 
                                                let aal = aa |> List.length
                                                if bbl >= 7 && aal >= 7 then 
                                                    if aa.Head.Price - bb.Head.Price > 2M * st.PriceStep then 
                                                        if aa.Tail.Head.Price > medianPrice then aa.Head.Price - st.PriceStep, MarketSide.Ask
                                                        else bb.Head.Price + st.PriceStep, MarketSide.Bid
                                                    else
                                                        let aboveMedian = aa.Tail.Head.Price > medianPrice
                                                        if (rnd.NextDouble() > 0.30) then 
                                                            if aa.Tail.Head.Price > medianPrice then aa.Head.Price - st.PriceStep, MarketSide.Ask
                                                            else bb.Head.Price + st.PriceStep, MarketSide.Bid
                                                        elif bb.Tail.Head.Price < medianPrice then bb.Head.Price + st.PriceStep, MarketSide.Bid
                                                        else aa.Head.Price - st.PriceStep, MarketSide.Ask
                                                elif aal = 0 then (bb.Head.Price + st.PriceStep), MarketSide.Ask
                                                elif bbl = 0 then (aa.Head.Price - st.PriceStep), MarketSide.Bid
                                                elif bbl < aal then 
                                                    let newPrice =
                                                        if aa.Head.Price - bb.Head.Price > 2M * st.PriceStep then aa.Head.Price - st.PriceStep
                                                        else ((bb |> List.last).Price - st.PriceStep)
                                                    if newPrice < lowCap then lowCap + st.PriceStep, MarketSide.Bid
                                                    else newPrice, MarketSide.Bid
                                                else
                                                    let newPrice = 
                                                        if aa.Head.Price - bb.Head.Price > 2M * st.PriceStep then bb.Head.Price + st.PriceStep
                                                        else ((aa |> List.last).Price + st.PriceStep)
                                                    if newPrice > highCap then highCap - st.PriceStep, MarketSide.Ask
                                                    else newPrice, MarketSide.Ask

                            let cappedPrice =   if p < lowCap then lowCap + st.PriceStep
                                                elif p > highCap then highCap - st.PriceStep
                                                else p
                            // printfn "s p q: %A %A %A" side p quantity
                            { orderData with    Symbol = sym 
                                                MarketSide = side
                                                OrderType = Limit cappedPrice
                                                Quantity = quantity
                                                CreatedTime = timestamp
                                } |> OrderCommand.Create |> ms.SubmitOrder 
                async {
                    tradeStep 100M<price> 400M<price> (DateTime.Today.AddHours 7.) (TimeSpan.FromSeconds 1.) symbols 20
                    tradeStep 100M<price> 400M<price> (DateTime.Today.AddHours 7.) (TimeSpan.FromSeconds 1.) symbols 20
                    tradeStep 100M<price> 400M<price> (DateTime.Today.AddHours 7.) (TimeSpan.FromSeconds 1.) symbols 20
                    tradeStep 100M<price> 400M<price> (DateTime.Today.AddHours 7.) (TimeSpan.FromSeconds 1.) symbols 20
                    tradeStep 100M<price> 400M<price> (DateTime.Today.AddHours 7.) (TimeSpan.FromSeconds 1.) symbols 20
                    tradeStep 100M<price> 400M<price> (DateTime.Today.AddHours 7.) (TimeSpan.FromSeconds 1.) symbols 20
                    tradeStep 100M<price> 400M<price> (DateTime.Today.AddHours 7.) (TimeSpan.FromSeconds 1.) symbols 20
                    tradeStep 100M<price> 400M<price> (DateTime.Today.AddHours 7.) (TimeSpan.FromSeconds 1.) symbols 20
                    tradeStep 100M<price> 400M<price> (DateTime.Today.AddHours 7.) (TimeSpan.FromSeconds 1.) symbols 20
                    tradeStep 100M<price> 400M<price> (DateTime.Today.AddHours 7.) (TimeSpan.FromSeconds 1.) symbols 20
                    tradeStep 100M<price> 400M<price> (DateTime.Today.AddHours 7.) (TimeSpan.FromSeconds 1.) symbols 20
                    tradeStep 100M<price> 400M<price> (DateTime.Today.AddHours 7.) (TimeSpan.FromSeconds 1.) symbols 20
                    tradeStep 100M<price> 400M<price> (DateTime.Today.AddHours 7.) (TimeSpan.FromSeconds 1.) symbols 20
                    tradeStep 100M<price> 400M<price> (DateTime.Today.AddHours 7.) (TimeSpan.FromSeconds 1.) symbols 20
                    tradeStep 100M<price> 400M<price> (DateTime.Today.AddHours 7.) (TimeSpan.FromSeconds 1.) symbols 20

                    // for i in 1 .. 10000000 do
                    for i in 1 .. 10000 do
                        //do! Async.Sleep 1000
                        tradeStep 100M<price> 400M<price> (DateTime.Today.AddHours 7.) (TimeSpan.FromSeconds 1.) symbols 2
                }

            do if runBot then tradingBot(__, ["AVC"; "USD"; "EUR"; "GBP"; "AIM"; "V1"; "V2"; "ICO1"; "ICO2"; "ICO3"; "ICO4"; "ICO5"; "ICO6"; "ICO7"; "ICO8"; "ICO9"; "ICO10"; "ICO11"; "ICO12" ]) |> Async.Start

            
            member __.SubmitOrder orderCommand = processCommand orderCommand posLimit

            member __.MainSymbol = Symbol "AVC" 
            member __.Symbols with get() = symbolStackMap |> Map.toSeq |> Seq.map fst |> Seq.filter(fun s -> s <> __.MainSymbol)
            member __.SymbolStrings = __.Symbols |> Seq.map(fun (Symbol s) -> s) |> Seq.toArray
            member __.OrderStack symbol: Async<OrderStack> = async { return (findSymbolStack symbol).OrderStack }
            member __.OrderStackView symbol maxDepth = async { return (findSymbolStack symbol).OrderStack |> toOrderStackView maxDepth }

            member __.OrderCommands startIndex pageSize = orderCommands |> getPage startIndex pageSize
            member __.OrderEvents startIndex pageSize = events |> getPage startIndex pageSize
            member __.FullOrders startIndex pageSize = fullOrders |> getPage startIndex pageSize

            member __.LastOrderCommands pageSize = orderCommands |> getLastPage pageSize
            member __.LastOrderEvents pageSize = events |> getLastPage pageSize
            member __.LastFullOrders pageSize = fullOrders |> getLastPage pageSize

            member __.OrderCommandsCount with get() = orderCommands |> getPos
            member __.OrderEventsCount with get() = events |> getPos
            member __.FullOrdersCount with get() = fullOrders |> getPos

            member __.SymbolOrderCommands symbol startIndex pageSize = (symbol |> findSymbolStack).Commands |> getPage startIndex pageSize
            member __.SymbolOrderEvents symbol startIndex pageSize = (symbol |> findSymbolStack).Events |> getPage startIndex pageSize
            member __.SymbolFullOrders symbol startIndex pageSize = (symbol |> findSymbolStack).FullOrders |> getPage startIndex pageSize

            member __.SymbolLastOrderCommands symbol pageSize = (symbol |> findSymbolStack).Commands |> getLastPage pageSize
            member __.SymbolLastOrderEvents symbol pageSize = (symbol |> findSymbolStack).Events |> getLastPage pageSize
            member __.SymbolLastFullOrders symbol pageSize = (symbol |> findSymbolStack).FullOrders |> getLastPage pageSize

            member __.SymbolOrderCommandsCount symbol = (symbol |> findSymbolStack).Commands |> getPos
            member __.SymbolOrderEventsCount symbol = (symbol |> findSymbolStack).Events |> getPos
            member __.SymbolFullOrdersCount symbol = (symbol |> findSymbolStack).FullOrders |> getPos

            member __.Orders (startIndex: uint64) (pageSize: uint32) = async { return orders |> Seq.skip (int startIndex) |> Seq.truncate (int pageSize) |> Seq.map (fun kv -> kv.Value) |> Seq.toArray } // TODO: Find a less expensive way 
            member __.OrderById orderID = async { return orders |> Map.tryFind orderID }

            // member __.OrderById2 (orderID: string) = orders |> Map.toArray |> Array.map (fun kv -> (fst kv).ToString()) |> fun a -> orderID + " | " + String.Join(",", a)


            static member Instance = MatchingService ((new SqliteProvider(new SqliteConnectionStringBuilder ( DataSource = "datasource.db" ))), 1M<price>, 100UL, true)
