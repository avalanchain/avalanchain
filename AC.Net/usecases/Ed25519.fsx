#r "../packages/dlech.Chaos.NaCl/lib/net40/Chaos.NaCl.dll"

open System
open Chaos.NaCl

#time 

let rnd = Random();

let pk = Ed25519.PublicKeyFromSeed(Array.create<byte> 32 0uy)
let sk = Ed25519.ExpandedPrivateKeyFromSeed(Array.create<byte> 32 0uy)

let sign data = Ed25519.Sign(data, sk)

let verify data signature = Ed25519.Verify(signature, data, pk)

let perfTest upperBound = 
    let data = [| for i in 0 .. upperBound - 1 -> 
                    let d = Array.create<byte> 100 0uy
                    rnd.NextBytes(d)
                    d |]

    let sigs = [| for i in 0 .. upperBound - 1 -> sign(data.[i]) |]
    // let verfs = [| for i in 0 .. upperBound - 1 -> verify data.[i] sigs.[i] |]
    ()

let runs = [ for i in 0 .. 9 -> async { perfTest 100000 } ]
runs 
|> Async.Parallel
|> Async.RunSynchronously
