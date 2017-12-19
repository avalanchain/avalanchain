#load "../.paket/load/net461/main.group.fsx"

open System
open System.IO
#if INTERACTIVE
let cd = Path.Combine(__SOURCE_DIRECTORY__, "bin/Debug/net461")
System.IO.Directory.SetCurrentDirectory(cd)
#I "bin/Debug/net461"
#endif


open Akka.Actor
open Akkling
open Microsoft.FSharpLu

let setupNode() = 
    let systemName = "ac"
    let config = 
        sprintf """
            akka {
                persistence {
                    #journal.plugin = "akka.persistence.journal.inmem"
          
                    #snapshot-store.plugin = "akka.persistence.snapshot-store.local"
                	journal {
                		plugin = "akka.persistence.journal.sqlite"
                		sqlite {
                		
                			# qualified type name of the SQLite persistence journal actor
                			class = "Akka.Persistence.Sqlite.Journal.SqliteJournal, Akka.Persistence.Sqlite"

                			# dispatcher used to drive journal actor
                			plugin-dispatcher = "akka.actor.default-dispatcher"

                			# connection string used for database access
                            # "Filename=file:memdb-journal-" + counter.IncrementAndGet() + ".db;Mode=Memory;Cache=Shared"
                			connection-string = "Filename=./db/streams.db"
                			
                			# connection string name for .config file used when no connection string has been provided
                			connection-string-name = ""

                			# default SQLite commands timeout
                			connection-timeout = 30s

                			# SQLite table corresponding with persistent journal
                			table-name = event_journal
                			
                			# metadata table
                			metadata-table-name = journal_metadata

                			# should corresponding journal table be initialized automatically
                			auto-initialize = on

                			# timestamp provider used for generation of journal entries timestamps
                			timestamp-provider = "Akka.Persistence.Sql.Common.Journal.DefaultTimestampProvider, Akka.Persistence.Sql.Common"
                			
                			circuit-breaker {
                				max-failures = 5
                				call-timeout = 20s
                				reset-timeout = 60s
                			}

                            #Query section
                            #event-adapters {{
                            #  color-tagger  = ""Akka.Persistence.TCK.Query.ColorFruitTagger, Akka.Persistence.TCK""
                            #}}
                            #event-adapter-bindings = {{
                            #  ""System.String"" = color-tagger
                            #}}
                            #refresh-interval = 100ms
                		}
                	}

                	snapshot-store {
                		plugin = "akka.persistence.snapshot-store.sqlite"
                		sqlite {
                		
                			# qualified type name of the SQLite persistence journal actor
                			class = "Akka.Persistence.Sqlite.Snapshot.SqliteSnapshotStore, Akka.Persistence.Sqlite"

                			# dispatcher used to drive journal actor
                			plugin-dispatcher = "akka.actor.default-dispatcher"

                			# connection string used for database access
                			connection-string = "Filename=./db/snapshots.db"

                			# connection string name for .config file used when no connection string has been provided
                			connection-string-name = ""

                			# default SQLite commands timeout
                			connection-timeout = 30s
                			
                			# SQLite table corresponding with persistent journal
                			table-name = snapshot_store

                			# should corresponding journal table be initialized automatically
                			auto-initialize = on

                		}
                	}                                   

                    view.auto-update-interval = 100
                    query.journal.sql {
                      # Implementation class of the SQL ReadJournalProvider
                      class = "Akka.Persistence.Query.Sql.SqlReadJournalProvider, Akka.Persistence.Query.Sql"
                      # Absolute path to the write journal plugin configuration entry that this
                      # query journal will connect to.
                      # If undefined (or "") it will connect to the default journal as specified by the
                      # akka.persistence.journal.plugin property.
                      write-plugin = ""
                      # The SQL write journal is notifying the query side as soon as things
                      # are persisted, but for efficiency reasons the query side retrieves the events
                      # in batches that sometimes can be delayed up to the configured `refresh-interval`.
                      refresh-interval = 1s
                      # How many events to fetch in one query (replay) and keep buffered until they
                      # are delivered downstreams.
                      max-buffer-size = 1000
                    }                    
                }
            }
            """ 
        |> Configuration.parse
        |> Configuration.fallback (Configuration.defaultConfig())
    Directory.CreateDirectory("db") |> ignore // Create folder for sqlite files if not exists 
    config//.WithFallback (DistributedPubSub.DefaultConfig())
    |> System.create systemName 

