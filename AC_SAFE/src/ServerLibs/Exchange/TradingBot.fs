namespace Avalanchain.Exchange

module TradingBot = 
    open System
    open System.Threading.Tasks
    open FSharp.Control.Tasks
    open FSharp.Control.Tasks.ContextInsensitive

    open MatchingEngine

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
        
    let tradingBot(ms: MatchingService, symbols) = 
        let rnd = Random()
        let tradeStep lowCap highCap (dt: DateTime) (dtStep: TimeSpan) (symbols: Symbol list) count = 
            // [| for i in 1 .. count do
            //     let timestamp = dt.Add(TimeSpan(dtStep.Ticks * int64(i))) |> DateTimeOffset
            //     yield [|    for sym in symbols do 
            //                 let sym = Symbol sym
             
            //                 let orderCommand: OrderCommand = 
            //                     { orderData with    Symbol = sym 
            //                                         MarketSide = MarketSide.Ask
            //                                         OrderType = Limit 100.M<price>
            //                                         Quantity = 20.M<qty>
            //                                         CreatedTime = timestamp } |> OrderCommand.Create
            //                 yield (orderCommand |> ms.SubmitOrder :> Task)
             
            //                 let orderCommand: OrderCommand = 
            //                     { orderData with    Symbol = sym 
            //                                         MarketSide = MarketSide.Bid
            //                                         OrderType = Limit 100.M<price>
            //                                         Quantity = 20.M<qty>
            //                                         CreatedTime = timestamp } |> OrderCommand.Create 
            //                 yield (orderCommand |> ms.SubmitOrder :> Task) |] 
            //             |> Task.WhenAll
            // |] |> Task.WhenAll

            task {
                for i in 1 .. count do
                    let timestamp = dt.Add(TimeSpan(dtStep.Ticks * int64(i))) |> DateTimeOffset
                    for sym in symbols do 
                        let quantity = decimal(rnd.Next(52, 100)) * 1M<qty>
                        let! st = ms.OrderStack(sym)
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
                        let orderCommand: OrderCommand = 
                            { orderData with    Symbol = sym 
                                                MarketSide = side
                                                OrderType = Limit cappedPrice
                                                Quantity = quantity
                                                CreatedTime = timestamp } |> OrderCommand.Create 
                        let! res = ms.SubmitOrder orderCommand
                        match res with 
                        | Ok _ -> ()
                        | Error e -> printfn "Order submitted with an error: '%A'" e 
            }
        task {
            do! tradeStep 100M<price> 400M<price> (DateTime.Today.AddHours 7.) (TimeSpan.FromSeconds 1.) symbols 200
            for i in 1 .. 10000000 do
                do! Task.Delay 1
                do! tradeStep 100M<price> 400M<price> (DateTime.Today.AddHours 7.) (TimeSpan.FromSeconds 1.) symbols 2000
        }
