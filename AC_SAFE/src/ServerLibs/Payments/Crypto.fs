namespace Avalanchain.Core

module Crypto = 
    open System
    open System.Security.Cryptography

    open Jose

    // TODO: Readjust this
    type SigningPublicKey = SigningPublicKey of byte[]
    type SignedProof<'T> = 'T

    type JwsEd25519() =
        interface Jose.IJwsAlgorithm with 
            member __.Sign (securedInput: byte[], key: obj): byte[] = securedInput
            member __.Verify(signature: byte[], securedInput: byte[], key: obj): bool = true    

    let jwsAlgo = Jose.JwsAlgorithm.ES512

    Jose.JWT.DefaultSettings
        .RegisterJws(jwsAlgo, JwsEd25519()) 
        .RegisterJwsAlias("Ed25519", jwsAlgo) 
        |> ignore            

    let hasher = SHA256.Create()

    let sha (s: string) = 
        hasher.ComputeHash(Text.ASCIIEncoding.UTF8.GetBytes s) |> Convert.ToBase64String    

    type CryptoContext (*<'TData>*) = {
//        Hasher: Hasher
        SigningPublicKey: SigningPublicKey
//        Signer: Signer
//        Verifier: Verifier
//        EncryptionPublicKey: EncryptionPublicKey 
//        //PrivateKey: PrivateKey
//        //SignatureMethod: Signature
//        EncryptionMethod: Encryption
//        Encryptor: Encryptor
//        Decryptor: Decryptor
//        Dispose: unit -> unit
    }
    with 
//        member this.ProofVerifier proof = this.Verifier proof.Signature (Unsigned proof.ValueHash.Bytes)
//        member this.HashSigner (hash: Hash) = this.Signer (Unsigned hash.Bytes)
//        member this.Address = this.SigningPublicKey |> this.Hasher |> (fun h -> Base58CheckEncoding.Encode h.Bytes)
        member this.Address = "TBD"

    let cryptoContextNamed name: CryptoContext = { SigningPublicKey = SigningPublicKey [||] }