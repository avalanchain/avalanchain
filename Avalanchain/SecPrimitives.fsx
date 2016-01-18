#r "../packages/FSharp.Core.Fluent-4.0.1.0.0.5/lib/portable-net45+netcore45+wpa81+wp8+MonoAndroid1+MonoTouch1/FSharp.Core.Fluent-4.0.dll"
#r "../packages/FSharpx.Extras.1.10.3/lib/40/FSharpx.Extras.dll"
#r "../packages/FSharpx.Async.1.12.0/lib/net40/FSharpx.Async.dll"
#r "../packages/FSharpx.Collections.1.13.4/lib/net40/FSharpx.Collections.dll"

#load "SecPrimitives.fs"

open Avalanchain

let hash0 = [| for i in 0uy .. 5uy -> i |]
let hash1 = [| for i in 10uy .. 15uy -> i |]
let hash2 = [| for i in 20uy .. 25uy -> i |]
let hash3 = [| for i in 30uy .. 35uy -> i |]
let hash4 = [| for i in 40uy .. 45uy -> i |]

let hasher = (fun h -> Hash h)
let serializer = (fun h -> h)

let zeroMerkle = [] |> SecPrimitives.toMerkle serializer hasher None
let oneMerkle = [ hash0 ] |> SecPrimitives.toMerkle serializer hasher None
let twoMerkle = [ hash0; hash1 ] |> SecPrimitives.toMerkle serializer hasher None
let threeMerkle = [ hash0; hash1; hash2 ] |> SecPrimitives.toMerkle serializer hasher None
let fourMerkle = [ hash0; hash1; hash2; hash3 ] |> SecPrimitives.toMerkle serializer hasher None
let fiveMerkle = [ hash0; hash1; hash2; hash3; hash4 ] |> SecPrimitives.toMerkle serializer hasher None



