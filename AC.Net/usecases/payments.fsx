// #r "../packages/Inferno/lib/net452/SecurityDriven.Inferno.dll"
#r "../packages/System.Collections.Immutable/lib/portable-net45+win8+wp8+wpa81/System.Collections.Immutable.dll"
#r "../packages/jose-jwt/lib/net40/jose-jwt.dll"
#r "../packages/Newtonsoft.Json/lib/net45/Newtonsoft.Json.dll"
#r "../packages/Microsoft.FSharpLu.Json/lib/net452/Microsoft.FSharpLu.Json.dll"


#time

// module Payments = 
open Security.Cryptography
open System
open System.Collections.Immutable
open System.Security.Cryptography
open Newtonsoft.Json
open FSharpLu.Json

JsonConvert.DefaultSettings <- fun () ->
    let settings = JsonSerializerSettings()
    settings.ContractResolver <- new Serialization.CamelCasePropertyNamesContractResolver()
    settings

type Ref = Ref of string
type Pos = { Pos: uint64 }
type BlockPos = { BlockPos: uint32 }

type PaymentAccountRef = {
    Address: string
}

type PaymentAmount = decimal<pa>
and [<Measure>] pa // PaymentAmount

type Asset = Asset of string

type PaymentTransactionItem = {
    From: PaymentAccountRef
    To: PaymentAccountRef 
    Amount: decimal<pa>
    Asset: Asset
}

type PaymentTransaction =
    | S of PaymentTransactionItem // Single transfer
    | L of PaymentTransactionItem list // List of transfers

// type ChainRequest =
//     | PT of PaymentTransaction
//     | RPL of ReplayRequest
// and ReplayRequest = { pos: Pos }


let transaction = { From = { Address = "Addr1"} 
                    To = { Address = "Addr2"}
                    Amount = 1000M<pa>
                    Asset = Asset "ACTX" } |> S

let transactionJson = Microsoft.FSharpLu.Json.Compact.serialize transaction

printfn "%s" transactionJson

let transactions = [{   From = { Address = "Addr1"} 
                        To = { Address = "Addr2"}
                        Amount = 1000M<pa> 
                        Asset = Asset "ACTX" } 
                    {   From = { Address = "Addr3"} 
                        To = { Address = "Addr4"}
                        Amount = 1500M<pa>
                        Asset = Asset "AAC" }                             
                    ] |> L

let transactionsJson = Microsoft.FSharpLu.Json.Compact.serialize transactions
printfn "%s" transactionsJson

let transactions2 = Microsoft.FSharpLu.Json.Compact.deserialize<PaymentTransaction> transactionsJson

let transactionJson2 = Microsoft.FSharpLu.Json.Compact.serialize transaction
printfn "%s" transactionJson2

type AccountBalances = {
    Balances: Map<Asset, PaymentAmount>
}

type TransactionRejectionReason =
    | WrongHash 
    | WrongSignature
    | AccountNotExists of PaymentAccountRef
    | InvalidAmount of PaymentAmount
    | NotEnoughFunds of NotEnoughFunds
and NotEnoughFunds = {
    Available: PaymentAmount option // TODO: Check for security setting when using (that's why 'option')
    Expected: PaymentAmount
    Account: PaymentAccountRef
} 

type RejectedTransaction = {
    Request: PaymentTransaction
    Reason: TransactionRejectionReason
}

type ConfirmationId = CID of string 
module Confirmations =
    type [<Interface>] IConfirmation = 
        abstract CID: ConfirmationId

    // [<RequireQualifiedAccess>]
    // type ConfirmationStorageRequest = 
    //     | Get of CID: ConfirmationId * MaxCount: uint8
    //     | Count of CID: ConfirmationId

    // [<RequireQualifiedAccess>]
    // type ConfirmationStorageResponse = 
    //     | Get of CID: ConfirmationId * MaxCount: IConfirmation list
    //     | Count of CID: ConfirmationId * uint32        

    type [<Interface>] IConfirmationStorage = 
        abstract Get: ConfirmationId * ?maxCount:uint8 -> IConfirmation list
        abstract GetCount: ConfirmationId -> int32 option

type StoredTransaction = {
    Request: PaymentTransaction
    Pos: Pos
    TimeStamp: DateTimeOffset
    Confirmations: Set<ConfirmationId>
}    

type Token = Token of string

type [<Interface>] ICryptoContext = 
    abstract Sign: string -> Token
    abstract Validate: Token -> 'T option

[<RequireQualifiedAccess>]    
module Credentials =
    type PublicKey = PublicKey of byte[]
    type PrivateKey = PrivateKey of byte[]

    type [<Interface>] IKeyStorage = 
        abstract KnownKeys: unit -> PublicKey[]
        abstract Validate: PublicKey -> bool
        abstract SigningKey: unit -> PrivateKey
        abstract SigningKeys: unit -> PrivateKey[]

module Chain = 
    type ChainRef = Ref // TODO: Change
    type ChainDef = { Ref: ChainRef }

    type [<Interface>] IChainedToken = 
        abstract Ref: Ref
        abstract Pos: Pos
        abstract PRef: Ref

    // type CT = {
    //     Ref: Ref
    //     Pos: Pos
    //     PRef: Ref
    // } 
    // with interface IChainedToken 
    //         with    member __.Ref = __.Ref
    //                 member __.PRef = __.PRef
    //                 member __.Pos = __.Pos

    type ChainBlock = {
        ChainRef: ChainRef
        From: Pos
        BlockPos: BlockPos
        MaxSize: uint32
        Tokens: ImmutableList<Token>
        // TokensHash
    }

    type ChainBlockRef = Ref

    type Chain (def: ChainDef) =
        let blocks = ImmutableSortedDictionary.Create<BlockPos, ChainBlock>()
        member __.Def = def
        member __.Ref = def.Ref
        member __.Blocks = blocks.Values.ToImmutableArray()
        member __.AddBlock block = blocks.Add (block.BlockPos, block)
        member __.DropBlocks (bps: BlockPos seq) = blocks.RemoveRange(bps)
        member __.IsFullChain() = blocks |> Seq.mapi(fun i kv -> i = int(kv.Value.BlockPos.BlockPos)) |> Seq.forall id

[<RequireQualifiedAccess>]
module Customer =
    type AccountBalances = {
        Balances: Map<PaymentAccountRef, AccountBalances>
    }

    type Credentials = Credentials

    type AccountDetails = {
        Name: string
    }

    type Account = {
        Ref: PaymentAccountRef
        Details: AccountDetails
        Credentials: Credentials
        CryptoContext: ICryptoContext
    }
    with member __.Name = __.Details.Name


