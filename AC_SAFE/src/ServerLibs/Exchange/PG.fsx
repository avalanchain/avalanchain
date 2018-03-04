#r @"System.dll"
#r @"System.Core.dll"
#r @"..\packages\Newtonsoft.Json.9.0.1\lib\net45\Newtonsoft.Json.dll"

#load "FSharpLu.Json.Helpers.fs"
#load "FSharpLu.Json.WithFunctor.fs"
#load "FSharpLu.Json.Default.fs"
#load "FSharpLu.Json.Compact.fs"
#load "FSharpLu.Json.BackwardCompatible.fs"

#load "MatchingEngine.fs"

#time

open System
open FSharpLu.Json

open avalanchain.Common.MatchingEngine

let toBid p q = { orderData with OrderType = Limit p; Quantity = q }
let toAsk p q = { aorderData with OrderType = Limit p; Quantity = q }
let toPQ o = o.Price, o.Quantity
let toLPQ = List.map toPQ

let orders (bm, bc) (am, ac) = 
    [for i in bm .. 1M .. bc -> toBid (100M<price> - i * 5M<price>) 10M<qty> ]
    @ [for i in am .. 1M .. ac -> toAsk (100M<price> + i * 5M<price>) 10M<qty> ]

let test ords (expBid, expAsk) =
    let sym = Symbol "AVC"
    let ms = Facade.MatchingService(5M<price>, 100UL, false)
    ords
    |> List.map OrderCommand.Create
    |> List.iter ms.SubmitOrder

    //ms.OrderStack(sym)
    let stackView = ms.OrderStackView sym 10
    let bids = stackView.BidOrders |> toLPQ
    let asks = stackView.AskOrders |> toLPQ
    let retb, reta = bids = expBid, asks = expAsk
    if not retb then 
        printfn "Bid View: %A" bids
        printfn "Bid Det View: %A" stackView.BidOrders
        printfn "Bid Orders: %A" ((ms.OrderStack sym).BidOrders)
    if not reta then 
        printfn "Ask View: %A" bids
        printfn "Ask Det View: %A" stackView.AskOrders
        printfn "Ask Orders: %A" ((ms.OrderStack sym).AskOrders)
    retb, reta

let blankB1 = [ 95M<price>, 10M<qty> ]

let blankA1 = [ 105M<price>, 10M<qty> ]

let blankB2 = [ 95M<price>, 10M<qty>
                90M<price>, 10M<qty> ]

let blankA2 = [ 105M<price>, 10M<qty>
                110M<price>, 10M<qty> ]

let blankB5 = [ 95M<price>, 10M<qty>
                90M<price>, 10M<qty>
                85M<price>, 10M<qty>
                80M<price>, 10M<qty>
                75M<price>, 10M<qty> ]

let blankA5 = [ 105M<price>, 10M<qty>
                110M<price>, 10M<qty>
                115M<price>, 10M<qty>
                120M<price>, 10M<qty>
                125M<price>, 10M<qty> ]

let test0 = test [] ([], [])
let test5 = test (orders (1M, 5M) (1M, 5M)) (blankB5, blankA5) 

let testBidEmpty = test (orders (0M, -1M) (1M, 5M)) ([], blankA5) 
let testAskEmpty = test (orders (1M, 5M) (0M, -1M)) (blankB5, []) 

let testAskFull = test [toBid 105M<price> 10M<qty>
                        toAsk 100M<price> 10M<qty> ] ([], []) 

let testAskFull2 = test [   toBid 105M<price> 10M<qty>
                            toAsk 105M<price> 10M<qty> ] ([], []) 

let testAskOver = test [toBid 105M<price> 10M<qty>
                        toAsk 100M<price> 20M<qty> ] ([], [100M<price>, 10M<qty>]) 

let testAskOver2 = test [   toBid 105M<price> 10M<qty>
                            toAsk 110M<price> 20M<qty> 
                            toAsk 100M<price> 15M<qty> ] ([], [ 100M<price>, 5M<qty>
                                                                110M<price>, 20M<qty>])                         

