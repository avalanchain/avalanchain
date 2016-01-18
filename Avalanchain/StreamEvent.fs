module Avalanchain.StreamEvent

open System

open FSharp.Core.Fluent
open Chessie.ErrorHandling

open SecPrimitives
open SecKeys
open RefsAndPathes
   

type SubmitterSignature = Signature
and SubmitterKey = SigningPublicKey

type Event<'TData> = {
    Data: 'TData
    SubmitterKey: SigningPublicKey
    SubmitterSignature: SubmitterSignature
    SubmittedVia: NodeRef
}
and HashedEvent<'TData> = Hashed<Event<'TData>>
and MerkledEvent<'TData> = Merkled<Event<'TData>> //* ExecutionProof
and EventRef = Hash
//and EventSpine = MerkleTree

