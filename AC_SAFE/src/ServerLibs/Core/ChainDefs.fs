namespace Avalanchain.Core

module ChainDefs =
    open System
    open Crypto

    type Hash = { Hash: string } // TODO: Redo this
    type Sig = { Sig: string } // TODO: Redo this

    type Uid = 
        | UUID of Guid
        | Hash of Hash
        | Sig of Sig

    type NodeRef = { Nid: string }
    type MasterNodeRef = { MNid: string }

    type NodeProof = { NRef: NodeRef; Sig: Sig }
    type MasterNodeProof = { MRef: MasterNodeRef; Sig: Sig }

    type Asset = { Asset: string }

    // type DVVClock = {
    //     Nodes: Map<NodeRef, Pos>
    // }

    type Json = Json of string
    type JsFunc = string // TODO
    type Func1 = Jsf1 of JsFunc
    type Func2 = Jsf2 of JsFunc

    type Derivation =
        | Fork
        | Map of Func1
        | Filter of Func1
        | Fold of Func2 * init: Json
        | Reduce of Func2
        | FilterFold of filter: Func1 * folder: Func2
        | GroupBy of groupper: Func1 * max: uint32

    type ChainRef = TokenRef

//    type Kid = uint16
//    type PublicKey = PublicKey of obj 
//    type PrivateKey = PrivateKey of obj 
//    type KeyPair = {
//        PublicKey: PublicKey
//        PrivateKey: PrivateKey
//        Kid: Kid
//    }
//    type KeyStorage = Kid -> KeyPair

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
    
    

    type ChainDefToken = JwtToken<ChainDef>
    let internal chainDefToHeader keyPair = JwtTokenHeader.Create keyPair.Kid None
//    let internal chainDefFromHeader: JwtFromHeader = fun dc -> { kid = Convert.ToUInt16(dc.["kid"]); pos = -1L; alg = JwtAlgoAsym.ES384.ToString(); enc = false }

//    let toChainToken keyPair = fun pos ->  toJwt (fun () -> { kid = keyPair.Kid; pos = pos; alg = JwtAlgoAsym.ES384.ToString(); enc = false }) keyPair
    let toChainDefToken keyPair = toJwt keyPair (chainDefToHeader keyPair) 
    let fromChainDefToken keyVault = fromJwt<ChainDef> keyVault


    type ChainItemToken<'T> = JwtToken<'T>
    let internal chainItemToHeader keyPair pos = JwtTokenHeader.Create keyPair.Kid (Some pos)
//    let internal chainItemFromHeader: JwtFromHeader = fun dc -> { kid = Convert.ToUInt16(dc.["kid"]); pos = Convert.ToInt64(dc.["pos"]); alg = JwtAlgoAsym.ES384.ToString(); enc = false }

    let toChainItemToken keyPair pos = toJwt keyPair (chainItemToHeader keyPair pos)
    let fromChainItemToken<'T> keyVault = fromJwt<'T> keyVault

