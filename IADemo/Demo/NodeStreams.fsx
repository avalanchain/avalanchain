// #load "../.paket/load/net461/main.group.fsx"

// // #load "lib/ws.fs"

// open System
// open System.IO
// #if INTERACTIVE
// let cd = Path.Combine(__SOURCE_DIRECTORY__, "bin/Debug/net461")
// System.IO.Directory.SetCurrentDirectory(cd)
// #I "bin/Debug/net461"
// #endif

#load "AC_x509.fsx"

open Akka.IO
open Akka.Streams
open Akka.Streams.Dsl
open Akkling
open Akkling.Streams

let system = System.create "streams-sys" <| Configuration.defaultConfig()
let mat = system.Materializer()



open System
open FSharp.Reflection
open FSharpLu.Json

open System.Security.Cryptography

open Jose
open Newtonsoft.Json
open Microsoft.FSharpLu.Json

#time

JsonConvert.DefaultSettings <- fun () ->
    let settings = JsonSerializerSettings()
    settings.ContractResolver <- Serialization.CamelCasePropertyNamesContractResolver()
    settings.Converters <- [| CompactUnionJsonConverter() |]
    settings


module JwsAlgo = 
    open Org.BouncyCastle.Crypto.Parameters
    open AC_x509
    

    // Jose.JwsAlgorithm.ES384
    type CustomEC() =
        interface Jose.IJwsAlgorithm with 
            member __.Sign (securedInput: byte[], key: obj): byte[] = 
                AC_x509.sign (key :?> ECPrivateKeyParameters) securedInput
            member __.Verify(signature: byte[], securedInput: byte[], key: obj): bool = 
                AC_x509.verify (key :?> ECPublicKeyParameters) securedInput signature

    type EmptyEC() =
        interface Jose.IJwsAlgorithm with 
            member __.Sign (securedInput: byte[], key: obj): byte[] = securedInput
            member __.Verify(signature: byte[], securedInput: byte[], key: obj): bool = true

    type EC25519() =
        interface Jose.IJwsAlgorithm with 
            member __.Sign (securedInput: byte[], key: obj): byte[] = securedInput
            member __.Verify(signature: byte[], securedInput: byte[], key: obj): bool = true



let jwsAlgo = Jose.JwsAlgorithm.ES384

Jose.JWT.DefaultSettings.RegisterJws(jwsAlgo, JwsAlgo.EC25519()) |> ignore



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

type TokenRef = Sig of string
type ChainRef = TokenRef

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

open System.IO
open MBrace.FsPickler.Json

let jsonSerializer = FsPickler.CreateJsonSerializer(indent = false)

type ECToken<'T>(o: 'T, privateKey: CngKey) =
    let token = 
        // let payload = o |> JsonConvert.SerializeObject//Compact.serialize
        use tw = new StringWriter()
        do jsonSerializer.Serialize(tw, o)
        let payload = tw.ToString()
        Jose.JWT.Encode(payload, privateKey, JwsAlgorithm.ES384)
        //Jose.JWT.EncodeBytes([| 1uy; 1uy; 1uy; 1uy; 1uy; 1uy |], privateKey, jwsAlgo)
        //"4343.3434.343.343"
    // new (token: string, privateKey: CngKey) = 
    //     //ECToken<'T>((Jose.JWT.Decode(token, privateKey) |> Compact.deserialize<'T>), privateKey)
    //     ECToken<'T>((Jose.JWT.Decode(token, privateKey) |> JsonConvert.DeserializeObject<'T>), privateKey)
    member __.Ref = token.Split([|'.'|], 4) |> Array.last |> sha |> Sig
    member __.Token = token
    member __.Payload = o

type ChainDefToken(chainDef: ChainDef, privateKey: CngKey) = 
    inherit ECToken<ChainDef>(chainDef, privateKey)

let chainDef = {
    algo = Sym(HS512)
    uid = Guid.NewGuid()
    chainType = ChainType.New
    encryption = Encryption.None
    compression = Compression.None
}

//let chainRef = 

// let keyPair = x509.generateKeys()
// let cngKey = CngKey.Create(CngAlgorithm.ECDsaP384)
let cngKey = CngKey.Create(CngAlgorithm.ECDsaP384)