let testAskTopup = test [   toBid 100M<price> 10M<qty> 
                            toAsk 105M<price> 20M<qty>
                            toAsk 105M<price> 20M<qty> ] ([100M<price>, 10M<qty>], [105M<price>, 40M<qty>]) 

let testAskTopup2 = test [  toBid 95M<price> 10M<qty> 
                            toAsk 100M<price> 20M<qty>
                            toAsk 110M<price> 20M<qty> 
                            toAsk 105M<price> 10M<qty> ] ([95M<price>, 10M<qty>], [ 100M<price>, 20M<qty>
                                                                                    105M<price>, 10M<qty>
                                                                                    110M<price>, 20M<qty>
                                                                                    ]) 

let testBidFull = test [toAsk 100M<price> 10M<qty> 
                        toBid 105M<price> 10M<qty> ] ([], []) 

let testBidFull2 = test [   toAsk 105M<price> 10M<qty>
                            toBid 105M<price> 10M<qty> ] ([], []) 

let testBidOver = test [toAsk 100M<price> 10M<qty> 
                        toBid 105M<price> 20M<qty> ] ([105M<price>, 10M<qty>], []) 

let testBidTopup = test [   toAsk 105M<price> 10M<qty> 
                            toBid 100M<price> 20M<qty>
                            toBid 100M<price> 20M<qty> ] ([100M<price>, 40M<qty>], [105M<price>, 10M<qty>]) 

let testBidTopup2 = test [  toAsk 105M<price> 10M<qty> 
                            toBid 100M<price> 20M<qty>
                            toBid 90M<price> 20M<qty> 
                            toBid 95M<price> 10M<qty> ] ([  100M<price>, 20M<qty>
                                                            95M<price>, 10M<qty>
                                                            90M<price>, 20M<qty>
                                                            ], [105M<price>, 10M<qty>]) 
                            
let testBidIssue = test [   toBid 570M<price> 27M<qty> 
                            toBid 580M<price> 45M<qty>
                            toBid 570M<price> 57M<qty> ] ([ 580M<price>, 45M<qty>
                                                            570M<price>, 84M<qty>
                                                            ], [])


let testInnerIssue = test [ toBid 270M<price> 60000M<qty> 
                            toBid 260M<price> 240000M<qty>
                            toBid 258M<price> 9195M<qty> 
                            toAsk 330M<price> 361721M<qty> 
                            toAsk 350M<price> 106159M<qty> 
                            toAsk 370M<price> 56445M<qty> 
                            toBid 350M<price> 30000M<qty>
                                                         ] ([   270M<price>, 60000M<qty> 
                                                                260M<price>, 240000M<qty>
                                                                258M<price>, 9195M<qty> 
                                                                ], [
                                                                    330M<price>, 331721M<qty> 
                                                                    350M<price>, 106159M<qty> 
                                                                    370M<price>, 56445M<qty>
                                                                ])


let ms = Facade.MatchingService(5M<price>, 10UL, false)
let rnd = Random()
let tradeStep lowCap highCap (dt: DateTime) (dtStep: TimeSpan) symbols count =
    for i in 1 .. count do
        let timestamp = dt.Add(TimeSpan(dtStep.Ticks * int64(i))) |> DateTimeOffset
        for sym in symbols do 
            let sym = Symbol sym
            let quantity = decimal(rnd.Next(52, 100)) * 1M<qty>
            let st = ms.OrderStack(sym)
            let p, side = match st.BidOrders, st.AskOrders with
                            | [], [] -> decimal(((highCap - lowCap) / st.PriceStep) / 2M |> Math.Round) * st.PriceStep, MarketSide.Ask
                            | bb, aa -> 
                                let bbl = bb |> List.length 
                                let aal = aa |> List.length
                                if bbl >= 7 && aal >= 7 then 
                                    if rnd.NextDouble() < 0.5 then aa.Tail.Head.Price, MarketSide.Bid
                                    else bb.Tail.Head.Price, MarketSide.Ask
                                elif aal = 0 then (bb.Head.Price + st.PriceStep), MarketSide.Ask
                                elif bbl = 0 then (aa.Head.Price - st.PriceStep), MarketSide.Bid
                                elif bbl < aal then 
                                    let newPrice = ((bb |> List.last).Price - st.PriceStep)
                                    if newPrice < lowCap then lowCap + st.PriceStep, MarketSide.Bid
                                    else newPrice, MarketSide.Bid
                                else
                                    let newPrice = ((aa |> List.last).Price + st.PriceStep)
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
                                Account = TradingAccount("TRA-" + (rnd.Next(5) + 1).ToString())
                } |> OrderCommand.Create |> ms.SubmitOrder 

