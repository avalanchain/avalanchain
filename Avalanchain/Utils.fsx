#r "../packages/FSharp.Core.Fluent-4.0.1.0.0.5/lib/portable-net45+netcore45+wpa81+wp8+MonoAndroid1+MonoTouch1/FSharp.Core.Fluent-4.0.dll"
#r "../packages/FSharpx.Extras.1.10.3/lib/40/FSharpx.Extras.dll"
#r "../packages/FSharpx.Async.1.12.0/lib/net40/FSharpx.Async.dll"
#r "../packages/FSharpx.Collections.1.10.1/lib/net40/FSharpx.Collections.dll"
#r "../packages/Chessie.0.2.2/lib/net40/Chessie.dll"
#r "../packages/Newtonsoft.Json.6.0.5/lib/net45/Newtonsoft.Json.dll"
#r "../packages/FsPickler.1.7.1/lib/net45/FsPickler.dll"
#r "../packages/FsPickler.Json.1.7.1/lib/net45/FsPickler.Json.dll"
#r "../packages/FSharp.Quotations.Evaluator.1.0.7/lib/net40/FSharp.Quotations.Evaluator.dll"

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