let chain (chainDef: ChainDef) =
    ChainDefToken(chainDef, null)//cngKey)

let cdToken = chain chainDef
// cdToken.Payload

let chainDef2 = {
    algo = Asym(ES512)
    uid = Guid.NewGuid()
    chainType = ChainType.Derived (cdToken.Ref, 0UL, Map("function (a) {return a;}"))
    encryption = Encryption.None
    compression = Compression.None
}


let cdTokenDerived = chain chainDef2
// cdTokenDerived.Payload


let ecc384Keys() =
    let x = [| 70uy; 151uy; 220uy; 179uy; 62uy; 0uy; 79uy; 232uy; 114uy; 64uy; 58uy; 75uy; 91uy; 209uy; 232uy; 128uy; 7uy; 137uy; 151uy; 42uy; 13uy; 148uy; 15uy; 133uy; 93uy; 215uy; 7uy; 3uy; 136uy; 124uy; 14uy; 101uy; 242uy; 207uy; 192uy; 69uy; 212uy; 145uy; 88uy; 59uy; 222uy; 33uy; 127uy; 46uy; 30uy; 218uy; 175uy; 79uy |]
    let y = [| 189uy; 202uy; 196uy; 30uy; 153uy; 53uy; 22uy; 122uy; 171uy; 4uy; 188uy; 42uy; 71uy; 2uy; 9uy; 193uy; 191uy; 17uy; 111uy; 180uy; 78uy; 6uy; 110uy; 153uy; 240uy; 147uy; 203uy; 45uy; 152uy; 236uy; 181uy; 156uy; 232uy; 223uy; 227uy; 148uy; 68uy; 148uy; 221uy; 176uy; 57uy; 149uy; 44uy; 203uy; 83uy; 85uy; 75uy; 55uy |]
    let d = [| 137uy; 199uy; 183uy; 105uy; 188uy; 90uy; 128uy; 82uy; 116uy; 47uy; 161uy; 100uy; 221uy; 97uy; 208uy; 64uy; 173uy; 247uy; 9uy; 42uy; 186uy; 189uy; 181uy; 110uy; 24uy; 225uy; 254uy; 136uy; 75uy; 156uy; 242uy; 209uy; 94uy; 218uy; 58uy; 14uy; 33uy; 190uy; 15uy; 82uy; 141uy; 238uy; 207uy; 214uy; 159uy; 140uy; 247uy; 139uy |]

    printfn "%d %d %d" x.Length y.Length d.Length

    Security.Cryptography.EccKey.New(x, y, d)



// Jose.JWT.DefaultSettings.RegisterJws(jwsAlgo, JwsAlgo.CustomEC()) |> ignore

let keyPair = AC_x509.AC_x509.generateKeys()
let token0 = Jose.JWT.EncodeBytes([| 1uy; 1uy; 1uy; 1uy; 1uy; 1uy |], keyPair.Private, jwsAlgo)
printfn "token: %s" token0
printfn "token verification: %A" (Jose.JWT.DecodeBytes(token0, keyPair.Public, jwsAlgo))


for a in 0 .. 99999 do 
    (chain chainDef) |> ignore 

for a in 0 .. 999 do 
    Jose.JWT.EncodeBytes([| 1uy; 1uy; 1uy; 1uy; 1uy; 1uy |], keyPair.Private, jwsAlgo)


let crypto = System.Security.Cryptography.RandomNumberGenerator.Create()
let randomNumber = Array.init 32 byte
crypto.GetBytes(randomNumber)

let token = Jose.JWT.Encode([| 1uy; 1uy; 1uy; 1uy; 1uy; 1uy |], randomNumber, Jose.JweAlgorithm.A256KW, Jose.JweEncryption.A256CBC_HS512)
printfn "Token: %s" token
    



////////////////

type AdminMessage = 
| ChainControl of ChainControlMessage
| NodeControl of NodeControlMessage
and ChainControlMessage =
| AddChain of ChainDefToken
| ReloadChain
and NodeControlMessage =
| ConnectToNode
| RebuildNode