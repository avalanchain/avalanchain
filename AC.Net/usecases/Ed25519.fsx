#r "../packages/dlech.Chaos.NaCl/lib/net40/Chaos.NaCl.dll"

open System
open Chaos.NaCl

#time 

let rnd = Random();

let pk = Ed25519.PublicKeyFromSeed(Array.create<byte> 32 0uy)
let sk = Ed25519.ExpandedPrivateKeyFromSeed(Array.create<byte> 32 0uy)

let sign data = Ed25519.Sign(data, sk)

let verify (data, signature) = Ed25519.Verify(signature, data, pk)

let genTestData upperBound = 
    [|  for i in 0 .. upperBound - 1 -> 
        let d = Array.create<byte> 100 0uy
        rnd.NextBytes(d)
        d |]

let signAll = Array.map sign

[ for i in 0 .. 9 -> async { perfTest 100000 } ]
let verifyAll data sigs = 
    Array.zip data sigs
    |> Array.map verify

let data = genTestData 100000
let sigs = signAll data

let verifs = verifyAll data sigs


let runs = [ for i in 0 .. 9 -> async { verifyAll data sigs |> ignore } ]
|> Async.Parallel
|> Async.RunSynchronously
