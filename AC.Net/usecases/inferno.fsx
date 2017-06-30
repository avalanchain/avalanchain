#r "../packages/Inferno/lib/net452/SecurityDriven.Inferno.dll"

open System
open System.Security.Cryptography
open SecurityDriven.Inferno
open SecurityDriven.Inferno.Extensions

#time

let dsaKeyPrivate = CngKeyExtensions.CreateNewDsaKey()
let dsaKeyPrivateBlob = dsaKeyPrivate.GetPrivateBlob()
let dsaKeyPublicBlob = dsaKeyPrivate.GetPublicBlob()
let dsaKeyPublic: CngKey = dsaKeyPublicBlob.ToPublicKeyFromBlob()

use ecdsa = new ECDsaCng(dsaKeyPrivate) 
ecdsa.HashAlgorithm <- CngAlgorithm.Sha384 // generate DSA signature with private key

let rnd = Random();

let upperBound = 1000
let data = [| for i in 0 .. upperBound - 1 -> 
                let d = Array.create<byte> 100 0uy
                rnd.NextBytes(d)
                d |]

let sigs = [| for i in 0 .. upperBound - 1 -> ecdsa.SignData(data.[i]) |]
let verfs = [| for i in 0 .. upperBound - 1 -> ecdsa.VerifyData(data.[i], sigs.[i]) |]

