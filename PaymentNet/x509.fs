namespace avalanchain.Common

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


    let generateKeys () =
        let keySize = 384
        //using ECDSA algorithm for the key generation
        let gen = Org.BouncyCastle.Crypto.Generators.ECKeyPairGenerator("ECDSA")
        let curve25519 = Org.BouncyCastle.Crypto.EC.CustomNamedCurves.GetByName("curve25519")
        printfn "curve25519: %A" curve25519

        //Creating Random
        let secureRandom = SecureRandom()

        //Parameters creation using the random and keysize
        let keyGenParam = KeyGenerationParameters(secureRandom, keySize |> int)

        //Initializing generation algorithm with the Parameters--This method Init i modified
        gen.Init(keyGenParam)
    
        //Generating p-384 keys 384 specifies strength
        let keyPair = gen.GenerateKeyPair()
        keyPair

    let internal toPem<'T> (o: 'T) = 
        use textWriter = new StringWriter()
        let pemWriter = Org.BouncyCastle.OpenSsl.PemWriter(textWriter)
        pemWriter.WriteObject(o)
        pemWriter.Writer.Flush()
        textWriter.ToString()

    let internal fromPem pem = 
        use textReader = new StringReader (pem)
        let pemReader = Org.BouncyCastle.OpenSsl.PemReader (textReader)
        let pemObject = pemReader.ReadPemObject()
        pemObject

    let privateKey (keyPair: AsymmetricCipherKeyPair) = keyPair.Private |> toPem
    let privateKeyParam (keyPair: AsymmetricCipherKeyPair) = keyPair.Private :?> ECPrivateKeyParameters
    let publicKey (keyPair: AsymmetricCipherKeyPair) = keyPair.Public |> toPem
    let publicKeyParam (keyPair: AsymmetricCipherKeyPair) = keyPair.Public :?> ECPublicKeyParameters

        // printfn "Private Key: '%s'" privateKey // TODO: Remove this
        // printfn "Private Key Param: '%s'" (privateKeyParam.D.ToString())
        // printfn "Public Key: '%s'" publicKey // TODO: Remove this
        // printfn "Public Key Param X: '%s'" (publicKeyParam.Q.X.ToBigInteger().ToString())
        // printfn "Public Key Param Y '%s'" (publicKeyParam.Q.Y.ToBigInteger().ToString())

    let generateCertificate name (keyPair: AsymmetricCipherKeyPair) = 
        let gen = X509V3CertificateGenerator()
        let certName = X509Name("CN=" + name)
        let serialNo = BigInteger.ProbablePrime(120, Random())

        gen.SetSerialNumber(serialNo)
        gen.SetSubjectDN(certName)
        gen.SetIssuerDN(certName)
        gen.SetNotAfter(DateTime.Now.AddYears(100))
        gen.SetNotBefore(DateTime.Now.Subtract(TimeSpan(7, 0, 0, 0)))
        gen.SetPublicKey(keyPair.Public)

        let signatureFactory = Asn1SignatureFactory("SHA384WITHECDSA", keyPair.Private, SecureRandom.GetInstance("SHA384PRNG"))

        let cert = gen.Generate(signatureFactory)
        cert

    let toPkcs12 friendlyName password cert (keyPair: AsymmetricCipherKeyPair) = 
        let builder = new Pkcs12StoreBuilder()
        let store = builder.SetUseDerEncoding(true).Build()
        let entry = X509CertificateEntry(cert)
        store.SetCertificateEntry(friendlyName, entry)
        store.SetKeyEntry(friendlyName, AsymmetricKeyEntry(keyPair.Private), [| entry |])
        use stream = new MemoryStream()
        store.Save(stream, null, SecureRandom.GetInstance("SHA384PRNG"))
        stream.Seek(0L, SeekOrigin.Begin) |> ignore
        stream.ToArray() |> Pkcs12Utilities.ConvertToDefiniteLength

    let fromPkcs12 friendlyName password (bytes: byte[]) =
        let store = Pkcs12Store()
        store.Load(new MemoryStream(bytes), null)
        let certChain = store.GetCertificateChain friendlyName
        let firstCert = certChain.[0].Certificate
        firstCert

    let storeCertificate fileName friendlyName password cert (keyPair: AsymmetricCipherKeyPair) = 
        let bytes = toPkcs12 friendlyName password cert keyPair
        use storeFile = IO.File.OpenWrite(fileName)
        storeFile.Write(bytes, 0, bytes.Length)

    let loadCertificateFile fileName =
        let file = IO.File.OpenRead(fileName)
        use stream = new MemoryStream()
        file.CopyTo stream
        stream.ToArray()

    let loadCertificate fileName friendlyName password =
        fromPkcs12 friendlyName password (loadCertificateFile fileName)

    