module Avalanchain.Cluster.Actors

open Akka.Persistence
open Akka.Actor
open System
open Chessie.ErrorHandling
open Avalanchain.Projection

type EventSourcingLogic<'TCommand, 'TEvent, 'TState, 'TFrame, 'TMsg> = {
    InitialState: 'TState
    Process: 'TState -> 'TCommand -> Result<'TEvent, 'TMsg> 
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

    //type AdminCommand = AdminCommand

    type Command<'T> = NewValue of 'T 

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


module Stream2 =
    open Avalanchain.StreamEvent
    open Avalanchain.EventStream
    open Avalanchain
    open Avalanchain.EventProcessor

    let internal checkIntegrity (dataHasher: DataHasher<Event<'TData>>)  (event: HashedEvent<'TData>) = 
        let rehashed = dataHasher event.Value
        if (rehashed.Hash = event.Hash) 
        then ok event
        else fail IntegrityFailure 
    
    let internal checkPermissions (permissionsChecker: HashedEvent<'TData> -> DataResult<unit>) event = 
        match permissionsChecker event with 
        | Ok (_) -> ok event
        | Bad msgs -> fail (PermissionsFailure msgs)

    let internal project streamDef (stateHasher: DataHasher<StreamState<'TState>>) (state: HashedState<'TState>) (event: HashedEvent<'TData>) =
        let projection = streamDef.Value.Projection.F 
        try
            let res = projection state.Value.Value event.Value.Data
            match res with
            | Ok (newState, msgs) -> 
                let ns = { 
                    Value = newState 
                    StreamRef = streamDef.Value.Ref
                    Nonce = state.Value.Nonce + 1UL 
                }
                Ok (stateHasher(ns), msgs |> List.map ExecutionWarning)
            | Bad msgs -> fail (ProcessingFailure msgs)
        with
            | ex -> fail (ProcessingFailure([sprintf "Error projection execution : '%s'" (ex.ToString())]))

    let internal buildNewFrame 
        streamDef
        hasher
        (proofer: Proofer<'TState, 'TData>)
        (streamFrame: EventStreamFrame<'TState, 'TData> option)
        (hashedEvent: HashedEvent<'TData>)
        (hashedState: HashedState<'TState>) =
        let nonce = hashedState.Value.Nonce
        let merkledEvent = addToMerkled hasher (streamFrame |> Option.bind (fun sf -> Some sf.Event.Merkle)) hashedEvent
        let merkledState = addToMerkled hasher (streamFrame |> Option.bind (fun sf -> Some sf.State.Merkle)) hashedState
        let newStreamFrame = {
                                Def = streamDef
                                TimeStamp = DateTimeOffset.UtcNow
                                Event = merkledEvent
                                State = merkledState 
                                Nonce = nonce
                                Proofs = [proofer streamDef.Value.Ref nonce merkledState.HashedValue hashedEvent] |> Set.ofList
                                // StreamStatus = streamFrame.StreamStatus
                            }
        newStreamFrame

    type Command<'T> = NewValue of 'T 

    type StreamLogicContext<'TState, 'TData when 'TData: equality and 'TState: equality> = {
        Hasher: Hasher
        Proofer: Proofer<'TState, 'TData>
        DataHasher: DataHasher<Event<'TData>>
        StateHasher: DataHasher<StreamState<'TState>>
        PermissionsChecker: HashedEvent<'TData> -> DataResult<unit>
    }

    let streamLogic<'TState, 'TData, 'TMsg when 'TData: equality and 'TState: equality> (slc: StreamLogicContext<'TState, 'TData>) streamDef = {
        InitialState = 
            slc.StateHasher({ 
                                Value = Unchecked.defaultof<'TState> 
                                StreamRef = streamDef.Value.Ref
                                Nonce = 0UL 
                            })
        Process = (fun _ (NewValue v) -> v |> checkIntegrity slc.DataHasher >>= checkPermissions slc.PermissionsChecker)
        Apply = (fun s e -> project streamDef slc.StateHasher s e >>= (fun ss -> ok (e, ss)))
        Bundle = (fun frame (e, s) -> buildNewFrame streamDef slc.Hasher slc.Proofer frame e s)
        Unbundle = (fun frame -> (frame.Event.HashedValue, frame.State.HashedValue))
    }

    type StreamActor<'TState, 'TData when 'TData: equality and 'TState: equality>(streamLogicContext: StreamLogicContext<'TState, 'TData>, streamDef) =
        inherit ResActor<Command<HashedEvent<'TData>>, HashedEvent<'TData>, HashedState<'TState>, EventStreamFrame<'TState, 'TData>, EventProcessingMsg>
            (streamLogic<'TState, 'TData, EventProcessingMsg> streamLogicContext streamDef)