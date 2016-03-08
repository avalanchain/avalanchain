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

type ResActor<'TCommand, 'TEvent, 'TState, 'TFrame, 'TMsg> (eventSourcingLogic: EventSourcingLogic<'TCommand, 'TEvent, 'TState, 'TFrame, 'TMsg>) as this = 
    inherit PersistentActor()
    let mutable frame: 'TFrame option = None
    let getState() = match frame with
                        | Some f -> eventSourcingLogic.Unbundle f |> snd
                        | None -> eventSourcingLogic.InitialState
    do (Console.WriteLine("NAME - " + base.Self.Path.Address.ToString())
        UntypedActor.Context.SetReceiveTimeout(Nullable(TimeSpan.FromMinutes(2.0))))
    member private this.Self = base.Self
    member private this.Context = UntypedActor.Context
    override __.PersistenceId with get() = (sprintf "Actor %s-%s" (this.Context.Parent.Path.Name) this.Self.Path.Name)
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
        this.Log.Debug("Received '{0}'", msg)
        Console.WriteLine("Received '{0}'", msg)
        match msg with 
        | :? 'TCommand as c ->   // TODO: Think about adding Admin channel logic
            let state = getState()
            let event = eventSourcingLogic.Process state c
            match event >>= eventSourcingLogic.Apply state with 
            | Ok ((e, newState), msgs) -> 
                this.Persist(e, (fun ee -> frame <- Some(eventSourcingLogic.Bundle frame (ee, newState))))
                if List.isEmpty msgs then this.Sender.Tell(msgs, this.Self)
                this.Log.Info("Command '{0}' processed, Event '{1}' saved, new State '{2}', warnings '{3}", c, e, newState, msgs)
                Console.WriteLine("Command '{0}' processed, Event '{1}' saved, new State '{2}', warnings '{3}", c, e, newState, msgs)
                true
            | Bad msgs -> 
                this.Sender.Tell(msgs, this.Self)
                this.Log.Error("Event '{0}' processing failed with saved, new State '{1}'", c, msgs)
                Console.WriteLine("Event '{0}' processing failed with saved, new State '{1}'", c, msgs)
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
    open System.Collections.Concurrent
    open Akka.FSharp

    //type AdminCommand = AdminCommand

    type Command<'T> = NewValue of 'T 

    let kvLogic<'T, 'TMsg> = {
        InitialState = Unchecked.defaultof<'T>
        Process = (fun _ (NewValue v) -> ok (v))
        Apply = (fun _ e -> ok<'T * 'T, 'TMsg> (e, e))
        Bundle = (fun _ (e, _) -> e)
        Unbundle = (fun e -> (e, e))
    }

    type KVMsg<'TK, 'TV> = 
        | Add of 'TK * 'TV
        | Get of 'TK

    type KeyValueActor<'T, 'TMsg>(key: string) =
        inherit ResActor<Command<'T>, 'T, 'T, 'T, 'TMsg>(kvLogic<'T, 'TMsg>)
        member __.Key = key

    let bucketDict (dict:ConcurrentDictionary<'TK, 'TV>) (mailbox: Actor<KVMsg<'TK, 'TV>>) = function
        | Add (k, v)  -> dict.[k] = v |> ignore
        | Get k  -> mailbox.Sender() <! (dict.TryGetValue k |> (function | (true, v) -> Some(v) | (false, _) -> None))

    let bucket mailbox = bucketDict (new ConcurrentDictionary<'TK, 'TV>()) mailbox

    //type KeyValueBucketActor<'T, 'TMsg> = 

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

    type StreamActor2<'TState, 'TEvent, 'TMsg>(projection, key: string) =
        inherit ResActor<Command<'TEvent>, 'TEvent, 'TState, 'TEvent * 'TState, 'TMsg>(streamLogic<'TState, 'TEvent, 'TMsg> projection)
        member __.Key = key

    type StreamActor<'T, 'TMsg>(projection, key: string) = 
        inherit StreamActor2<'T, 'T, 'TMsg>(projection, key)


//module Stream2 =
//    open Avalanchain.StreamEvent
//    open Avalanchain.EventStream
//    open Avalanchain
//    open Avalanchain.EventProcessor
//
//    let internal checkIntegrity (dataHasher: DataHasher<Event<'TData>>)  (event: HashedEvent<'TData>) = 
//        let rehashed = dataHasher event.Value
//        if (rehashed.Hash = event.Hash) 
//        then ok event
//        else fail IntegrityFailure 
//    
//    let internal checkPermissions (permissionsChecker: HashedEvent<'TData> -> DataResult<unit>) event = 
//        match permissionsChecker event with 
//        | Ok (_) -> ok event
//        | Bad msgs -> fail (PermissionsFailure msgs)
//
//    let internal project streamDef (stateHasher: DataHasher<StreamState<'TState>>) (state: HashedState<'TState>) (event: HashedEvent<'TData>) =
//        let projection = streamDef.Value.Projection.F 
//        try
//            let res = projection state.Value.Value event.Value.Data
//            match res with
//            | Ok (newState, msgs) -> 
//                let ns = { 
//                    Value = newState 
//                    StreamRef = streamDef.Value.Ref
//                    Nonce = state.Value.Nonce + 1UL 
//                }
//                Ok (stateHasher(ns), msgs |> List.map ExecutionWarning)
//            | Bad msgs -> fail (ProcessingFailure msgs)
//        with
//            | ex -> fail (ProcessingFailure([sprintf "Error projection execution : '%s'" (ex.ToString())]))
//
//    let internal buildNewFrame 
//        streamDef
//        hasher
//        (proofer: Proofer<'TState, 'TData>)
//        (streamFrame: EventStreamFrame<'TState, 'TData> option)
//        (hashedEvent: HashedEvent<'TData>)
//        (hashedState: HashedState<'TState>) =
//        let nonce = hashedState.Value.Nonce
//        let merkledEvent = addToMerkled hasher (streamFrame |> Option.bind (fun sf -> Some sf.Event.Merkle)) hashedEvent
//        let merkledState = addToMerkled hasher (streamFrame |> Option.bind (fun sf -> Some sf.State.Merkle)) hashedState
//        let newStreamFrame = {
//                                Def = streamDef
//                                TimeStamp = DateTimeOffset.UtcNow
//                                Event = merkledEvent
//                                State = merkledState 
//                                Nonce = nonce
//                                Proofs = [proofer streamDef.Value.Ref nonce merkledState.HashedValue hashedEvent] |> Set.ofList
//                                // StreamStatus = streamFrame.StreamStatus
//                            }
//        newStreamFrame
//
//    type Command<'T> = NewValue of 'T 
//
//    type StreamLogicContext<'TState, 'TData when 'TData: equality and 'TState: equality> = {
//        Hasher: Hasher
//        Proofer: Proofer<'TState, 'TData>
//        DataHasher: DataHasher<Event<'TData>>
//        StateHasher: DataHasher<StreamState<'TState>>
//        PermissionsChecker: HashedEvent<'TData> -> DataResult<unit>
//    }
//
//    let streamLogic<'TState, 'TData, 'TCommand, 'TMsg when 'TData: equality and 'TState: equality> 
//        (slc: StreamLogicContext<'TState, 'TData>) 
//        streamDef 
//        (dataGetter: 'TCommand -> HashedEvent<'TData>) = {
//        InitialState = 
//            slc.StateHasher({ 
//                                Value = Unchecked.defaultof<'TState> 
//                                StreamRef = streamDef.Value.Ref
//                                Nonce = 0UL 
//                            })
//        Process = (fun _ c -> dataGetter(c) |> checkIntegrity slc.DataHasher >>= checkPermissions slc.PermissionsChecker)
//        Apply = (fun s e -> project streamDef slc.StateHasher s e >>= (fun ss -> ok (e, ss)))
//        Bundle = (fun frame (e, s) -> buildNewFrame streamDef slc.Hasher slc.Proofer frame e s)
//        Unbundle = (fun frame -> (frame.Event.HashedValue, frame.State.HashedValue))
//    }
//
//    let mt = (fun c -> match c with (NewValue v) -> v)
//
//    type StreamActor<'TState, 'TData when 'TData: equality and 'TState: equality>(streamLogicContext: StreamLogicContext<'TState, 'TData>, streamDef) =
//        inherit ResActor<Command<HashedEvent<'TData>>, HashedEvent<'TData>, HashedState<'TState>, EventStreamFrame<'TState, 'TData>, EventProcessingMsg>
//            (streamLogic<'TState, 'TData, Command<HashedEvent<'TData>>, EventProcessingMsg> streamLogicContext streamDef mt)