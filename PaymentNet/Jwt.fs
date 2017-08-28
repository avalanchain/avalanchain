namespace avalanchain.Common

module Jwt =
    open System
    open FSharp.Reflection
    open FSharpLu.Json

    open System.Security.Cryptography
    
    open Jose
    
    let private recordToMap<'r> (r: 'r) =
        let fields = FSharpType.GetRecordFields(typedefof<'r>) |> Array.map (fun pi -> pi.Name)
        let vals = FSharpValue.GetRecordFields(r) 
        Array.zip fields vals |> Map.ofArray

    type TokenHeader = {
        kid: uint16
        pos: uint64
        cty: string
        // alg: string
        // enc: string
    }

    type JwtAlgoSym =
    | HS256
    | HS384
    | HS512

    type JwtAlgoAsym =
    | ES256
    | ES384
    | ES512

    type JwtAlgo =
    | Sym of JwtAlgoSym
    | Asym of JwtAlgoAsym

    type Uid = Guid
    type Pos = uint64

    let hasher = SHA384.Create()

    let sha (s: string) = 
        hasher.ComputeHash(Text.ASCIIEncoding.UTF8.GetBytes s) |> Convert.ToBase64String

    type Json = string
    type JsFunc = string // TODO
    type Func1 = JsFunc
    type Func2 = JsFunc

    type Derivation =
    | Fork
    | Map of Func1
    | Filter of Func1
    | Fold of Func2 * init: Json
    | Reduce of Func2
    | FilterFold of filter: Func1 * folder: Func2
    | GroupBy of groupper: Func1 * max: uint32

    type ChainRef = Sig of string

    [<RequireQualifiedAccess>] 
    type ChainType = 
    | New
    | Derived of cr: ChainRef * pos: Pos * Derivation

    [<RequireQualifiedAccess>] 
    type Encryption = // TODO: expand
    | None

    [<RequireQualifiedAccess>] 
    type Compression = 
    | None
    | Deflate

    // [<CLIMutable>]
    type ChainDef = {
        algo: JwtAlgo
        uid: Uid
        chainType: ChainType
        encryption: Encryption
        compression: Compression 
    }

    type ChainDefToken(chainDef: ChainDef, privateKey: CngKey) =
        let token = 
            let payload = chainDef |> FSharpLu.Json.Compact.serialize
            Jose.JWT.Encode(payload, privateKey, JwsAlgorithm.ES384)
        member __.Token = token
        member __.Ref = token.Split([|'.'|], 4) |> Array.last |> sha |> Sig
        member __.Payload = Jose.JWT.Decode(token, privateKey)
                            |> FSharpLu.Json.Compact.deserialize<ChainDef>

    let chainDef = {
        algo = Sym(HS512)
        uid = Guid.NewGuid()
        chainType = ChainType.New
        encryption = Encryption.None
        compression = Compression.None
    }

    //let chainRef = 

    let keyPair = x509.generateKeys()
    let cngKey = CngKey.Create(CngAlgorithm.ECDsaP384)

    let chain (chainDef: ChainDef) =
        ChainDefToken(chainDef, cngKey)

    let cdToken = chain chainDef
    // cdToken.Payload

    let chainDef2 = {
        algo = Asym(ES512)
        uid = Guid.NewGuid()
        chainType = ChainType.Derived (cdToken.Ref, 0UL, Map("function (a) {return a}"))
        encryption = Encryption.None
        compression = Compression.None
    }


    let cdTokenDerived = chain chainDef2
    // cdTokenDerived.Payload

