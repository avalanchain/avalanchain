#r "../packages/FSharp.Quotations.Evaluator.1.0.7/lib/net40/FSharp.Quotations.Evaluator.dll"

open Microsoft.FSharp.Quotations
open FSharp.Quotations.Evaluator
open System.Linq.Expressions
open System

type SerializableFunction<'T> = Expr<'T>

let mutable c = 0
for i = 0 to 1000 do
    let a = <@ 1 + 1 @>
    let b = QuotationEvaluator.Evaluate a
    c <- b

let mutable c1 = 0
for i = 0 to 1000 do
    let a = <@ 1 + 1 @>
    let b = QuotationEvaluator.CompileUntyped a
    c1 <- Convert.ToInt32(b)

let mutable c3 = 0
for i = 0 to 1000 do
    let a = <@ fun a b -> a + b @>
    let b = QuotationEvaluator.Evaluate a
    c3 <- b i (i + 1)
printfn "%A" c3

let mutable c31 = 0
let a1 = <@ fun a b -> a + b @>
let b1 = QuotationEvaluator.Evaluate a1
for i = 0 to 30000000 do
    c31 <- b1 i (i + 1)
printfn "%A" c31

let mutable c2 = 0
for i = 0 to 1000 do
    let a = <@ 1 + 1 @>
    let b = QuotationEvaluator.ToLinqExpression a
    let l = Expression.Lambda<Func<int>>(b).Compile()
    c2 <- l.Invoke()

let aa = <@ fun a b -> a + b @>
let bb = QuotationEvaluator.Evaluate aa