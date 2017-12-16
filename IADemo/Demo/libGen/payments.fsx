// #r "../packages/Inferno/lib/net452/SecurityDriven.Inferno.dll"
#r "../packages/System.Collections.Immutable/lib/portable-net45+win8+wp8+wpa81/System.Collections.Immutable.dll"
#r "../packages/jose-jwt/lib/net40/jose-jwt.dll"
#r "../packages/Newtonsoft.Json/lib/net45/Newtonsoft.Json.dll"
#r "../packages/Microsoft.FSharpLu.Json/lib/net452/Microsoft.FSharpLu.Json.dll"
#r "../packages/FSharpx.Collections/lib/net40/FSharpx.Collections.dll"


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

module Chains = 
    type ChainRef = Ref // TODO: Change
    // type ChainType = Simple | Blocked // Simple chains are optimized for latency, Blocked - for scalability
    type ChainDef = { 
        Ref: ChainRef 
        Uid: Uid
        MaxBlockSize: uint32
        // ChainType: ChainType
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
        Parent: Ref
        BlockPos: BlockPos
        From: Pos
        Tokens: ImmutableArray<Token>
        //BlockToken: Token
    }
        with member __.Size = __.Tokens.Length |> uint32

    type TopBlock = {
        ChainRef: ChainRef
        Parent: Ref
        From: Pos
        BlockPos: BlockPos
        PendingTokens: Token list
        Size: uint32
    }
    with 
        //member __.Size = __.PendingTokens |> List.length |> uint32 // TODO: Refactor this for performance
        member __.Add token = { __ with PendingTokens = token :: __.PendingTokens; Size = __.Size + 1u}
        member __.Tokens with get() = (__.PendingTokens |> List.rev).ToImmutableArray()
        static member New chainRef parent blockPos fromPos = {  ChainRef = chainRef
                                                                Parent = parent
                                                                From = { Pos = fromPos }  
                                                                BlockPos = { BlockPos = blockPos }
                                                                PendingTokens = [] 
                                                                Size = 0u }

    type ChainBlockRef = Ref
    type ChainBlockToken = {
        Ref: ChainBlockRef
        Token: Token
    }
    type ChainSpine = {
        ChainRef: ChainRef
        Blocks: ImmutableArray<ChainBlockRef * BlockPos * Pos * uint32>
    }
    with member __.Add blockRef = { __ with Blocks = __.Blocks.Add blockRef } 

    type [<Interface>] IBlockLoader = 
        abstract LoadBlocks: blockRefs: ChainBlockRef -> ImmutableArray<ChainBlock>



    type ChainData = {
        ChainDef: ChainDef
        Spine: ChainSpine
        Blocks: Map<BlockPos, ChainBlock>
        BlockTokens: ImmutableArray<ChainBlockToken>
        TopBlock: TopBlock
    }
    with 
        member __.Ref = __.ChainDef.Ref
        member __.Size = __.TopBlock.From.Pos + (__.TopBlock.Size |> uint64) 
        static member New chainDef = {  ChainDef = chainDef
                                        Spine = { ChainRef = chainDef.Ref; Blocks = ImmutableArray.Create() }
                                        Blocks = Map.empty
                                        BlockTokens = ImmutableArray.Create()
                                        TopBlock = TopBlock.New chainDef.Ref chainDef.Ref 0u 0UL }

    type ChainEvent =
    | Received of chainRef: ChainRef * tokenRef: Ref
    | AssignedBlock of chainRef: ChainRef * tokenRef: Ref
    | ConfirmedInBlock of chainRef: ChainRef * tokenRef: Ref * blockRef: ChainBlockRef

    type Chain(initial: ChainData, deref: Token -> Ref, blockTokenizer: ChainBlock -> ChainBlockToken, dispatcher: ChainEvent -> unit) =
        let mutable chain = initial
        new (chainDef: ChainDef, deref: Token -> Ref, blockTokenizer: ChainBlock -> ChainBlockToken, dispatcher: ChainEvent -> unit) = 
            Chain(ChainData.New chainDef, deref, blockTokenizer, dispatcher)
        member __.Chain with get() = chain
        member __.Add token = 
            let tokenRef = token |> deref
            Received(chain.Ref, tokenRef) |> dispatcher 
            let newChain, newBlockTokens =    
                if int(chain.TopBlock.Size) < int(chain.ChainDef.MaxBlockSize) - 1 then 
                    { chain with TopBlock = chain.TopBlock.Add token }, ImmutableArray.Empty
                else
                    let tokens = chain.TopBlock.Tokens.Add token
                    let newBlock = {ChainRef = chain.Ref
                                    Parent = chain.TopBlock.Parent
                                    From = chain.TopBlock.From
                                    BlockPos = chain.TopBlock.BlockPos
                                    Tokens = tokens } 
                    let blockToken = blockTokenizer newBlock
                    { chain with    Blocks = chain.Blocks.Add(newBlock.BlockPos, newBlock)
                                    BlockTokens = chain.BlockTokens.Add blockToken
                                    Spine = chain.Spine.Add (blockToken.Ref, newBlock.BlockPos, newBlock.From, newBlock.Size) 
                                    TopBlock = TopBlock.New chain.Ref blockToken.Ref (newBlock.BlockPos.BlockPos + 1u) (newBlock.From.Pos + uint64(newBlock.Size)) }, tokens
            AssignedBlock(chain.Ref, tokenRef) |> dispatcher
            chain <- newChain
            for bt in newBlockTokens do ConfirmedInBlock(chain.Ref, tokenRef, bt |> deref) |> dispatcher 
        member __.TokenPage (from: Pos) (size: uint32) = 
            // chain.Spine.Blocks 
            // |> Seq.filter (fun (cbr, p, s) -> p.Pos + uint64(s) < from.Pos)
            // |> 
                let blocksMap = chain.Blocks
                let blocks = chain.Spine.Blocks 
                                |> Seq.filter (fun (cbr, bp, p, s) -> p.Pos + uint64(s) >= from.Pos)
                                |> Seq.collect (fun (cbr, bp, p, s) -> blocksMap.[bp].Tokens |> Seq.mapi (fun i t -> t, p.Pos + uint64(i)))

                let topBlock = 
                    let topFrom = chain.TopBlock.From.Pos
                    chain.TopBlock.PendingTokens |> Seq.mapi (fun i t -> t, topFrom + uint64(i))
                Seq.append blocks topBlock
                |> Seq.skipWhile(fun (t, i) -> i < from.Pos)
                |> Seq.takeWhile(fun (t, i) -> i < from.Pos + uint64(size))

        //     Seq.append (chain.Spine.Blocks ) chain.TopBlock.



