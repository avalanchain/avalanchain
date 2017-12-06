namespace Avalanchain

module x509 =
    open System
    open System.IO
    open Org.BouncyCastle.Asn1.X509
    open Org.BouncyCastle.Crypto
    open Org.BouncyCastle.Crypto.Parameters
    open Org.BouncyCastle.Security
    open Org.BouncyCastle.Math
    open Org.BouncyCastle.Crypto.Prng
    open Org.BouncyCastle.Crypto.Generators
    open Org.BouncyCastle.Crypto.Operators
    open Org.BouncyCastle.Pkcs
    open Org.BouncyCastle.X509

    open System.Security.Cryptography
    open System.Security.Cryptography.X509Certificates

    let gen = X509V3CertificateGenerator()
    let certName = X509Name("CN=Avalanchain")
    let issuerDN = X509Name("CN=Avalanchain")
    let serialNo = BigInteger.ProbablePrime(120, Random())

    gen.SetSerialNumber(serialNo)
    gen.SetSubjectDN(certName)
    gen.SetIssuerDN(issuerDN)
    gen.SetNotAfter(DateTime.Now.AddYears(100))
    gen.SetNotBefore(DateTime.Now.Subtract(TimeSpan(7, 0, 0, 0)))


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

    type ECKeys(keyPair: AsymmetricCipherKeyPair) = 
        member __.KeyPair = keyPair
        member __.PrivateKey = keyPair.Private :?> ECPrivateKeyParameters
        member __.PublicKey = keyPair.Public :?> ECPublicKeyParameters
        member __.PrivateKeyPem = __.PrivateKey |> toPem
        member __.PublicKeyPem = __.PublicKey |> toPem

    let generatePKeys (intSize: uint32) =
        //Generating p-384 keys 384 specifies strength
        generateKeys(intSize) |> ECKeys


    //let bcKeys = Org.BouncyCastle.Security.DotNetUtilities.GetKeyPair(generatePKeys())
    // Org.BouncyCastle.Crypto.Asn1.CreateKey(SubjectPublicKeyInfo.GetInstance(Asn1Object.FromByteArray(keyInfoData)))
    let bcKeys = generatePKeys 384u

    gen.SetPublicKey(bcKeys.KeyPair.Public)

    let signatureFactory = Asn1SignatureFactory("SHA384withECDSA", bcKeys.KeyPair.Private, SecureRandom(CryptoApiRandomGenerator()))

    let cert = gen.Generate(signatureFactory)

    let store = Pkcs12Store()
    let friendlyName = cert.IssuerDN.ToString()
    let entry = X509CertificateEntry(cert)
    store.SetCertificateEntry(friendlyName, entry)
    store.SetKeyEntry(friendlyName, AsymmetricKeyEntry(bcKeys.KeyPair.Private), [| entry |])
    let storeFile = IO.File.OpenWrite("X509.store")
    store.Save(storeFile, Seq.toArray "A password here", SecureRandom(CryptoApiRandomGenerator()))
    storeFile.Close()


    let store2 = Pkcs12Store()
    store.Load(IO.File.OpenRead("X509.store"), Seq.toArray "A password here")
    let entries = store.GetCertificateChain friendlyName 
    let chain = X509Chain()
    // for ce in entries do chain.Build(X509Certificate2(ce.Certificate)) |> ignore