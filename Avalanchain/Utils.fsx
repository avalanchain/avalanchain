#r "bin/Debug/FSharp.Core.Fluent-4.0.dll"
#r "bin/Debug/FSharpx.Extras.dll"
#r "bin/Debug/FSharpx.Async.dll"
#r "bin/Debug/FSharpx.Collections.dll"
#r "bin/Debug/Chessie.dll"
#r "bin/Debug/Newtonsoft.Json.dll"
#r "bin/Debug/FsPickler.dll"
#r "bin/Debug/FsPickler.Json.dll"
#r "bin/Debug/FSharp.Quotations.Evaluator.dll"
#r "bin/Debug/Base58Check.dll"

#load "SecPrimitives.fs"
#load "SecKeys.fs"
#load "RefsAndPathes.fs"
#load "Utils.fs"

open System
open System.Text
open System.Security.Cryptography

open Microsoft.FSharp.Quotations
open FSharp.Quotations.Evaluator
open Avalanchain

for i = 0 to 10000 do
    let f1 = <@ fun x -> x + 1 @>
    let sf1 = Utils.picklerSerializer(f1)
    let uf1 = Utils.picklerDeserializer<Expr<int -> int>>(sf1)
    uf1|> ignore
//let aa = (QuotationEvaluator.Evaluate uf1) 5
