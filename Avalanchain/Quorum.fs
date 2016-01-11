module Avalanchain.Quorum

open System

open FSharp.Interop.Dynamic
open FSharp.Core.Fluent
open Chessie.ErrorHandling

open SecKeys
open SecPrimitives
open RefsAndPathes

type ExecutionGroup = ExecutionGroup of string 
and ExecutionPolicy = // TODO: ExecutionPolicy?
    | None
    | One of NodeSelectionStrategy * NodeSelectionStake 
    | All of ExecutionPolicy list
and NodeSelectionStrategy = 
    | Random 
    | Mandatory of ExecutionGroup list
and NodeSelectionStake = 
    | Percentage of float 
    | FixedCount of uint16 // TODO: Add limited types (or checks)


type ExecutionRequest =
    | NodeExecutionRequest
    | GroupExecutionRequest
and NodeExecutionRequest = {
    StreamRef: EventStreamRef
    Node: NodePath
}
and GroupExecutionRequest = {
    StreamRef: EventStreamRef
    Group: ExecutionGroup
    Policy: ExecutionPolicy
}

type Message =
    | System
    | Admin
    | Event
    | Command
    | Query

