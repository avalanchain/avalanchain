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
open System.Collections.Concurrent
open System.Security.Cryptography
open Newtonsoft.Json
open FSharpLu.Json

JsonConvert.DefaultSettings <- fun () ->
    let settings = JsonSerializerSettings()
    settings.ContractResolver <- new Serialization.CamelCasePropertyNamesContractResolver()
    settings

type Uid = Guid
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
    type ChainDef = { 
        Ref: ChainRef 
        Uid: Uid
        MaxBlockSize: uint32
    }

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
        Tokens: ImmutableArray<Token>
        //BlockToken: Token
    }
        with member __.Size = __.Tokens.Length

    type TopBlock = {
        ChainRef: ChainRef
        From: Pos
        BlockPos: BlockPos
        PendingTokens: Token list
    }
    with 
        member __.Size = __.PendingTokens |> List.length |> uint32 // TODO: Refactor this for performance
        member __.Add token = { __ with PendingTokens = token :: __.PendingTokens }
        member __.Tokens with get() = (__.PendingTokens |> List.rev).ToImmutableArray()
        static member New chainRef fromPos = {  ChainRef = chainRef
                                                From = { Pos = fromPos }  
                                                BlockPos = { BlockPos = 0u }
                                                PendingTokens = [] }

    type ChainBlockRef = Ref
    type ChainSpine = {
        ChainRef: ChainRef
        Blocks: ImmutableArray<ChainBlockRef>
    }
    with member Add 

    type [<Interface>] IBlockLoader = 
        abstract LoadBlocks: blockRefs: ChainBlockRef -> ImmutableArray<ChainBlock>



    type Chain = {
        ChainDef: ChainDef
        Spine: ChainSpine
        Blocks: Map<BlockPos, ChainBlock>
        TopBlock: TopBlock
    }
    with 
        member __.Ref = __.ChainDef.Ref
        member __.Size = __.TopBlock.From.Pos + (__.TopBlock.Size |> uint64) 
        static member New chainDef = {  ChainDef = chainDef
                                        Spine = { ChainRef = chainDef.Ref; Blocks = ImmutableArray.Create() }
                                        Blocks = Map.empty
                                        TopBlock = TopBlock.New chainDef.Ref 0UL }

    type ChainManager(initialChain: Chain, blockRefCalc: ChainBlock -> ChainBlockRef) =
        let mutable chain = initialChain
        member __.Add token = 
            chain <-    if chain.TopBlock.Size < chain.ChainDef.MaxBlockSize then { chain with TopBlock = chain.TopBlock.Add token }

                        else
                            let newBloclk = {   ChainRef = chain.Ref
                                                From = chain.TopBlock.From
                                                BlockPos = chain.TopBlock.BlockPos
                                                Tokens = chain.TopBlock.Tokens } 
                            { chain with    Blocks = chain.Blocks.Add(newBloclk.BlockPos, newBloclk)
                                            Spine = chain.Spine.}

    // type Chain (def: ChainDef, spine: ChainSpine, pendingRequsts: , blockLoader: IBlockLoader) =
    //     let blocks = ConcurrentDictionary<BlockPos, ChainBlock>()
    //     let mutable topBlock = 
    //     member __.Def = def
    //     member __.Ref = def.Ref
    //     member __.Blocks = blocks.Values.ToImmutableArray()
    //     member __.PendingRequests = 
    //     member __.AddBlock block = blocks.AddOrUpdate (block.BlockPos, (fun bp -> block), (Func<_,_,_>(fun bp b -> block))) |> ignore
    //     member __.DropBlocks (bps: BlockPos seq) = for bp in bps do blocks.TryRemove(bp) |> ignore
    //     member __.LastBlock with get() = if blocks.Count > 0 then blocks.Values |> Seq.maxBy (fun cb -> cb.BlockPos.BlockPos) |> Some else None
    //     member __.IsFullChain() = blocks |> Seq.mapi(fun i kv -> i = int(kv.Value.BlockPos.BlockPos)) |> Seq.forall id


[<RequireQualifiedAccess>]
module Customer =
    type AccountBalances = {
        Balances: Map<PaymentAccountRef, AccountBalances>
    }

    type AccountDetails = {
        Name: string
    }

    type Account = {
        Ref: PaymentAccountRef
        Details: AccountDetails
        KeyStorage: Credentials.IKeyStorage
        CryptoContext: ICryptoContext
    }
    with member __.Name = __.Details.Name


