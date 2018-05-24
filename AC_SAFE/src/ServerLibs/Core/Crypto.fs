namespace Avalanchain.Core

module Crypto = 
    open System
    open System.Collections.Generic

    open Sodium
    open FSharpx.Option
    open FSharpx.Result

    open TypeShape.Core.Utils
    open TypeShape.Tools

    // open Jose


    let encodeBase64Bytes = 
        Convert.ToBase64String >> fun output -> output.Split('=').[0].Replace('+', '-').Replace('/', '_') 
    let decodeBase64Bytes (token: string) = 
        let output = token.Replace('-', '+').Replace('_', '/') // 62nd and 63rd char of encoding
        match output.Length % 4 with // Pad with trailing '='s
        | 0 -> output
        | 2 -> output + "=="
        | 3 -> output + "="
        | _ -> output //raise (System.ArgumentOutOfRangeException("input", "Illegal base64url string!"))
        |> System.Convert.FromBase64String 

    let getBytes (str: string) = System.Text.Encoding.UTF8.GetBytes str
    let getString = System.Text.Encoding.UTF8.GetString

    let encodeBase64 (input: string) = input |> getBytes |> encodeBase64Bytes
    let decodeBase64 = decodeBase64Bytes >> getString

    type Pos = uint64
    type PageSize = uint32

    type JwtAlgo = Ed25519
    type JwtType = JWT
    type JwtKeyId = uint64
    type JwtTokenHeader = {
        alg: JwtAlgo
        typ: JwtType
        kid: JwtKeyId
        pos: Pos option     // Optional position
        enc: bool           // Encoding
    } with static member Create kid pos = { alg = Ed25519; typ = JWT; kid = kid; pos = pos; enc = false }

    type TokenRef = { Cid: string }

    type IntegrityError =
        | SigningError of SigningError
        | VerificationError of VerificationError
    and SigningError =
        | KeyIdNotFould of JwtKeyId
        | NoPrivateKeyAvailable of JwtKeyId
        | SigningFailed
    and VerificationError =
        | InvalidTokenFormat
        | InvalidTokenHeaderFormat
        | KeyIdNotFound of JwtKeyId
        | SignatureVerificationFailed    

    let (+.+) (l: string) r = l + "." + r

    let private prepareToSignStrings header payload = encodeBase64 header +.+ encodeBase64 payload

    let private typeCache = TypeCache()
    let toJson<'T> v = 
        use ctx = typeCache.CreateGenerationContext()
        let pickler = Json.genPicklerCached<'T> ctx
        Json.serialize (pickler) v  

    let fromJson<'T> json = 
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

    let private hashKey() = Array.zeroCreate<byte> 32
    let hash (bytes: byte[]) = GenericHash.Hash (bytes, hashKey(), 32)
    let hashString (str: string) = GenericHash.Hash (str, hashKey(), 32) 


    type KeyVaultEntry = {
        Kid: JwtKeyId
        KeyRing: KeyRing
    }   with interface IDisposable with member __.Dispose() = (__.KeyRing :> IDisposable).Dispose()

    module KeyVaultEntry =
        let private load keyRing = { KeyRing = keyRing; Kid = keyRing.Kid() }
        let loadPair = NaClPair >> load 
        let loadPairHex publicKeyHex privateKeyHex = new KeyPair((publicKeyHex |> Utilities.HexToBinary), (privateKeyHex |> Utilities.HexToBinary)) |> loadPair
        let loadPub = NaClPub >> load 
        let loadPubHex publicKeyHex = publicKeyHex |> Utilities.HexToBinary |> NaClPubKey |> loadPub
        let generate() = PublicKeyAuth.GenerateKeyPair() |> loadPair
        
    type IKeyVault =
        abstract member Get: JwtKeyId -> KeyVaultEntry option
        abstract member Active: KeyVaultEntry 
        
    type KeyVault (entries: KeyVaultEntry seq) = 
        let entries = entries |> Seq.toArray 
        let keyEntries = entries |> Seq.map(fun kve -> kve.Kid, kve) |> Map.ofSeq
        let active = entries |> Array.head
        interface IKeyVault with 
            member __.Get kid = keyEntries |> Map.tryFind kid 
            member __.Active = active

    let toHeader pos = JwtTokenHeader.Create UInt64.MaxValue pos // Kid will be populated by signing anyway    

    let prepareToSign header payload = 
        let json = toJson payload
        json, prepareToSignStrings (toJson header) json // TODO: Cache out the pickler generation

    let prepareToVerify (token: string) = 
        let idx = token.LastIndexOf "."
        match idx with 
        | -1 -> None
        | _ -> (token.Substring(0, idx), token.Substring(idx + 1)) |> Some
        
    let extractHeader (token: string) = 
        let idx = token.IndexOf "."
        match idx with 
        | -1 -> None
        | _ -> (token.Substring(0, idx), token.Substring(idx + 1)) |> Some        

    let sign (kve: KeyVaultEntry) header payload = result {
        let header = { header with kid = kve.Kid } 
        let json, prepared = prepareToSign header payload
        let! signature = NoPrivateKeyAvailable kve.Kid, prepared |> kve.KeyRing.Sign 
        let signatureStr = encodeBase64Bytes signature
        return prepared +.+ signatureStr, header, json, signatureStr
    }

    let unpack verify (kv: IKeyVault) token = result { 
        let! (headerWithPayload, signature) = InvalidTokenFormat, prepareToVerify token
        let! (headerStr, payloadStr) = InvalidTokenHeaderFormat, extractHeader headerWithPayload
        let header = headerStr |> decodeBase64 |> fromJson<JwtTokenHeader>
        let payload = payloadStr |> decodeBase64 
        if verify then 
            let! keyEntry = KeyIdNotFound header.kid, kv.Get header.kid
            if keyEntry.KeyRing.Verify headerWithPayload (decodeBase64Bytes signature) then return token, header, payload, signature
            else return! Error SignatureVerificationFailed
        else return token, header, payload, signature
    }

    let toRef (str: string) = { Cid = str |> hashString |> Utilities.BinaryToHex } 

    type SignedProof<'T> = { 
        Value: 'T
        Proof: string 
    }

    type JwtToken<'T> = {
        Token: string
        Json: string
        Ref: TokenRef
        Payload: 'T
        Header: JwtTokenHeader
        Signature: string
    }

    type JwtToHeader = unit -> JwtTokenHeader
    type JwtFromHeader = IDictionary<string, obj> -> JwtTokenHeader
    // type JwtToFromHeader = {
    //     To: JwtToHeader
    //     From: JwtFromHeader
    // }

    let toJwt (kve: KeyVaultEntry) header (payload: 'T) = result {
        let! token, header, json, signature = sign kve header payload
        return {Token = token
                Json = json
                Ref = signature |> toRef
                Payload = payload 
                Header = header
                Signature = signature }
    }

    let fromJwt<'T> (kv: IKeyVault) verify (token: string) = result {
        let! token, header, payload, signature = unpack verify kv token
        return {Token = token
                Json = payload
                Ref = signature |> toRef 
                Payload = fromJson<'T> payload 
                Header = header
                Signature = signature }
    }

