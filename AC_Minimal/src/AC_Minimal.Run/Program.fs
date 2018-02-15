// Learn more about F# at http://fsharp.org

open System
open avalanchain.Common

[<EntryPoint>]
let main argv =
    printfn "Hello World from F#!"

    let a = AC_Minimal.PaymentNetwork.PaymentAccountRef("addr")

    Console.ReadLine() |> ignore
    0 // return an integer exit code
