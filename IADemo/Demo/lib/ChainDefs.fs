namespace Avalanchain

open System.Collections.Generic
module ChainDefs =

    open System
    open Akka.Actor
    open Akka.Configuration
    open Akka.IO
    open Akka.Persistence
    open Akka.Streams
    open Akka.Streams.Dsl
    open Reactive.Streams

    open Hyperion

    open Akkling
    open Akkling.Persistence
    open Akkling.Cluster
    open Akkling.Cluster.Sharding
    open Akkling.Streams

    open FSharp.Reflection
    open System.Security.Cryptography

    open Jose
    open Newtonsoft.Json
    open Microsoft.FSharpLu.Json


    JsonConvert.DefaultSettings <- fun () ->
        let settings = JsonSerializerSettings()
        settings.ContractResolver <- Serialization.CamelCasePropertyNamesContractResolver()
        settings.Converters <- [| CompactUnionJsonConverter() |]
        settings


    module JwsAlgo = 
        open Org.BouncyCastle.Crypto.Parameters

        // Jose.JwsAlgorithm.ES384
        type CustomEC() =
            interface Jose.IJwsAlgorithm with 
                member __.Sign (securedInput: byte[], key: obj): byte[] = 
                    AC_x509.sign (key :?> ECPrivateKeyParameters) securedInput
                member __.Verify(signature: byte[], securedInput: byte[], key: obj): bool = 
                    AC_x509.verify (key :?> ECPublicKeyParameters) securedInput signature

        type Base64EC() =
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

    let internal jsonSerializer = FsPickler.CreateJsonSerializer(indent = false)

    type JwtTokenHeader = {
        kid: uint16
        pos: int64
        // cty: string
        // alg: string
        // enc: string
    }

    type JwtToken<'t> = {
        Token: string
        Ref: TokenRef
        Payload: 't
        Header: JwtTokenHeader
    }

    type JwtToHeader = unit -> JwtTokenHeader
    type JwtFromHeader = IDictionary<string, obj> -> JwtTokenHeader
    // type JwtToFromHeader = {
    //     To: JwtToHeader
    //     From: JwtFromHeader
    // }

    let toJwt (toHeader: JwtToHeader) privateKey (o: 'T) =
        let token = 
            // let payload = o |> JsonConvert.SerializeObject//Compact.serialize
            use tw = new StringWriter()
            do jsonSerializer.Serialize(tw, o)
            let payload = tw.ToString()
            Jose.JWT.Encode(payload, privateKey, JwsAlgorithm.ES384)
        {   Token = token
            Ref = token.Split([|'.'|], 4) |> Array.last |> sha |> Sig
            Payload = token 
            Header = toHeader() }

    let fromJwt<'T> (fromHeader: JwtFromHeader) privateKey (token: string): JwtToken<'T> =
        let payload = Jose.JWT.Decode<string>(token, privateKey)
        let headers = Jose.JWT.Headers(token)
        use reader = new StringReader(payload)
        {   Token = token
            Ref = token.Split([|'.'|], 4) |> Array.last |> sha |> Sig
            Payload = jsonSerializer.Deserialize(reader) 
            Header = fromHeader headers }


    type ChainDefToken = JwtToken<ChainDef>
    let internal chainDefToHeader kid = fun () -> { kid = kid; pos = -1L }
    let internal chainDefFromHeader: JwtFromHeader = fun dc -> { kid = Convert.ToUInt16(dc.["kid"]); pos = -1L }

    let toChainDefToken kid = toJwt (chainDefToHeader kid)
    let fromChainDefToken<'T> = fromJwt<'T> chainDefFromHeader


    type ChainItemToken<'T> = JwtToken<'T>
    let internal chainItemToHeader kid pos = fun () -> { kid = kid; pos = pos }
    let internal chainItemFromHeader: JwtFromHeader = fun dc -> { kid = Convert.ToUInt16(dc.["kid"]); pos = Convert.ToInt64(dc.["pos"]) }

    let toChainItemToken kid pos = toJwt (chainItemToHeader kid pos)
    let fromChainItemToken<'T> = fromJwt<'T> chainItemFromHeader
