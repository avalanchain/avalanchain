namespace Avalanchain.Core

module Crypto = 
    open System
    open TypeShape.Core.Utils
    open Sodium

    // open Jose

    open TypeShape.Tools

    let encodeBase64Bytes = 
        Convert.ToBase64String >> fun output -> output.Split('=').[0].Replace('+', '-').Replace('/', '_') 
    let decodeBase64Bytes (token: string) = 
        let output = token.Replace('-', '+').Replace('_', '/') // 62nd and 63rd char of encoding
        match output.Length % 4 with // Pad with trailing '='s
        | 0 -> output
        | 2 -> output + "=="
        | 3 -> output + "="
        | _ -> raise (System.ArgumentOutOfRangeException("input", "Illegal base64url string!"))
        |> System.Convert.FromBase64String 

    let encodeBase64 (input: string) = input |> System.Text.Encoding.UTF8.GetBytes |> encodeBase64Bytes
    let decodeBase64 = decodeBase64Bytes >> System.Text.Encoding.UTF8.GetString

    type JwtAlgo = Ed25519
    type JwtType = JWT
    type JwtKeyId = uint64
    type JwtHeader = {
        alg: JwtAlgo
        typ: JwtType
        kid: JwtKeyId
    } with static member Create kid = { alg = Ed25519; typ = JWT; kid = kid }

    let (+.+) (l: string) r = l + "." + r

    let prepareToSignStrings header payload = encodeBase64 header +.+ encodeBase64 payload

    let private typeCache = TypeCache()
    let toJson v = 
        use ctx = typeCache.CreateGenerationContext()
        let pickler = Json.genPicklerCached<'T> ctx
        Json.serialize (pickler) v 

    let fromJson json = 
        use ctx = typeCache.CreateGenerationContext()
        let pickler = Json.genPicklerCached<'T> ctx
        Json.deserialize (pickler) json


    type KeyRing =
    | NaClPair of KeyPair
    | NaClPub of NaClPubKey
        with 
            member inline __.PublicKey = match __ with  | NaClPair kp -> kp.PublicKey |> NaClPubKey 
                                                        | NaClPub pk -> pk
            member inline __.PrivateKey = match __ with | NaClPair kp -> Some kp.PrivateKey 
                                                        | NaClPub _ -> None
            member inline __.PublicKeyHex = __.PublicKey.Bytes |> Utilities.BinaryToHex
            member inline __.PrivateKeyHex = __.PrivateKey |> Option.map Utilities.BinaryToHex
            member inline __.Kid() = __.PublicKey.Kid()
            member inline __.Sign (str: string) = __.PrivateKey |> Option.map (fun privKey -> PublicKeyAuth.SignDetached(str, privKey)) 
            member inline __.Verify (str: string) signature = 
                PublicKeyAuth.VerifyDetached(signature, Text.Encoding.UTF8.GetBytes(str), __.PublicKey.Bytes)
            interface IDisposable with member __.Dispose() = match __ with  | NaClPair kp -> kp.Dispose() 
                                                                            | NaClPub _ -> ()
    and NaClPubKey = NaClPubKey of byte[] 
        with 
            member __.Bytes = match __ with NaClPubKey bts -> bts
            member __.Kid() = BitConverter.ToUInt64(ShortHash.Hash(__.Bytes, (Array.zeroCreate<byte> 16)), 0)

    type CryptoContext = {
        Kid: JwtKeyId
        KeyRing: KeyRing
    }   with interface IDisposable with member __.Dispose() = (__.KeyRing :> IDisposable).Dispose()

    module CryptoContext =
        let private load keyRing = { KeyRing = keyRing; Kid = (keyRing.Kid()) }
        let loadPair = NaClPair >> load 
        let loadPairHex publicKeyHex privateKeyHex = new KeyPair((publicKeyHex |> Utilities.HexToBinary), (privateKeyHex |> Utilities.HexToBinary)) |> loadPair
        let loadPub = NaClPub >> load 
        let loadPubHex publicKeyHex = publicKeyHex |> Utilities.HexToBinary |> NaClPubKey |> loadPub
        let generate() = PublicKeyAuth.GenerateKeyPair() |> loadPair
        

    let prepareToSign kid payload = prepareToSignStrings (JwtHeader.Create kid |> toJson) (toJson payload) // TODO: Cache out the pickler generation

    let prepareToVerify (token: string) = 
        let idx = token.LastIndexOf "."
        match idx with 
        | -1 -> None
        | _ -> (token.Substring(0, idx), token.Substring(idx + 1)) |> Some

    let sign (sk: CryptoContext) payload = 
        let prepared = prepareToSign sk.Kid payload
        prepared |> sk.KeyRing.Sign |> Option.map (fun signature -> prepared +.+ (encodeBase64Bytes signature))

    let verify (sk: CryptoContext) token = 
        prepareToVerify token
        |> Option.map (fun (payload, signature) -> sk.KeyRing.Verify payload (decodeBase64Bytes signature))


    type SignedProof<'T> = 'T

