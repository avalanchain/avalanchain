namespace Avalanchain.Core
open System.Collections.Generic

module Crypto = 
    open System
    open TypeShape.Core.Utils
    open Sodium
    open FSharpx.Option

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

    type Pos = uint64

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
                member inline __.Hash (str: string) = GenericHash.Hash (str, string(null), 32)
                member inline __.Hash (bytes: byte[]) = GenericHash.Hash (bytes, null, 32)
                interface IDisposable with member __.Dispose() = match __ with  | NaClPair kp -> kp.Dispose() 
                                                                                | NaClPub _ -> ()
    and NaClPubKey = NaClPubKey of byte[] 
        with 
            member __.Bytes = match __ with NaClPubKey bts -> bts
            member __.Kid() = BitConverter.ToUInt64(ShortHash.Hash(__.Bytes, (Array.zeroCreate<byte> 16)), 0)

    type KeyVaultEntry = {
        Kid: JwtKeyId
        KeyRing: KeyRing
    }   with interface IDisposable with member __.Dispose() = (__.KeyRing :> IDisposable).Dispose()

    module KeyVaultEntry =
        let private load keyRing = { KeyRing = keyRing; Kid = (keyRing.Kid()) }
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

    let sign (kve: KeyVaultEntry) header payload = maybe {
        let header = { header with kid = kve.Kid } 
        let json, prepared = prepareToSign header payload
        let! signature = prepared |> kve.KeyRing.Sign 
        let signatureStr = encodeBase64Bytes signature
        return prepared +.+ signatureStr, header, json, signatureStr
    }

    let unpack verify (kv: IKeyVault) token = maybe {
        let! (headerWithPayload, signature) = prepareToVerify token
        let! (headerStr, payload) = extractHeader headerWithPayload 
        let header = fromJson<JwtTokenHeader>(headerStr)
        let! keyEntry = kv.Get header.kid
        if (not verify) || keyEntry.KeyRing.Verify headerWithPayload (decodeBase64Bytes signature) then
            return token, header, payload, signature 
        else return! None
    }

    let toRef (kve: KeyVaultEntry) (str: string) = 
        { Cid = str |> kve.KeyRing.Hash |> Utilities.BinaryToHex } 

    type SignedProof<'T> = { 
        Value: 'T
        Proof: string 
    }

    type JwtToken<'t> = {
        Token: string
        Json: string
        Ref: TokenRef
        Payload: 't
        Header: JwtTokenHeader
        Signature: string
    }

    type JwtToHeader = unit -> JwtTokenHeader
    type JwtFromHeader = IDictionary<string, obj> -> JwtTokenHeader
    // type JwtToFromHeader = {
    //     To: JwtToHeader
    //     From: JwtFromHeader
    // }

    let toJwt (kve: KeyVaultEntry) header (payload: 'T) = maybe {
        let! token, header, json, signature = sign kve header payload
        return {Token = token
                Json = json
                Ref = signature |> toRef kve
                Payload = payload 
                Header = header
                Signature = signature }
    }

    let fromJwt<'T> (kv: IKeyVault) verify (token: string): JwtToken<'T> option = maybe {
        let! token, header, payload, signature = unpack verify kv token
        return {Token = token
                Json = payload
                Ref = signature |> toRef (kv.Get header.kid).Value // Can use Value safely here as the kid was already extracted in the verify() call above 
                Payload = fromJson<'T> payload 
                Header = header
                Signature = signature }
    }

