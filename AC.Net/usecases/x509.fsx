#r "../packages/BouncyCastle/lib/BouncyCastle.Crypto.dll"

open System
open Org.BouncyCastle.Asn1.X509
open Org.BouncyCastle.Crypto
open Org.BouncyCastle.Crypto.Parameters
open Org.BouncyCastle.Security
open Org.BouncyCastle.Math
open Org.BouncyCastle.Crypto.Prng
open Org.BouncyCastle.Crypto.Generators
open Org.BouncyCastle.Pkcs
open Org.BouncyCastle.X509

#time

let gen = X509V3CertificateGenerator()
let certName = X509Name("CN=PickAName")
let serialNo = BigInteger.ProbablePrime(120, new Random())

gen.SetSerialNumber(serialNo)
gen.SetSubjectDN(certName)
gen.SetIssuerDN(certName)
gen.SetNotAfter(DateTime.Now.AddYears(100))
gen.SetNotBefore(DateTime.Now.Subtract(TimeSpan(7, 0, 0, 0)))
gen.SetSignatureAlgorithm("SHA384withECDSA")


open System.IO
let generateKeys (keySize: uint32): AsymmetricCipherKeyPair =
    //using ECDSA algorithm for the key generation
    let gen = Org.BouncyCastle.Crypto.Generators.ECKeyPairGenerator("ECDSA")

    //Creating Random
    let secureRandom = SecureRandom()

    //Parameters creation using the random and keysize
    let keyGenParam = KeyGenerationParameters(secureRandom, keySize |> int)

    //Initializing generation algorithm with the Parameters--This method Init i modified
    gen.Init(keyGenParam)

    //Generation of Key Pair
    gen.GenerateKeyPair()

let toPem (key: AsymmetricKeyParameter) = 
    use textWriter = new StringWriter()
    let pemWriter = Org.BouncyCastle.OpenSsl.PemWriter(textWriter)
    pemWriter.WriteObject(key)
    pemWriter.Writer.Flush()
    pemWriter.ToString()

let generatePKeys (intSize: uint32) =
    //Generating p-128 keys 128 specifies strength
    let keyPair = generateKeys(intSize)

    let privateKey = keyPair.Private |> toPem
    let privateKeyParam = keyPair.Private :?> ECPrivateKeyParameters
    let publicKeyParam = keyPair.Public :?> ECPublicKeyParameters
    let publicKey = keyPair.Public |> toPem

    printfn "Private Key: '%s'" privateKey // TODO: Remove this
    printfn "Private Key Param: '%s'" (privateKeyParam.D.ToString())
    printfn "Public Key: '%s'" publicKey // TODO: Remove this
    printfn "Public Key Param X: '%s'" (publicKeyParam.Q.X.ToBigInteger().ToString())
    printfn "Public Key Param Y '%s'" (publicKeyParam.Q.Y.ToBigInteger().ToString())
    keyPair


//let bcKeys = Org.BouncyCastle.Security.DotNetUtilities.GetKeyPair(generatePKeys())
// Org.BouncyCastle.Crypto.Asn1.CreateKey(SubjectPublicKeyInfo.GetInstance(Asn1Object.FromByteArray(keyInfoData)))
let bcKeys = generatePKeys 384u

gen.SetPublicKey(bcKeys.Public)

let cert = gen.Generate(bcKeys.Private)

let store = Pkcs12Store()
let friendlyName = cert.IssuerDN.ToString()
let entry = X509CertificateEntry(cert)
store.SetCertificateEntry(friendlyName, entry)
store.SetKeyEntry(friendlyName, AsymmetricKeyEntry(bcKeys.Private), [| entry |])
let storeFile = IO.File.OpenWrite("X509.store")
store.Save(storeFile, Seq.toArray "A password here", SecureRandom(CryptoApiRandomGenerator()))
storeFile.Close()


let store2 = Pkcs12Store()
store.Load(IO.File.OpenRead("X509.store"), Seq.toArray "A password here")
store.GetCertificateChain friendlyName
