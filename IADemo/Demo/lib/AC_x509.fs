namespace Avalanchain

module AC_x509 =
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

    let keySize = 384
    let internal factoryName = sprintf "SHA%dWITHECDSA" keySize
    let internal signerName = sprintf "SHA-%dwithECDSA" keySize

    let generateKeys () =
        //using ECDSA algorithm for the key generation
        let gen = Org.BouncyCastle.Crypto.Generators.ECKeyPairGenerator("ECDSA")

        //Creating Random
        let secureRandom = SecureRandom()

        //Parameters creation using the random and keysize
        let keyGenParam = KeyGenerationParameters(secureRandom, keySize |> int)

        //Initializing generation algorithm with the Parameters--This method Init i modified
        gen.Init(keyGenParam)
    
        //Generating p-384 keys 384 specifies strength
        let keyPair = gen.GenerateKeyPair()
        keyPair

    let internal toPem (key: AsymmetricKeyParameter) = 
        use textWriter = new StringWriter()
        let pemWriter = Org.BouncyCastle.OpenSsl.PemWriter(textWriter)
        pemWriter.WriteObject(key)
        pemWriter.Writer.Flush()
        pemWriter.ToString()

    let privateKey (keyPair: AsymmetricCipherKeyPair) = keyPair.Private |> toPem
    let privateKeyParam (keyPair: AsymmetricCipherKeyPair) = keyPair.Private :?> ECPrivateKeyParameters
    let publicKey (keyPair: AsymmetricCipherKeyPair) = keyPair.Public |> toPem
    let publicKeyParam (keyPair: AsymmetricCipherKeyPair) = keyPair.Public :?> ECPublicKeyParameters

        // printfn "Private Key: '%s'" privateKey // TODO: Remove this
        // printfn "Private Key Param: '%s'" (privateKeyParam.D.ToString())
        // printfn "Public Key: '%s'" publicKey // TODO: Remove this
        // printfn "Public Key Param X: '%s'" (publicKeyParam.Q.X.ToBigInteger().ToString())
        // printfn "Public Key Param Y '%s'" (publicKeyParam.Q.Y.ToBigInteger().ToString())

    // let keyPair = generateKeys()

    let signatureFactory privateKey = Asn1SignatureFactory(factoryName, privateKey, SecureRandom.GetInstance("SHA256PRNG"))

    let sign (privateKeyParam: ECPrivateKeyParameters) data: byte[] = 
        let signer = SignerUtilities.GetSigner(signerName)
        signer.Init(true, privateKeyParam)
        signer.BlockUpdate(data, 0, data.Length);
        signer.GenerateSignature()

    let verify (publicKeyParam: ECPublicKeyParameters) data signature: bool = 
        let signer = SignerUtilities.GetSigner(signerName)
        signer.Init(false, publicKeyParam)
        signer.BlockUpdate(data, 0, data.Length)
        signer.VerifySignature(signature)
        

    let private generateCertificate name (keyPair: AsymmetricCipherKeyPair) = 
        let gen = X509V3CertificateGenerator()
        let certName = X509Name("CN=PickAName")
        let serialNo = BigInteger.ProbablePrime(120, Random())

        gen.SetSerialNumber(serialNo)
        gen.SetSubjectDN(certName)
        gen.SetIssuerDN(certName)
        gen.SetNotAfter(DateTime.Now.AddYears(100))
        gen.SetNotBefore(DateTime.Now.Subtract(TimeSpan(7, 0, 0, 0)))
        gen.SetPublicKey(keyPair.Public)

        //gen.SetSignatureAlgorithm("SHA384WITHECDSA")

        let cert = gen.Generate(signatureFactory(keyPair.Private))
        cert

    let storeCertificate friendlyName password cert (keyPair: AsymmetricCipherKeyPair) = 
        let store = Pkcs12Store()
        let entry = X509CertificateEntry(cert)
        store.SetCertificateEntry(friendlyName, entry)
        store.SetKeyEntry(friendlyName, AsymmetricKeyEntry(keyPair.Private), [| entry |])
        use storeFile = IO.File.OpenWrite("X509.store")
        store.Save(storeFile, Seq.toArray password, SecureRandom.GetInstance("SHA256PRNG"))


    let loadCertificate friendlyName password =
        let store = Pkcs12Store()
        store.Load(IO.File.OpenRead("X509.store"), Seq.toArray password)
        let certChain = store.GetCertificateChain friendlyName

        let firstCert = certChain.[0].Certificate
        firstCert