namespace Avalanchain.Common

module TradingBot = 
    open System

    open Avalanchain.Common.MatchingEngine
    open Avalanchain.Common.MatchingEngine.Facade
    let tradingBot(ms: MatchingService, symbols) = 
        let rnd = Random()
        let tradeStep lowCap highCap (dt: DateTime) (dtStep: TimeSpan) (symbols: string list) count = async {
            for i in 1 .. count do
                let timestamp = dt.Add(TimeSpan(dtStep.Ticks * int64(i))) |> DateTimeOffset
                for sym in symbols do 
                    let sym = Symbol sym
                    let quantity = decimal(rnd.Next(52, 100)) * 1M<qty>
                    let st = ms.OrderStack(sym)
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
                    do! ms.SubmitOrder orderCommand
        }
        async {
            do! tradeStep 100M<price> 400M<price> (DateTime.Today.AddHours 7.) (TimeSpan.FromSeconds 1.) symbols 20
            for i in 1 .. 10000000 do
                do! Async.Sleep 1000
                do! tradeStep 100M<price> 400M<price> (DateTime.Today.AddHours 7.) (TimeSpan.FromSeconds 1.) symbols 2
        }