tradeStep 100M<price> 400M<price> (DateTime.Today.AddHours 7.) (TimeSpan.FromSeconds 1.) [ "AVC" ] 100
let st = ms.OrderStackView(Symbol "AVC") 100
st.BidOrders.Length
st.AskOrders.Length






// let ms = Facade.MatchingService(5M<price>, 10UL, false)
// let rnd = Random()
// //for i in 0 .. rnd.Next(200, 2000) do
// let tradeStep lowCap highCap (dt: DateTime) (dtStep: TimeSpan) symbols count =
//     for i in 0 .. count do
//         let timestamp = dt.Add(TimeSpan(dtStep.Ticks * int64(i))) |> DateTimeOffset
//         for sym in symbols do 
//             let sym = Symbol sym
//             let side = if rnd.NextDouble() < 0.5 then MarketSide.Bid else MarketSide.Ask
//             let quantity = decimal(rnd.Next(52, 100)) * 1M<qty>
//             let st = ms.OrderStack(sym)
//             let p = match side, st.BidOrders, st.AskOrders with
//                     | _, [], [] -> decimal(((highCap - lowCap) / st.PriceStep) / 2M |> Math.Round) * st.PriceStep
//                     | MarketSide.Bid, b, [] -> (b.Head.Price + decimal(rnd.Next(6) - 3) * st.PriceStep)
//                     | MarketSide.Bid, _, a -> 
//                         let r = rnd.NextDouble() 
//                         if r < 0.5 then a.Head.Price else a.Head.Price + decimal(r) * st.PriceStep
//                     | MarketSide.Ask, [], a -> (a.Head.Price + decimal(rnd.Next(6) - 3) * st.PriceStep)
//                     | MarketSide.Ask, b, _ -> 
//                         let r = rnd.NextDouble() 
//                         if r < 0.5 then b.Head.Price else b.Head.Price - decimal(r) * st.PriceStep
//             let capedSide = 
//                 if p < lowCap then MarketSide.Ask
//                 elif (p > highCap) then MarketSide.Bid 
//                 else side
                            
//             // printfn "s p q: %A %A %A" side p quantity
//             { orderData with    Symbol = sym 
//                                 MarketSide = capedSide
//                                 OrderType = Limit p
//                                 Quantity = quantity
//                                 CreatedTime = timestamp
//                 } |> OrderCommand.Create |> ms.SubmitOrder 

// tradeStep 100M<price> 300M<price> (DateTime.Today.AddHours 7.) (TimeSpan.FromSeconds 1.) [ "AVC" ] 10000
// let st = ms.OrderStackView(Symbol "AVC") 100
// st.BidOrders.Length
// st.AskOrders.Length

// let stt = ms.OrderStack(Symbol "AVC") 





// let ms = Facade.MatchingService(5M<price>, 100UL, false)
// let rnd = Random()
// for o in [orderData; orderData2; aorderData; aorderData2] do 
//     //for i in 0 .. rnd.Next(200, 2000) do
//     for i in 0 .. 100 do
//         for sym in ["AVC" ] do 
//             { o with    Symbol = Symbol sym 
//                         OrderType = Limit (decimal(rnd.Next(100, 400)) * 1M<price>)
//                         Quantity = decimal(rnd.Next(2, 100)) * 1M<qty>
//                 } |> OrderCommand.Create |> ms.SubmitOrder 

// let st = ms.OrderStackView(Symbol "AVC") 100
// st.BidOrders.Length