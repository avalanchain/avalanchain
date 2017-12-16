namespace Avalanchain

open System.Collections.Generic
module Chains =

    open System
    open Akka.Actor
    open Akka.Configuration
    open Akka.IO
    open Akka.Persistence
    open Akka.Persistence.Serialization
    // open Akka.Persistence.Journal
    open Akka.Streams
    open Akka.Streams.Dsl
    open Reactive.Streams

    open Hyperion

    open Akkling
    open Akkling.Persistence
    open Akkling.Cluster
    open Akkling.Cluster.Sharding
    open Akkling.Streams

    open Akkling.Persistence

    open Akka.Persistence.Query
    open Akka.Persistence.Query.Sql   

    open ChainDefs 

    type PersistEvent<'T> =
        { Val : 'T }
        // { Val : JwtToken<'T> }

    type PersistCommand<'T> =
        | Offer of 'T
        | Batch of 'T seq
        | PrintState
        | TakeSnapshot
        | GetJournal

    type PersistMessage<'T> =
        | Command of PersistCommand<'T>
        | Event of PersistEvent<'T>

    type Pos = uint64
    type PersistState<'T> = {
        LastPos: Pos
        Latest: 'T option
    }


    

    let persistActor (snapshotInterval: int64): Props<PersistMessage<'T>> =
        propsPersist(fun mailbox ->
            // let persistentContext = (mailbox :?> ExtEventsourced<_>).Incarnation() :?> FunPersistentActor<PersistMessage<string>>
            // persistentContext.Recovery <- fun recoveredState -> 
            let snapshot state = 
                mailbox.SnapshotStore.Tell (SaveSnapshot (SnapshotMetadata(mailbox.Pid, mailbox.LastSequenceNr()), state), (untyped mailbox.Self))
            let rec loop state = 
                actor {
                    // persistentContext.SaveSnapshot state
                    let! msg = mailbox.Receive() //|> retype
                    match msg with
                    | SnapshotOffer st -> 
                        mailbox.Log.Value.Info ("State recovered for PId '{0}': '{1}'", (mailbox.Pid), st)
                        return! loop st
                    | Event e -> 
                        if (mailbox.LastSequenceNr() % snapshotInterval) = 0L && not (mailbox.IsRecovering()) 
                        then snapshot state
                        return! loop ({ state with  Latest = Some e.Val; LastPos = state.LastPos + 1UL })
                    | Command cmd ->
                        match cmd with
                        | PrintState ->
                            //mailbox.Sender() <! retype (state, mailbox.LastSequenceNr)
                            printfn "State for PId '%s', pos: '%A': '%A'" mailbox.Pid (mailbox.LastSequenceNr()) state
                            return! loop state
                        | Offer v -> return! PersistAsync (Event { Val = v })
                        | Batch va -> return! (va |> Seq.map (fun v -> Event { Val = v }) |> PersistAllAsync)
                        | TakeSnapshot -> 
                            snapshot state
                            return! loop state
                        | GetJournal -> mailbox.Sender() <! mailbox.Journal
                    // | m -> 
                    //     printfn "Unhandled msg: '%A'" m
                    //     // return! loop state
                    //     return Unhandled
                }
            loop { Latest = None; LastPos = 0UL } ) 

    let persistSink snapshotInterval = Sink.ofProps(persistActor snapshotInterval)
        

    let readJournal system = PersistenceQuery.Get(system).ReadJournalFor<SqlReadJournal>(SqlReadJournal.Identifier);

    