let chainDef: Chains.ChainDef = {
    Ref = Ref "chain1"
    Uid = Guid.NewGuid()
    MaxBlockSize = 10000u
}

let chain = 
    Chains.Chain(chainDef, 
                    (fun (Token t) -> Ref t), 
                    (fun cb -> {//Chains.ChainBlockToken.Token = (Microsoft.FSharpLu.Json.Compact.serialize cb |> Token); 
                                Chains.ChainBlockToken.Token = cb.Tokens.[0] //|> Token
                                Chains.ChainBlockToken.Ref = Guid.NewGuid().ToString() |> Ref }), 
                    //(fun e -> printfn "Event: %A" e))
                    ignore)

//let testTokens = [| for i in 0 .. 100000 -> Guid.NewGuid().ToString("N") |> Token |]
// let testTokens = seq { for i in 0 .. 10000000 -> i.ToString("N") |> Token }
// testTokens |> Seq.iter (chain.Add)
// let page0 = (chain.TokenPage { Pos = 0UL } 7u) |> Seq.toArray
// let page10 = (chain.TokenPage { Pos = 10UL } 7u) |> Seq.toArray
// let page100 = (chain.TokenPage { Pos = 100UL } 7u) |> Seq.toArray
// let page995 = (chain.TokenPage { Pos = 995UL } 7u) |> Seq.toArray
// let page10000 = (chain.TokenPage { Pos = 10000UL } 7u) |> Seq.toArray

// let page100000 = (chain.TokenPage { Pos = 100000UL } 7u) |> Seq.toArray
// let page1000000 = (chain.TokenPage { Pos = 1000000UL } 7u) |> Seq.toArray
// let page888888 = (chain.TokenPage { Pos = 888888UL } 200000u) |> Seq.toArray
// (chain.TokenPage { Pos = 888888UL } 200000u) |> Seq.iter (ignore)


open FSharpx.Collections
let mutable vector = PersistentVector.empty<Token>
let testTokens1 = seq { for i in 0 .. 50000000 -> i.ToString("N") |> Token }
for t in testTokens1 do vector <- vector.Conj t


chain.Chain.TopBlock
chain.Chain.Spine
chain.Chain.Blocks.[{BlockPos = 0u}].Size

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


