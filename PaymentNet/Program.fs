// Learn more about F# at http://fsharp.org

open System
open System.Security.Cryptography.X509Certificates
open avalanchain.Common.x509

[<EntryPoint>]
let main argv =
    printfn "Hello World from F#!"

    let keyPair = generateKeys()

    let cert = generateCertificate "cert1" keyPair

    let certPem = cert |> toPem
    let privPem = keyPair.Private |> toPem
    printfn "Cert %A" (certPem)

    printfn "Cert %A" (cert.GetPublicKey() |> toPem)

    printfn "Priv %A" (privPem)

    let cert2 = new X509Certificate2(certPem |> Text.Encoding.UTF8.GetBytes)

    let pubKey = cert2.GetECDsaPublicKey()

    printfn "Cert2 %A" (cert2)
    printfn "pubKey %A" (pubKey)

    let cert2 = new X509Certificate2((certPem + "\n" + privPem) |> Text.Encoding.UTF8.GetBytes)

    printfn "Cert2 %A" (cert2)
    printfn "PrivKey %A" (cert2.GetECDsaPrivateKey())


    //let pa = {}

    //let pat = ECToken(pa, )

    Console.ReadLine() |> ignore

    0 // return an integer exit code
