module Avalanchain.Cluster.Actors

open Akka.Persistence
open Akka.Actor
open System
open Chessie.ErrorHandling
open Avalanchain.Projection

type EventSourcingLogic<'TCommand, 'TEvent, 'TState, 'TFrame, 'TMsg> = {
    InitialState: 'TState
    Process: 'TState option -> 'TCommand -> Result<'TEvent, 'TMsg> 
    Apply: 'TState -> 'TEvent -> Result<'TEvent * 'TState, 'TMsg>
    Bundle: 'TFrame option -> 'TEvent * 'TState -> 'TFrame
    Unbundle: 'TFrame -> 'TEvent * 'TState
}

type ResActor<'TCommand, 'TEvent, 'TState, 'TFrame, 'TMsg> (eventSourcingLogic: EventSourcingLogic<'TCommand, 'TEvent, 'TState, 'TFrame, 'TMsg>) as self = 
    inherit PersistentActor()
    let mutable frame: 'TFrame option = None
    let getState() = match frame with
                        | Some f -> eventSourcingLogic.Unbundle f |> snd
                        | None -> eventSourcingLogic.InitialState
    do (UntypedActor.Context.SetReceiveTimeout(Nullable(TimeSpan.FromMinutes(2.0))))
    member private __.Self = base.Self
    member private __.Context = UntypedActor.Context
    override __.PersistenceId with get() = (sprintf "Actor %s-%s" (self.Context.Parent.Path.Name) self.Self.Path.Name)
    override __.ReceiveRecover(msg: obj) = 
        match msg with 
        | :? 'TEvent as e -> 
            let applyResult = eventSourcingLogic.Apply (getState()) e 
            match applyResult with
            | Ok ((e, newState), msgs) -> 
                frame <- Some(eventSourcingLogic.Bundle frame (e, newState))
                true
            | Bad msgs -> false
        | :? SnapshotOffer as so -> 
            match so.Snapshot with
            | :? 'TFrame as fr -> 
                frame <- Some(fr)
                true
            | _ -> false
        | _ -> false
    override this.ReceiveCommand(msg: obj) = 
        match msg with 
        | :? 'TCommand as c ->   // TODO: Think about adding Admin channel logic
            let state = getState()
            let event = eventSourcingLogic.Process state c
            match event >>= eventSourcingLogic.Apply state with 
            | Ok ((e, newState), msgs) -> 
                this.Persist(e, (fun ee -> frame <- Some(eventSourcingLogic.Bundle frame (ee, newState))))
                if List.isEmpty msgs then this.Sender.Tell(msgs, this.Self)
                true
            | Bad msgs -> 
                this.Sender.Tell(msgs, this.Self)
                false
        | _ -> false

let simpleEventSourcingLogic = {
    InitialState = []
    Process = (fun s cmd -> ok (sprintf "Received '%s'" (cmd.ToString())))
    Apply = (fun s e -> ok (e, e::s))
    Bundle = (fun frame (e, s) -> (e, s))
    Unbundle = (fun (e, s) -> (e, s))
}

module KeyValue =
    //type AdminCommand = AdminCommand

    type Command<'T> = NewValue of 'T 

    let kvLogic<'T, 'TMsg> = {
        InitialState = Unchecked.defaultof<'T>
        Process = (fun _ (NewValue v) -> ok (v))
        Apply = (fun _ e -> ok<'T * 'T, 'TMsg> (e, e))
        Bundle = (fun _ (e, _) -> e)
        Unbundle = (fun e -> (e, e))
    }

    type KeyValueActor<'T, 'TMsg>() =
        inherit ResActor<Command<'T>, 'T, 'T, 'T, 'TMsg>(kvLogic<'T, 'TMsg>)

module Stream =
    open Avalanchain.StreamEvent
    open Avalanchain.EventStream

    //type AdminCommand = AdminCommand

    type Command<'T> = NewValue of 'T 

//    let streamLogic<'T, 'TMsg> (projection: 'T -> 'T -> Result<'T, 'TMsg>)  = {
//        InitialState = Unchecked.defaultof<'T>
//        Process = (fun _ (NewValue v) -> ok (v))
//        Apply = (fun s e -> projection s e >>= (fun ss -> ok (e, ss)))
//        Bundle = (fun f (e, s) -> (e, s))
//        Unbundle = (fun (e, s) -> (e, s))
//    }
//
//    type StreamActor<'T, 'TMsg>(projection) =
//        inherit ResActor<Command<'T>, 'T, 'T, 'T * 'T, 'TMsg>(streamLogic<'T, 'TMsg> projection)


    let streamLogic<'TState, 'TEvent, 'TMsg> (projection: 'TState -> 'TEvent -> Result<'TState, 'TMsg>)  = {
        InitialState = Unchecked.defaultof<'TState>
        Process = (fun _ (NewValue v) -> ok (v))
        Apply = (fun s e -> projection s e >>= (fun ss -> ok (e, ss)))
        Bundle = (fun f (e, s) -> (e, s))
        Unbundle = (fun (e, s) -> (e, s))
    }

    type StreamActor<'TState, 'TEvent, 'TMsg>(projection) =
        inherit ResActor<Command<'TEvent>, 'TEvent, 'TState, 'TEvent * 'TState, 'TMsg>(streamLogic<'TState, 'TEvent, 'TMsg> projection)

    type StreamActor<'T, 'TMsg>(projection) = 
        inherit StreamActor<'T, 'T, 'TMsg>(projection)

    let eventStreamLogic<'TState, 'TData, 'TMsg when 'TData: equality and 'TState: equality> (projection: 'TState -> 'TData -> Result<'TState, 'TMsg>)  = {
        InitialState = Unchecked.defaultof<'TState>
        Process = (fun _ (NewValue v) -> ok (v))
        Apply = (fun s e -> projection s e >>= (fun ss -> ok (e, ss)))
        Bundle = (fun f (e, s) -> (e, s))
        Unbundle = (fun (e, s) -> (e, s))
    }

//    type EventStreamActor<'TState, 'TData, 'TMsg when 'TData: equality and 'TState: equality>(projection) =
//        inherit ResActor<Command<HashedEvent<'TData>>, HashedEvent<'TData>, HashedState<'TState>, EventStreamFrame<'TState, 'TData>, 'TMsg>(eventStreamLogic<'TState, 'TData, 'TMsg> projection)