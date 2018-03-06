namespace Avalanchain.Core

module Payments = 
    open Crdt
    open Crypto
    open Chain

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
