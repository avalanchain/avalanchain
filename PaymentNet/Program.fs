// Learn more about F# at http://fsharp.org

open System
open System.Security.Cryptography
open System.Security.Cryptography.X509Certificates
open avalanchain.Common.x509

open Org.BouncyCastle.Pkcs
open Org.BouncyCastle.Crypto.Parameters

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

    // let cert2 = new X509Certificate2((certPem + "\n" + privPem) |> Text.Encoding.UTF8.GetBytes)

    // printfn "Cert2 %A" (cert2)
    // printfn "PrivKey %A" (cert2.GetECDsaPrivateKey())

    let fileName = "X509.store"
    let friendlyName = "cert1Store"
    let password = "password"
    storeCertificate fileName friendlyName password cert keyPair
    let cert3 = loadCertificate fileName friendlyName password 
    printfn "Cert3 %A" (cert3)

    let cert4 = new X509Certificate2(loadCertificateFile fileName, password)
    printfn "Cert4 %A" (cert4)
    printfn "Cert4.HasPrivateKey %A" (cert4.HasPrivateKey)
    printfn "Cert4.Pub %A" ((cert4.GetECDsaPublicKey() :?> ECDsaCng).Key.Export(CngKeyBlobFormat.EccPublicBlob))
    printfn "Cert4.Pub %A" ((cert4.GetECDsaPublicKey() :?> ECDsaCng).Key.Export(CngKeyBlobFormat.EccFullPublicBlob))
    printfn "Cert4.Pub %A" ((cert4.GetECDsaPublicKey() :?> ECDsaCng).Key.Export(CngKeyBlobFormat.GenericPublicBlob))
    printfn "Cert4.Params %A" ((cert4.GetKeyAlgorithmParametersString()))
    //printfn "Cert4.Priv %A" (cert4.GetECDsaPrivateKey())

    let bcPKInfo = PrivateKeyInfoFactory.CreatePrivateKeyInfo(keyPair.Private).GetDerEncoded()
    printfn "Cert4.Priv %A" (bcPKInfo)

    let eccKeys (keyPair: Org.BouncyCastle.Crypto.AsymmetricCipherKeyPair) = 
        let d = (keyPair.Private :?> ECPrivateKeyParameters).D.ToByteArray() |> Array.skip 1
        let pubKey = keyPair |> publicKeyParam
        let x = pubKey.Q.AffineXCoord.ToBigInteger().ToByteArray()
        let y = pubKey.Q.AffineXCoord.ToBigInteger().ToByteArray()
        printfn "%A" ((keyPair.Private :?> ECPrivateKeyParameters).D)
        printfn "%A" (pubKey.Q.AffineXCoord.ToBigInteger())
        printfn "%A" (pubKey.Q.AffineYCoord.ToBigInteger())
        printfn "%d %d %d" x.Length y.Length d.Length
        
        //Security.Cryptography.EccKey.New(x, y, d, CngKeyUsages.Signing)
        CngKey.Create(CngAlgorithm.ECDsaP384)
        
    
    printfn "cng: %A" (eccKeys keyPair)

    X509CertificateBuilder

    //let cngKeyPrivate = CngKey.Import((keyPair.Private :?> ECPrivateKeyParameters).D, CngKeyBlobFormat.Pkcs8PrivateBlob)
    //printfn "cngKeyPrivate %A" (cngKeyPrivate)

    //let pa = {}

    //let pat = ECToken(pa, )

    Console.ReadLine() |> ignore

    0 // return an integer exit code
