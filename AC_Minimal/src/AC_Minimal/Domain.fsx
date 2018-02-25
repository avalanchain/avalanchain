#r "System.Collections.Immutable"

open System
open System.Security.Cryptography
open FSharp.Control
//open Avalanchain.SecKeys

module Crdt =
    type ReplicaId = string
    type Pos = uint64

    module GCounter =  
        type GCounter = Map<ReplicaId, Pos>
        let zero: GCounter = Map.empty
        let value (c: GCounter) = 
            c |> Map.fold (fun acc _ v -> acc + v) 0UL
        let inc r (c: GCounter) =
            match Map.tryFind r c with
            | Some x -> Map.add r (x + 1UL) c
            | None   -> Map.add r 1UL c
        let merge (a: GCounter) (b: GCounter): GCounter =
            a |> Map.fold (fun acc ka va ->
                match Map.tryFind ka acc with
                | Some vb -> Map.add ka (max va vb) acc
                | None    -> Map.add ka va acc) b
                

    type Ord =  
    | Lt = -1  // lower
    | Eq = 0   // equal
    | Gt = 1   // greater
    | Cc = 2   // concurrent

    type VClock = GCounter.GCounter
    module VClock =  
        let zero = GCounter.zero
        let inc = GCounter.inc
        let merge = GCounter.merge
        let compare (a: VClock) (b: VClock): Ord = 
            let valOrDefault k map =
                match Map.tryFind k map with
                | Some v -> v
                | None   -> 0UL
            let akeys = a |> Map.toSeq |> Seq.map fst |> Set.ofSeq
            let bkeys = b |> Map.toSeq |> Seq.map fst |> Set.ofSeq
            (akeys + bkeys)
            |> Seq.fold (fun prev k ->
                let va = valOrDefault k a
                let vb = valOrDefault k b
                match prev with
                | Ord.Eq when va > vb -> Ord.Gt
                | Ord.Eq when va < vb -> Ord.Lt
                | Ord.Lt when va > vb -> Ord.Cc
                | Ord.Gt when va < vb -> Ord.Cc
                | _ -> prev ) Ord.Eq

module Crypto = 
    // TODO: Readjust this
    type SigningPublicKey = byte[]
    type SignedProof<'T> = 'T

    let hasher = SHA256.Create()

    let sha (s: string) = 
        hasher.ComputeHash(Text.ASCIIEncoding.UTF8.GetBytes s) |> Convert.ToBase64String    

    type CryptoContext (*<'TData>*) = {
//        Hasher: Hasher
        SigningPublicKey: SigningPublicKey
//        Signer: Signer
//        Verifier: Verifier
//        EncryptionPublicKey: EncryptionPublicKey 
//        //PrivateKey: PrivateKey
//        //SignatureMethod: Signature
//        EncryptionMethod: Encryption
//        Encryptor: Encryptor
//        Decryptor: Decryptor
//        Dispose: unit -> unit
    }
    with 
//        member this.ProofVerifier proof = this.Verifier proof.Signature (Unsigned proof.ValueHash.Bytes)
//        member this.HashSigner (hash: Hash) = this.Signer (Unsigned hash.Bytes)
//        member this.Address = this.SigningPublicKey |> this.Hasher |> (fun h -> Base58CheckEncoding.Encode h.Bytes)
        member this.Address = "TBD"

    let cryptoContextNamed name: CryptoContext = { SigningPublicKey = [||] }

open Crypto

module Chain =
    type Hash = Guid // TODO: Redo this
    type Sig = { Sig: string } // TODO: Redo this

    type Uid = 
    | UUID of Guid
    | Hash of Hash
    | Sig of Sig

    type NodeRef = { Nid: string }
    type MasterNodeRef = { MNid: string }

    type NodeProof = { NRef: NodeRef; Sig: Sig }
    type MasterNodeProof = { MRef: MasterNodeRef; Sig: Sig }

    type ChainRef = { Cid: string }

    type Asset = { Asset: string }

    // type DVVClock = {
    //     Nodes: Map<NodeRef, Pos>
    // }

open Chain    
open Crdt

