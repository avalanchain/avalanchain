module Avalanchain.FrameSynchronizer

open System

open FSharp.Interop.Dynamic
open FSharp.Core.Fluent
open Chessie.ErrorHandling

open SecKeys
open SecPrimitives
open RefsAndPathes
open Quorum
open EventStream

type FrameSynchronizer<'TState, 'TData when 'TData: equality and 'TState: equality> = EventStreamFrame<'TState, 'TData> -> EventStreamFrame<'TState, 'TData>

type FrameSynchronizationContext<'TState, 'TData when 'TData: equality and 'TState: equality> = {
    ExecutionGroup: ExecutionGroup
    ExecutionPolicy: ExecutionPolicy
    FrameSynchronizer: FrameSynchronizer<'TState, 'TData>
}
    
let frameSynchronizationContextBuilder executionGroup executionPolicy =
    let frameSynchronizer =
        match executionPolicy with // TODO: Implement complex\ policies
        | Pass -> (fun x -> x)
        | One (strategy, stake) -> (fun x -> x)
        | All _ -> (fun x -> x)

    {
        ExecutionGroup = executionGroup
        ExecutionPolicy = executionPolicy
        FrameSynchronizer = frameSynchronizer
    }