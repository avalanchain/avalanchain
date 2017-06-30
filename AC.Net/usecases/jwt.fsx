#r "../packages/Inferno/lib/net452/SecurityDriven.Inferno.dll"
#r "../packages/jose-jwt/lib/net40/jose-jwt.dll"
#r "../packages/Newtonsoft.Json/lib/net45/Newtonsoft.Json.dll"
#r "../packages/Microsoft.FSharpLu.Json/lib/net452/Microsoft.FSharpLu.Json.dll"


#time

open Security.Cryptography
open System
open System.Security.Cryptography
open SecurityDriven.Inferno
open SecurityDriven.Inferno.Hash
open SecurityDriven.Inferno.Extensions
open FSharpLu.Json

type AAA = { name: string }
let o: AAA = Microsoft.FSharpLu.Json.Compact.deserialize """{ "name": "Hello Wo" }"""
printfn "%A" o

let dsaKeyPrivate = CngKeyExtensions.CreateNewDsaKey()
let dsaKeyPrivateBlob = dsaKeyPrivate.GetPrivateBlob()
let dsaKeyPublicBlob = dsaKeyPrivate.GetPublicBlob()
let dsaKeyPublic: CngKey = dsaKeyPublicBlob.ToPublicKeyFromBlob()

for i in 0 .. 999 do CngKeyExtensions.CreateNewDsaKey() |> ignore

open Jose

let eccKey = EccKey.Generate(dsaKeyPublic)
eccKey.D

let ec = EccKey.New(eccKey.X, eccKey.Y)

let payload = [ "sub", "mr.x@contoso.com" |> box 
                "exp", 1300819380 |> box
            ]


// let token = Jose.JWT.Encode(payload, dsaKeyPrivate, JwsAlgorithm.ES384)

// for i in 0 .. 999 do Jose.JWT.Encode(payload, dsaKeyPrivate, JwsAlgorithm.ES384) |> ignore

// let pwd = "secret" |> System.Text.Encoding.UTF8.GetBytes
// for i in 0 .. 19999 do Jose.JWT.Encode(payload, pwd, JwsAlgorithm.HS384) |> ignore


let token = Jose.JWT.Encode(payload, dsaKeyPrivate, JwsAlgorithm.ES384)

let headers1 = [
    "keyid", box "1"
    "pos", box "20" ] |> Map.ofList

let token1 = Jose.JWT.Encode(payload, dsaKeyPrivate, JwsAlgorithm.ES384, headers1)

let headers2 = [
    "kid", box "1"
    "pos", box "12" ] |> Map.ofList

open FSharp.Reflection
    
let recordToMap<'r> (r: 'r) =
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
| HS512

type JwtAlgoAsym =
| ES384
| ES512

type JwtAlgo =
| Sym of JwtAlgoSym
| Asym of JwtAlgoAsym

type Uid = Guid
type Pos = uint64

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

let hasher = SHA384Managed.Create()

let sha (s: string) = 
    hasher.ComputeHash(Text.ASCIIEncoding.UTF8.GetBytes s) |> Convert.ToBase64String

// for i in 0 .. 999999 do sha "Hello world" |> ignore


let mapper = {  new IJsonMapper with
                member __.Serialize o = Microsoft.FSharpLu.Json.Compact.serialize o
                member __.Parse<'T> (json: string) = Microsoft.FSharpLu.Json.Compact.deserialize<obj> json :?> 'T  // TODO: Add try
            }

type JsonMapper() =
    interface IJsonMapper with
        member __.Serialize o = Microsoft.FSharpLu.Json.Compact.serialize o
        member __.Parse<'T> (json: string) = Microsoft.FSharpLu.Json.Compact.deserialize<obj> json :?> 'T // TODO: Add try

// Jose.JWT.DefaultSettings.JsonMapper = mapper

type ChainDefToken(chainDef: ChainDef, privateKey: CngKey) =
    let token = 
        let payload = chainDef |> Microsoft.FSharpLu.Json.Compact.serialize
        Jose.JWT.Encode(payload, privateKey, JwsAlgorithm.ES384)
    member __.Token = token
    member __.Ref = token.Split([|'.'|], 4) |> Array.last |> sha |> Sig
    member __.Payload = Jose.JWT.Decode(token, privateKey)
                        |> Microsoft.FSharpLu.Json.Compact.deserialize<ChainDef>

let chainDef = {
    algo = Sym(HS512)
    uid = Guid.NewGuid()
    chainType = ChainType.New
    encryption = Encryption.None
    compression = Compression.None
}

//let chainRef = 


let chain (chainDef: ChainDef) =
    ChainDefToken(chainDef, dsaKeyPrivate)

let cdToken = chain chainDef
cdToken.Payload

let chainDef2 = {
    algo = Asym(ES512)
    uid = Guid.NewGuid()
    chainType = ChainType.Derived (cdToken.Ref, 0UL, Map("function (a) {return a}"))
    encryption = Encryption.None
    compression = Compression.None
}


let cdTokenDerived = chain chainDef2
cdTokenDerived.Payload