let system = setupNode()

type PersistEvent<'T> =
    { Val : 'T }

type PersistCommand<'T> =
    | Offer of 'T
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


open Akka.Persistence
open Akka.Persistence.Serialization
open Akkling.Persistence


let persistActor actorId (snapshotInterval: int64): IActorRef<PersistMessage<'T>> =
    spawn system actorId  <| propsPersist(fun mailbox ->
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
                    printfn "State recovered for Id '%s': '%A'" actorId st
                    return! loop st
                | Event e -> 
                    if (mailbox.LastSequenceNr() % snapshotInterval) = 0L && not (mailbox.IsRecovering()) 
                    then snapshot state
                    return! loop ({ state with  Latest = Some e.Val; LastPos = state.LastPos + 1UL })
                | Command cmd ->
                    match cmd with
                    | PrintState ->
                        //mailbox.Sender() <! retype (state, mailbox.LastSequenceNr)
                        printfn "State for Id '%s', pos: '%A': '%A'" actorId (mailbox.LastSequenceNr()) state
                        return! loop state
                    | Offer v -> return PersistAsync (Event { Val = v })
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

let counter: IActorRef<PersistMessage<string>> = (persistActor "a2" 4L)

let msg = "Hi" |> Offer |> Command
counter <! msg 

counter <! (PrintState |> Command)

counter <! (TakeSnapshot |> Command)

// let journal: IActorRef = async {let! reply = counter <? Command GetJournal
//                                 printfn "Current state of %A: %A" counter reply 
//                                 return reply } |> Async.RunSynchronously


open Akka.Streams
open Akka.Streams.Dsl


open Akka.Persistence.Journal
open Akkling
open Akkling.Streams
open Akka.Persistence.Query
open Akka.Persistence.Query.Sql

//open Akka.Persistence.Query.Sql

//let readJournal = PersistenceQuery.Get(system).ReadJournalFor<SqlReadJournal>("akka.persistence.query.my-read-journal");

let mat = ActorMaterializer.Create(system)
let readJournal = PersistenceQuery.Get(system).ReadJournalFor<SqlReadJournal>(SqlReadJournal.Identifier);


let pidsSource = readJournal.PersistenceIds();

// let pids = pidsSource |> Source.runFold mat (fun state e -> e :: state) [] 
//             |> Async.Start

let pids = pidsSource |> Source.runForEach mat (fun e -> printfn "%A" e) |> Async.Start


let events = readJournal.CurrentEventsByPersistenceId("a2", 0L, 10L)
let pids = events |> Source.runForEach mat (fun e -> printfn "%A" e) |> Async.Start

#time

for _ in 0 .. 1000 do 
    counter <! msg 

counter <! (PrintState |> Command)

// open Akka.FSharp
// open Akka.Persistence
// open Akka.Persistence.FSharp


//     let update state e = (e.ToString())::state

//     // apply function can recover not only from received events, but also from state snapshot
//     let apply (mailbox: Eventsourced<Command,obj,string list>) state (event:obj) = 
//         match event with
//         | :? string as e -> update state e
//         | :? SnapshotOffer as o -> o.Snapshot :?> string list
//         | x -> 
//             mailbox.Unhandled x
//             state

//     let exec (mailbox: Eventsourced<Command,obj,string list>) state cmd =
//         match cmd with
//         | Update s -> mailbox.PersistEvent (update state) [s]
//         | TakeSnapshot -> mailbox.SaveSnapshot state
//         | Print -> printf "State is: %A\n" state
//         | Crash -> failwith "planned crash"

//     let run() =
    
//         printfn "--- SCENARIO 1 ---\n"
//         let s1 = 
//             spawnPersist system "s1" {
//                 state = []
//                 apply = apply
//                 exec = exec
//             } []
