

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

    type Token = { Token: string }

    // type DVVClock = {
    //     Nodes: Map<NodeRef, Pos>
    // }

open Chain    
open Crdt

[<RequireQualifiedAccess>]
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
        Tk: Token
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
        | UnmatchedAsset of UnmatchedAsset
        | NotEnoughFunds of NotEnoughFunds
        | FailedConflictResolution
    and NotEnoughFunds = {
        Available: Amount
        Expected: Amount
    } 
    and UnmatchedAsset = {
        Supplied: Token
        Expected: Token
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
        //CryptoContext: CryptoContext
    } with 
        member __.Acknowledge (transaction: TransactionRequest): TransactionAccepted = failwith "Not implemented"
        member __.Reject (transaction: TransactionRequest): TransactionRejected = failwith "Not implemented"
    

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
    
    type TransactionDAG =  // TODO: Validate CRDT data structure
        TransactionRequest list
    and PendingTransactionsBag = Set<Transaction> // TODO: Add per-account indexing

    type [<RequireQualifiedAccess>] AccountStatus = | Active | Inactive | Conflict | Blocked | Deleted
    type AccountState = {
        ARef: AccountRef
        Clock: VClock 
        Amount: Amount
        Tk: Token       
        Status: AccountStatus
        PendingTransactions: PendingTransactionsBag
        AcknowledgedTransactions: TransactionDAG
    } with 
        static member CreateNew aRef clock tk = {
            ARef = aRef
            Clock = clock
            Amount = 0M<amount>
            Tk = tk
            Status = AccountStatus.Active
            PendingTransactions = Set []
            AcknowledgedTransactions = []
        }
        member __.GetBalance(): Amount * VClock = __.Amount, __.Clock
        member private __.UpdateAmount tr delta = { __ with Amount = __.Amount - delta; 
                                                            AcknowledgedTransactions = tr :: __.AcknowledgedTransactions 
                                                            Clock = __.Clock |> VClock.inc __.ARef.Address }
        member __.AddTransaction (tr: TransactionRequest): Result<AccountState, RejectionReason> = 
            if __.Tk <> tr.T.Tk then Error(UnmatchedAsset { Expected = __.Tk; Supplied = tr.T.Tk })
            elif tr.T.From = tr.T.To then Error SameAccountTransactionsNotSupported
            elif __.ARef = tr.T.From then
                if __.Amount < tr.T.Amount then Error(NotEnoughFunds { Expected = __.Amount; Available = tr.T.Amount })
                else Ok (__.UpdateAmount tr (- tr.T.Amount))
            elif __.ARef = tr.T.To then Ok (__.UpdateAmount tr tr.T.Amount)
            else Error IncorrectTransactionRequestFormat

    type Balances = {
        NRef: NodeRef
        Clock: VClock 
        Balances: Map<AccountRef, AccountState>
    } with
        member __.AddTransaction (tr: TransactionRequest): Result<Balances, RejectionReason list> = 
            let fromState = __.Balances |> Map.tryFind tr.T.From |> Option.defaultWith (fun () -> AccountState.CreateNew tr.T.From __.Clock tr.T.Tk) // TODO: Revisit this, using Balances clock and lazy creation in general is probably a bad idea
            let toState = __.Balances |> Map.tryFind tr.T.To |> Option.defaultWith (fun () -> AccountState.CreateNew tr.T.To __.Clock tr.T.Tk) // TODO: Revisit this, using Balances clock and lazy creation in general is probably a bad idea
            let updatedFromState = fromState.AddTransaction tr
            let updatedToState = toState.AddTransaction tr
            match updatedFromState, updatedToState with 
            | Ok fs, Ok ts -> Ok { __ with Balances = __.Balances.Add(fs.ARef, fs).Add(ts.ARef, ts); Clock = __.Clock |> VClock.inc __.NRef.Nid }
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
        | NodeMessage
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

open Crdt
#time

let rec incTest (vclock: VClock) iteration =
    if iteration > 0 then incTest (vclock |> VClock.inc (sprintf "rep%d" (iteration % 100))) (iteration - 1)
    else printfn "%A" vclock

incTest (Map []) 1000000