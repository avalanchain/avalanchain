#r "../packages/Inferno/lib/net452/SecurityDriven.Inferno.dll"

open System
open System.Security.Cryptography
open SecurityDriven.Inferno
open SecurityDriven.Inferno.Extensions

#time

let sha256 min max = 
    let sha = SHA256Managed.Create()
    for i in min .. max do
        let v = sprintf "value: %s" (i.ToString())
        let hash = sha.ComputeHash(Text.ASCIIEncoding.UTF8.GetBytes v) |> Convert.ToBase64String
        hash |> ignore

let sha512 min max = 
    let sha = SHA512Managed.Create()
    for i in min .. max do
        let v = sprintf "value: %s" (i.ToString())
        let hash = sha.ComputeHash(Text.ASCIIEncoding.UTF8.GetBytes v) |> Convert.ToBase64String
        hash |> ignore

let sha384 min max = 
    let sha = SHA384Managed.Create()
    for i in min .. max do
        let v = sprintf "value: %s" (i.ToString())
        let hash = sha.ComputeHash(Text.ASCIIEncoding.UTF8.GetBytes v) |> Convert.ToBase64String
        hash |> ignore

sha256 0 9999
sha384 0 9999
sha512 0 9999

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