module Payments = 
    type AccountRef = { Address: string }
    type AccountProof = { ARef: AccountRef; Sig: Sig }

    [<RequireQualifiedAccess>]
    type Proof = 
        | Account of AccountProof
        | Node of NodeProof
        | MasterNode of MasterNodeProof


    type Amount = decimal<amount>
    and [<Measure>] amount

    type Transaction = {
        Clock: VClock
        From: AccountRef
        To: AccountRef
        Amount: Amount
        Asset: Asset
    }

    type TransactionRef = Sig

    type TransactionRequest = {
        T: Transaction
        Ref: TransactionRef
    }

    type RejectionReason =
        | WrongHash 
        | WrongSignature
        | FromAccountNotExists of AccountRef
        | UnexpectedNegativeAmount of Amount
        | SameAccountTransactionsNotSupported
        | IncorrectTransactionRequestFormat
        | MissingAcknowledgement
        | UnmatchedAsset of UnmatchedAsset
        | NotEnoughFunds of NotEnoughFunds
        | PendingTransactionAlreadyProcessed of TransactionRef
        | FailedConflictResolution
    and NotEnoughFunds = {
        Available: Amount
        Expected: Amount
    } 
    and UnmatchedAsset = {
        Supplied: Asset
        Expected: Asset
    }

    type TransactionAccepted = {
        Tr: TransactionRequest
        APrf: AccountProof
        NPrf: NodeProof
    }

    type TransactionNodeRejected = {
        Tr: TransactionRequest
        Reason: RejectionReason
        NPrf: NodeProof
    }

    type TransactionAccountRejected = {
        Tr: TransactionRequest
        Reason: RejectionReason
        APrf: AccountProof
    }

    type TransactionNodeConfirmation = {
        TRef: TransactionRef
        NPrf: NodeProof
    } 

    [<RequireQualifiedAccess>]
    type TransactionRejected = 
        | Account of TransactionAccountRejected
        | Node of TransactionNodeRejected

    type TransactionResult = Result<TransactionAccepted, TransactionRejected>

    type TransactionNotification =
        | Replayed of Transaction // TODO: Add proofs
        | Cancelled of Transaction // TODO: Add proofs

    //type HashedPT = SignedProof<Transaction>

    // type StoredTransaction = {
    //     Result: Result<Transaction, RejectionStatus>
    //     Balances: Balances
    //     TimeStamp: DateTimeOffset
    // }

    type Account = {
        ARef: AccountRef
        PublicKey: SigningPublicKey
        Name: string
        NextClock: unit -> VClock
        Asset: Asset
        //CryptoContext: CryptoContext
    } with 
        member __.Sign<'T> (payload: 'T) = { Sig = sprintf "<'%s' '%s' Sig '%s'>" __.Name __.ARef.Address (payload.ToString()) }  // TODO: Change signing
        member __.Proof<'T> (payload: 'T) = { ARef = __.ARef; Sig = __.Sign payload }
        member __.Acknowledge (tr: TransactionRequest): TransactionRequest * AccountProof = tr, __.Proof tr
        member __.Reject (tr: TransactionRequest) reason: TransactionAccountRejected = 
            {   Tr = tr
                Reason = reason
                APrf = __.Proof tr }

        member __.PayTo toARef amount: TransactionRequest = 
            let t = {   From = __.ARef
                        To = toARef
                        Asset = __.Asset 
                        Amount = amount
                        Clock = __.NextClock() } 
            { T = t; Ref = __.Sign t }

    module Account =
        let create asset aref name nextClock = {ARef = aref
                                                PublicKey = [||]
                                                Name = name
                                                NextClock = nextClock
                                                Asset = asset }


    type Wallet = {
        Accounts: Map<AccountRef, Account>
    }

    // type AccountStateSnapshot = {
    //     ARef: AccountRef
    //     Transactions: TransactionDAG
    // }

    // type TransactionDAG() =  // TODO: Validate CRDT data structure
    //     (VClock * Transaction) list
    //     member __.A0

    [<RequireQualifiedAccess>]
    type Acknowledge = 
        | Auto 
        | Manual of AccountProof
    
    [<RequireQualifiedAccess>]
    type Acknowledged = 
        | Auto of VClock
        | Manual of VClock * AccountProof
    module AcknowledgeType = 
        let ofAck clock = function
                            | Acknowledge.Auto -> Acknowledged.Auto clock
                            | Acknowledge.Manual proof -> Acknowledged.Manual (clock, proof)

    type TransactionDAGItem = {
        Tr: TransactionRequest
        Acknowledged: Acknowledged
    }

    type TransactionDAG = TransactionDAGItem list // TODO: Validate CRDT data structure

    type PendingTransactionsBag = Map<TransactionRef, TransactionRequest> // TODO: Add per-account indexing

    [<RequireQualifiedAccess>]
    type AccountStatus = | Active | Inactive | Conflict | Blocked | Deleted
    
    type AccountState = {
        ARef: AccountRef
        Clock: VClock 
        Amount: Amount
        Asset: Asset       
        Status: AccountStatus
        PendingTransactions: PendingTransactionsBag
        AcknowledgedTransactions: TransactionDAG
    }

    module AccountState =
        let inline create amount aRef asset clock = {
            ARef = aRef
            Clock = clock
            Amount = amount
            Asset = asset
            Status = AccountStatus.Active
            PendingTransactions = Map.empty
            AcknowledgedTransactions = []
        }
        let inline createZero aRef asset clock = create 0M<amount> aRef asset clock
        let inline balance (state: AccountState): Amount * VClock = state.Amount, state.Clock
        let inline private updateClock (state: AccountState) = 
            {   state with Clock = state.Clock |> VClock.inc state.ARef.Address }
        let inline private updateAcknowledged acked (tr: TransactionRequest) delta (state: AccountState) = 
            {   state with  Amount = state.Amount + delta
                            AcknowledgedTransactions =  { Tr = tr; Acknowledged = acked } :: state.AcknowledgedTransactions }
            |> updateClock
        let inline private updatePending (tr: TransactionRequest) (state: AccountState) = 
            {   state with  PendingTransactions = state.PendingTransactions |> Map.add tr.Ref tr }
            |> updateClock
        let inline private applyTransaction ackedOpt (tr: TransactionRequest) (state: AccountState): Result<AccountState, RejectionReason> = 
            if state.Asset <> tr.T.Asset then Error(UnmatchedAsset { Expected = state.Asset; Supplied = tr.T.Asset })
            elif tr.T.From = tr.T.To then Error SameAccountTransactionsNotSupported
            elif state.ARef = tr.T.From then
                if state.Amount < tr.T.Amount then Error(NotEnoughFunds { Expected = tr.T.Amount; Available = state.Amount })
                else match ackedOpt with
                        | Some acked -> state |> updateAcknowledged acked tr (- tr.T.Amount) |> Ok
                        | None -> MissingAcknowledgement |> Error
            elif state.ARef = tr.T.To then match ackedOpt with
                                            | Some acked -> state |> updateAcknowledged acked tr tr.T.Amount |> Ok
                                            | None -> state |> updatePending tr |> Ok
            else Error IncorrectTransactionRequestFormat

        let addTransaction = applyTransaction 

        let inline acceptPending proof tref (state: AccountState): Result<AccountState, RejectionReason> =
            match state.PendingTransactions |> Map.tryFind tref with
            | Some tr ->    { state with PendingTransactions = state.PendingTransactions |> Map.remove tref }
                            |> applyTransaction (Acknowledged.Manual proof |> Some) tr 
            | None -> PendingTransactionAlreadyProcessed tref |> Error



    type Balances = {
        NRef: NodeRef
        Clock: VClock 
        Balances: Map<AccountRef, AccountState>
    }
    module Balances = 
        let create nid = {  NRef = { Nid = nid }
                            Clock = VClock.zero
                            Balances = Map.empty }
        let addAccount accState balances = { balances with Balances = balances.Balances |> Map.add accState.ARef accState }
        let addTransaction ackedOpt (tr: TransactionRequest) balances: Result<Balances, RejectionReason list> = 
            let fromState = balances.Balances |> Map.tryFind tr.T.From |> Option.defaultWith (fun () -> AccountState.createZero tr.T.From tr.T.Asset balances.Clock) // TODO: Revisit this, using Balances clock and lazy creation in general is probably a bad idea
            let toState = balances.Balances |> Map.tryFind tr.T.To |> Option.defaultWith (fun () -> AccountState.createZero tr.T.To tr.T.Asset balances.Clock) // TODO: Revisit this, using Balances clock and lazy creation in general is probably a bad idea
            let updatedFromState = fromState |> AccountState.addTransaction ackedOpt tr
            let updatedToState = toState |> AccountState.addTransaction ackedOpt tr
            match updatedFromState, updatedToState with 
            | Ok fs, Ok ts -> Ok { balances with Balances = balances.Balances.Add(fs.ARef, fs).Add(ts.ARef, ts); Clock = balances.Clock |> VClock.inc balances.NRef.Nid }
            | Error fr, Ok _ -> Error [ fr ]
            | Ok _, Error tr -> Error [ tr ]
            | Error fr, Error tr -> Error [ fr; tr ]




