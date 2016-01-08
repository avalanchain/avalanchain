module Avalanchain.StreamEvent

open System

open FSharp.Core.Fluent
open Chessie.ErrorHandling

open SecPrimitives
open SecKeys
open RefsAndPathes
   

type SubmitterSignature = Signature
and SubmitterKey = SigningPublicKey

//type EventData<'TData> = 
//    | Unencrypted of 'TData
//    | Encrypted of 'TData * Encryption
//    with member this.Data = match this with 
//                            | Unencrypted d -> d
//                            | Encrypted (d, _) -> d

type Event<'TData> = {
    //Data: EventData<'TData>
    Data: 'TData
    SubmitterKey: SigningPublicKey
    SubmitterSignature: SubmitterSignature
    SubmittedVia: NodeRef
}
and HashedEvent<'TData> = Hashed<Event<'TData>>
and MerkledEvent<'TData> = Merkled<Event<'TData>> //* ExecutionProof
and EventRef = Hash
and EventSpine = MerkleTree

