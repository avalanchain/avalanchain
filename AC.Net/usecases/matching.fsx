//#r "../packages/FSharpx.Collections/lib/net40/FSharpx.Collections.dll"

open System
open System.Collections.Generic
//open FSharpx.Collections

type MatchType = | Partial | Full
type MarketSide = | Bid | Ask
with member __.Opposite = if __ = Bid then Ask else Bid
  

type OrderID = Guid //uint64
let newOrderID = Guid.NewGuid

type ClOrdID = ClOrdID of string
type Price = decimal<price>
and [<Measure>] price
type Quantity = decimal<qty>
and [<Measure>] qty

type Symbol = Symbol of string
type TradingAccount = TradingAccount of string

type OrderType = 
    // | Market 
    | Limit of Price
    // | Stop 
    // | StopLimit

type OrderCommand = 
| Create of OrderData
| Cancel of OrderID

and OrderEvent =
| Created of Order
| CreationFailed of OrderData * Reason: string
| Matched of Allocation * MatchType
| Cancelled of OrderID
| CancellationFailed of OrderID * Reason: string

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
    static member Create orderData = {  ID = newOrderID()
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
    Orders: Map<OrderID, Order>
    OrderQueue: OrderID list
}
with 
    member __.Update order = { __ with Orders = __.Orders |> Map.add order.ID order }
    member __.PopHead() = 
        let head = __.OrderQueue.Head
        { __ with   Orders = __.Orders.Remove head
                    OrderQueue = __.OrderQueue.Tail  }
    member __.MatchFIFO order = 
        let rec matchFIFO o bucket events fullOrders =
            if bucket.OrderQueue.IsEmpty || o.RemainingQuantity <= 0M<qty> then
                o, bucket, o.RemainingQuantity, events, fullOrders
            else 
                let head = bucket.Orders.[bucket.OrderQueue.Head]
                let remaining = min o.RemainingQuantity head.RemainingQuantity
                let (head, e1), (o, e2) = Order.Allocate head o remaining
                let bucket = bucket.Update head
                let events = e1 :: e2 :: events
                if o.FullyAllocated then 
                    if head.FullyAllocated then 
                        o, (bucket.PopHead()), 0M<qty>, events, head :: fullOrders
                    else o, bucket, 0M<qty>, events, fullOrders
                else
                    matchFIFO o (bucket.PopHead()) events (head :: fullOrders)
        matchFIFO order __ [] []

    static member Create (order: Order) = 
        {   PriceBucket.Price = order.Price 
            Orders = [ order.ID, order ] |> Map.ofList
            OrderQueue = [ order.ID ] }

let rec insertOrder (op: Price -> Price -> bool) order skipped remaining = 
    match remaining with
    | [] -> 
        let pb = [PriceBucket.Create order]
        let sk = skipped |> List.rev
        sk @ pb
    | pb :: xs ->
        if pb.Price = order.Price then  [{ pb with  Orders = pb.Orders.Add(order.ID, order)
                                                    OrderQueue = pb.OrderQueue @ [ order.ID ] }]
        elif (op) pb.Price order.Price then insertOrder op order (pb :: skipped) xs
        else (skipped |> List.rev) @ ( (PriceBucket.Create order) :: remaining)

let rec matchOrder (op: Price -> Price -> bool) (order: Order) (matched: PriceBucket list) events fullOrders (remaining: PriceBucket list) = 
    match remaining with
    | [] -> order, matched, events, fullOrders, remaining
    | pb :: xs ->
        if op pb.Price order.Price then 
            let order, bucket, remainingQuantity, ets, fOrders = pb.MatchFIFO order
            let events = ets @ events
            let fullOrders = fOrders @ fullOrders
            let matched = bucket :: matched
            let remaining = xs
            if remainingQuantity > 0M<qty> then matchOrder op order matched events fullOrders remaining
            else order, matched, events, fullOrders, remaining
        else 
            order, matched, events, fullOrders, remaining


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
                    let bidOrders = insertOrder (>) order [] __.BidOrders
                    { __ with AskOrders = askOrders; BidOrders = bidOrders }, Created order :: events, fullOrders 
            | Ask -> 
                let order, matched, events, fullOrders, remaining = matchOrder (>=) order [] [] [] __.BidOrders
                let bidOrders = matched (*???*) @ remaining |> List.filter (fun pb -> pb.OrderQueue |> List.isEmpty |> not)
                if order.FullyAllocated then { __ with BidOrders = bidOrders }, events, fullOrders
                else 
                    let askOrders = insertOrder (<) order [] __.AskOrders
                    { __ with AskOrders = askOrders; BidOrders = bidOrders }, Created order :: events, fullOrders 
    static member Create priceStep = { PriceStep = priceStep; BidOrders = []; AskOrders = [] }


// let orderMatcher


let orderData = {
    OrderType = Limit 5M<price>
    Symbol = Symbol "AVC"
    MarketSide = Bid
    Quantity = 10M<qty>
    ClOrdID = ClOrdID "1"
    Account = TradingAccount "TRA1"
    CreatedTime = DateTimeOffset.UtcNow
}

let orderData2 = {
    OrderType = Limit 10M<price>
    Symbol = Symbol "AVC"
    MarketSide = Bid
    Quantity = 10M<qty>
    ClOrdID = ClOrdID "2"
    Account = TradingAccount "TRA1"
    CreatedTime = DateTimeOffset.UtcNow
}

let aorderData = {
    OrderType = Limit 15M<price>
    Symbol = Symbol "AVC"
    MarketSide = Ask
    Quantity = 15M<qty>
    ClOrdID = ClOrdID "3"
    Account = TradingAccount "TRA1"
    CreatedTime = DateTimeOffset.UtcNow
}

let aorderData2 = {
    OrderType = Limit 10M<price>
    Symbol = Symbol "AVC"
    MarketSide = Ask
    Quantity = 5M<qty>
    ClOrdID = ClOrdID "4"
    Account = TradingAccount "TRA1"
    CreatedTime = DateTimeOffset.UtcNow
}

let aorderData3 = {
    OrderType = Limit 2M<price>
    Symbol = Symbol "AVC"
    MarketSide = Ask
    Quantity = 27M<qty>
    ClOrdID = ClOrdID "4"
    Account = TradingAccount "TRA1"
    CreatedTime = DateTimeOffset.UtcNow
}

let order = Order.Create orderData
let order1 = Order.Create orderData
let order2 = Order.Create orderData2

let orders = insertOrder (>) order [] []
let orders1 = insertOrder (>) order1 [] orders
let orders2 = insertOrder (>) order2 [] orders1

let aorder = Order.Create aorderData
let aorder1 = Order.Create aorderData2
let aorder2 = Order.Create aorderData3

let aorders = insertOrder (<) aorder [] []
let aorders1 = insertOrder (<) aorder1 [] aorders

let mtch = matchOrder (>=) aorder2 [] [] [] orders2

let orderStack, events, fullOrders = (OrderStack.Create 5M<price>).AddOrder order
let orderStack1, events1, fullOrders1 = orderStack.AddOrder order1
let orderStack2, events2, fullOrders2 = orderStack1.AddOrder order2

let aorderStack1, aevents1, afullOrders1 = orderStack2.AddOrder aorder
let aorderStack2, aevents2, afullOrders2 = aorderStack1.AddOrder aorder1
let aorderStack3, aevents3, afullOrders3 = aorderStack2.AddOrder aorder2