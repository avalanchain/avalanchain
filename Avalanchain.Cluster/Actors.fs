module Avalanchain.Cluster.Actors

open Akka.Persistence
open Akka.Actor
open System
open Chessie.ErrorHandling
open Avalanchain.Projection

type EventSourcingLogic<'TAdminCommand, 'TBusinessCommand, 'TS, 'TE, 'TMsg> = {
    InitialState: unit -> 'TE * 'TS
    UpdateState: 'TS -> 'TE -> 'TS
    ProcessBusinessCommand: 'TS -> 'TBusinessCommand -> Result<'TS, 'TMsg> // TODO: Add Chessie error reporting
    //UnwrapState: 'TState -> 
    ProcessAdminCommand: 'TAdminCommand -> Result<'TS, 'TMsg> // TODO: Add Chessie error reporting
}

type ResActor<'TAdminCommand, 'TBusinessCommand, 'TState, 'TEvent, 'TMsg> (eventSourcingLogic) as self = 
    inherit PersistentActor()
    let mutable (lastEvent, state) = eventSourcingLogic.InitialState()
    do (UntypedActor.Context.SetReceiveTimeout(Nullable(TimeSpan.FromMinutes(2.0))))
    member private __.Self = base.Self
    member private __.Context = UntypedActor.Context
    override __.PersistenceId with get() = (sprintf "Actor %s-%s" (self.Context.Parent.Path.Name) self.Self.Path.Name)
    override __.ReceiveRecover(msg: obj) = 
        match msg with 
        | :? 'TEvent as e -> 
            state <- eventSourcingLogic.UpdateState state e 
            true
        | :? SnapshotOffer as so -> 
            match so.Snapshot with
            | :? 'TState as sos -> 
                state <- sos
                true
            | _ -> false
        | _ -> false
    override this.ReceiveCommand(msg: obj) = 
        match msg with 
        | :? 'TBusinessCommand as c -> 
            match eventSourcingLogic.ProcessBusinessCommand state c with 
            | Ok (e, msgs) -> 
                lastEvent <- e
                this.Persist(e, (fun ee -> 
                                    lastEvent <- ee 
                                    state <- eventSourcingLogic.UpdateState state ee))
                if List.isEmpty msgs then this.Sender.Tell(msgs, this.Self)
                true
            | Bad msgs -> 
                this.Sender.Tell(msgs, this.Self)
                false
        | :? 'TAdminCommand as c -> 
            match eventSourcingLogic.ProcessAdminCommand c with 
            | Ok (e, msgs) -> 
                this.Persist(e, (fun ee -> (state <- eventSourcingLogic.UpdateState state ee) |> ignore)) // TODO: Rethink Admin channel logic
                if List.isEmpty msgs then this.Sender.Tell(msgs, this.Self)
                true
            | Bad msgs -> 
                this.Sender.Tell(msgs, this.Self)
                false
        | _ -> false

let simpleEventSourcingLogic = {
    InitialState = (fun () -> "", [])
    UpdateState = (fun state e -> e::state)
    ProcessBusinessCommand = (fun e cmd -> ok (sprintf "Received '%s'" (cmd.ToString())))
    ProcessAdminCommand = (fun ac -> fail("No Admin channel defined"))
}

module KeyValue =
    type AdminCommand = AdminCommand

    type BusinessCommand<'T> = NewValue of 'T 

    let kvLogic<'T> = {
        InitialState = (fun () -> Unchecked.defaultof<'T>, Unchecked.defaultof<'T>)
        UpdateState = (fun state e -> e)
        ProcessBusinessCommand = (fun e (NewValue v) -> ok (v))
        ProcessAdminCommand = (fun ac -> fail("No Admin channel defined"))
    }

    type KeyValueActor<'T when 'T: equality>() =
        inherit ResActor<AdminCommand, BusinessCommand<'T>, 'T, 'T>(kvLogic<'T>)


module Stream =
    type AdminCommand = AdminCommand

    type BusinessCommand<'T> = NewValue of 'T 

    let streamLogic projection initialState = {
        InitialState = (fun () -> initialState)
        UpdateState = (fun state e -> e)
        ProcessBusinessCommand = (fun e (NewValue v) -> projection e v)
        ProcessAdminCommand = (fun ac -> fail("No Admin channel defined"))
    }

    type StreamActor<'T when 'T: equality>(projection, initialState) =
        inherit ResActor<AdminCommand, BusinessCommand<'T>, 'T, 'T>(streamLogic projection initialState)


    let streamLogic2 projection initialState = {
        InitialState = (fun () -> initialState)
        UpdateState = (fun state e -> e)
        ProcessBusinessCommand = (fun e (NewValue v) -> projection e v)
        ProcessAdminCommand = (fun ac -> fail("No Admin channel defined"))
    }

    type StreamActor<'TState, 'TData when 'TData: equality and 'TState: equality>(projection, initialState) =
        inherit ResActor<AdminCommand, BusinessCommand<'TData>, 'TState, 'TData>(streamLogic2 projection initialState)