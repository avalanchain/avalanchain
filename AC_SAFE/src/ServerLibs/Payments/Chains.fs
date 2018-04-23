namespace Avalanchain.Core

open System.Collections.Generic
module Chains =

    open System
    open System.Text.RegularExpressions

    open Akka.Actor
    open Akka.Configuration
    open Akka.IO
    open Akka.Persistence
    open Akka.Persistence.Serialization
    // open Akka.Persistence.Journal
    open Akka.Streams
    open Akka.Streams.Dsl
    open Reactive.Streams

    open Akkling
    open Akkling.Persistence
    // open Akkling.Cluster
    // open Akkling.Cluster.Sharding
    open Akkling.Streams
    open Akkling.Persistence

    open Akka.Persistence.Query
    open Akka.Persistence.Query.Sql   

    open Crypto
    open ChainDefs 

    type PersistEvent<'T> = {   
        Val: 'T 
        Pos: Pos
    }

    type PersistCommand<'T> =
        | Offer of 'T
        | PrintState
        | TakeSnapshot
        | GetPos

    type GetPosResult = GetPosResult of int64

    type PersistState<'T> = {
        LastPos: int64
        Latest: PersistEvent<'T> option
    }



    type PersistentActor<'T>(pid, snapshotInterval: int64) as actor =
        inherit UntypedPersistentActor()
        let mutable state = { LastPos = -1L; Latest = None }
        let updateState evt = 
            state <- { state with Latest = Some evt }
        let updateStatePos (pos: Pos) = 
            state <- { state with LastPos = pos |> int64 }        
                    
        override __.PersistenceId with get() = pid
        override __.OnRecover evt =
            match evt with 
            | :? SnapshotOffer as snapshot when (snapshot.Snapshot :? PersistState<'T>) -> 
                state <- (snapshot.Snapshot :?> PersistState<'T>) 
            | :? PersistEvent<'T> as event -> 
                updateState event
                updateStatePos (event.Pos)
            | :? RecoveryCompleted -> __.Log.Info ("Recovery completed for: {0}", actor.Self.Path)
            | a -> __.Log.Error ("Unhandled event: {0}", a)
        override __.OnCommand cmd =
            match cmd with
            | :? PersistCommand<'T> as c ->
                match c with 
                | Offer v -> 
                    let newPos = state.LastPos + 1L |> uint64
                    let evt = { Val = v; Pos = newPos }
                    updateStatePos newPos
                    actor.PersistAsync(evt, fun event -> updateState event)
                    if state.LastPos % snapshotInterval = 0L then actor.SaveSnapshot(state)
                | PrintState -> __.Log.Info ("Actor state: " + (state.ToString()) + " PosN: " + (actor.LastSequenceNr.ToString()))
                | TakeSnapshot -> actor.SaveSnapshot(state)
                | GetPos -> __.Sender.Tell (GetPosResult state.LastPos)
            | a -> __.Log.Info ("Unhandled command: {0}", a)    

    let persistActorProps<'T> pid (snapshotInterval: int64) : Props<PersistCommand<'T>> =
        Props.Create<PersistentActor<'T>>(pid, snapshotInterval)
        |> Props.From

    let persistActor<'T> system pid (snapshotInterval: int64) : IActorRef<PersistCommand<'T>> =
        persistActorProps<'T> pid snapshotInterval
        |> spawn system pid 

    let persistSink2 pid snapshotInterval = Sink.ofProps(persistActorProps pid snapshotInterval)

    let spawnIfNotExists (system: IActorRefFactory) name spawner =
        let selection = select system ((if system :? ActorSystem then "/user/" else "") + name)
        async {
            let! reply = selection <? Identify("correlation-id")
            return match reply with
                    | ActorIdentity("correlation-id", Some(ref)) -> ref // found
                    | _ -> spawner system  // not found
        } |> Async.RunSynchronously

    type Pid = string
    type SinkActorMessage = | GetSinkActorRef of Pid
    let rec sinkHolder<'T> (snapshotInterval: int64) (context: Actor<SinkActorMessage>) = 
        function
            | GetSinkActorRef pid -> 
                let act = spawnIfNotExists context pid (fun ctx -> persistActor<'T> ctx pid snapshotInterval)
                context.Sender() <! act
                ignored ()

    let spawnSinkHolder<'T> system (snapshotInterval: int64) = 
        let name = "_" + Regex("[\[\]`]").Replace(typedefof<'T>.Name, "_")
        spawnIfNotExists system name (fun ctx -> spawn ctx name <| props (actorOf2 (sinkHolder<'T> snapshotInterval)))
            
    let getSinkActor<'T> system (snapshotInterval: int64) pid =
        let sh = spawnSinkHolder<'T> system snapshotInterval
        async { let! (ar: IActorRef<PersistCommand<'T>>) = sh <? GetSinkActorRef pid
                return ar } |> Async.RunSynchronously

    let persistSink<'T> (system: ActorSystem) pid snapshotInterval = 
        let ar = getSinkActor<'T> system (snapshotInterval: int64) pid
        ar |> Sink.toActorRef (Offer Unchecked.defaultof<'T>)

    let readJournal system = PersistenceQuery.Get(system).ReadJournalFor<SqlReadJournal>(SqlReadJournal.Identifier);

    let currentEventsSource<'T> keyVault system verify pid from count = 
        (readJournal system).CurrentEventsByPersistenceId(pid, from, from + count) 
        |> Source.map(fun e -> (e.Event :?> string) |> fromJwt<'T> keyVault verify) 

    let allEventsSource<'T> keyVault system pid verify from count = 
        (readJournal system).EventsByPersistenceId(pid, from, from + count) 
        |> Source.map(fun e -> (e.Event :?> string) |> fromJwt<'T> keyVault verify) 

    let persistFlow<'T> snapshotInterval keyVault system verify pid = 
        let sink = persistSink<'T> system pid snapshotInterval
        Flow.ofSinkAndSourceMat sink Keep.none 
            (allEventsSource keyVault system pid verify 0L Int64.MaxValue) 
    
    let streamCurrentPos system (snapshotInterval: int64) pid = 
        let actor = getSinkActor<string> system (snapshotInterval: int64) pid
        async { let! (pos: GetPosResult) = actor <? GetPos
                return match pos with GetPosResult p -> p }