module Communication = 
    type MTPlcHld = string

    type ChainMessage =
        | System of SystemMessage
        | AccountMessage of AccountMessage
        | PaymentMessage of PaymentMessage
    and SystemMessage = 
        | AdminMessage of AdminMessage
        | NodeMessage of NodeMessage
    and AdminMessage =
        | RegisterAccountAccepted of MTPlcHld
        | RegisterAccountRejected of MTPlcHld
        | BlockAccount of MTPlcHld
        | ForgetAccount of MTPlcHld
        | RewriteHistory of MTPlcHld
    and AccountMessage = 
        | RegisterAccountRequest of MTPlcHld
    and PaymentMessage = 
        | SendPayment of Payments.TransactionRequest
        | PaymentAccepted of Payments.TransactionAccepted
        | PaymentRejected of MTPlcHld
        | PaymentRefunded of MTPlcHld
    and NodeMessage =
        | MasterAdded of NodeInfo
        | MasterRemoved of NodeInfo
        | ObserverAdded of NodeInfo
        | ObserverRemoved of NodeInfo
    and NodeInfo = MTPlcHld

    // type WorldState = {

    // }

    type Node = {
        Ref: NodeRef
        Wallets: Payments.Wallet list
        SendPayment: Payments.Account -> Payments.AccountRef -> Payments.Amount -> Async<Payments.TransactionResult>
    }


    // #time
    // let a = [| for i in 1 .. 10000000 -> i |] 
    // let b = [| for i in 1 .. 10000000 -> i |] |> List.ofArray |> Array.ofList
    // b.[9999999] <- 0
    // a = b

