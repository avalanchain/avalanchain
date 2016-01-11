module Avalanchain.RefsAndPathes

open System

open FSharp.Core.Fluent
open Chessie.ErrorHandling

open SecPrimitives
open SecKeys

type Path = string // TODO: change to proper path logic
and EventStreamPath = Path 
and NodePath = Path

type EventStreamRef = {
    Path: EventStreamPath
    Version: uint32
}

type ExecutorSignature = Signature
and ExecutorKey = ExecutorKey of SigningPublicKey

type NodeRef = NodeRef of SigningPublicKey

type ExecutionProof = {
    Data: ExecutionProofData
    Signature: ExecutorSignature
    //Key: ExecutorKey // <-- exists in the signature itself
}
and ExecutionProofData = {
    StreamRefHash: Hash
    Nonce: Nonce
    EventHash: Hash
    StateHash: Hash
}

