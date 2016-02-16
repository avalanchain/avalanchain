module Avalanchain.Quorum

open System

open FSharp.Interop.Dynamic
open FSharp.Core.Fluent
open Chessie.ErrorHandling

open SecKeys
open SecPrimitives
open RefsAndPathes

type ExecutionGroup = ExecutionGroup of string 
    with
        static member Default = ExecutionGroup "_default_" 
        member this.Value = 
            let (ExecutionGroup ret) = this
            ret
        
and ExecutionPolicy = // TODO: ExecutionPolicy?
    | Pass
    | One of NodeSelectionStrategy * NodeSelectionStake 
    | All of Set<ExecutionPolicy>
and NodeSelectionStrategy = 
    | Random 
    | Mandatory of Set<ExecutionGroup>
and NodeSelectionStake = 
    | Percentage of Percentage: float * Total: uint32 // TODO: Replace logic with total derived from aggregating join/leave messages
    | FixedCount of uint32 // TODO: Add limited types (or checks)


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