///////////////////// Tests

open Crdt
open System.Collections.Generic

open Payments

let aim = { Asset = "AIM" }
let aref1 = { Address = "acc1" }
let aref2 = { Address = "acc2" }

let mutable accState1 = (AccountState.create 100000000M<amount> aref1 aim VClock.zero)
let mutable accState2 = (AccountState.createZero aref2 aim VClock.zero)

let bals = "node1" 
            |> Balances.create
            |> Balances.addAccount accState1 
            |> Balances.addAccount accState2

let mutable clock = VClock.zero
let createAccount aref name = Account.create aim aref name (fun () -> clock <- clock |> VClock.inc aref.Address; clock )

let account1 = createAccount aref1 "ac1"
let account2 = createAccount aref2 "ac2"

let rec testTransfer amount iterations (bals: Balances) =
    if iterations > 0 then
        match bals |> Balances.addTransaction (Acknowledged.Auto VClock.zero |> Some) (account1.PayTo aref2 amount) with
        | Ok b -> testTransfer amount (iterations - 1) b
        | Error _ as er -> er
    else Ok bals

#time

let t1 = testTransfer 100M<amount> 10000 bals


let rec incTest (vclock: VClock) iteration =
    if iteration > 0 then incTest (vclock |> VClock.inc (sprintf "rep%d" (iteration % 100))) (iteration - 1)
    else printfn "%A" vclock

// incTest VClock.zero 1000000


let rec incTest2 wide (vclock: System.Collections.Generic.Dictionary<ReplicaId, Pos>) iteration =
    if iteration > 0 then 
        vclock.[sprintf "rep%d" (iteration % wide)] <- 1UL
        incTest2 wide vclock (iteration - 1)
    else printfn "%A" vclock

incTest2 100 (new System.Collections.Generic.Dictionary<ReplicaId, Pos>()) 1000000

let rec incTest3 wide (vclock: System.Collections.Concurrent.ConcurrentDictionary<ReplicaId, Pos>) iteration =
    if iteration > 0 then 
        vclock.[sprintf "rep%d" (iteration % wide)] <- 1UL
        incTest3 wide vclock (iteration - 1)
    else printfn "%A" vclock

incTest3 100 (new System.Collections.Concurrent.ConcurrentDictionary<ReplicaId, Pos>()) 1000000


let rec incTest4 wide (vclock: System.Collections.Immutable.ImmutableDictionary<ReplicaId, Pos>) iteration =
    if iteration > 0 then incTest4 wide (vclock.Add ((sprintf "rep%d" (iteration % wide)), 1UL)) (iteration - 1)
    else printfn "%A" vclock

incTest3 100 (new System.Collections.Concurrent.ConcurrentDictionary<ReplicaId, Pos>()) 1000000