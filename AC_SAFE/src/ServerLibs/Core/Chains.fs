namespace Avalanchain.Core

open System.Reactive.Linq
open FSharp.Control.Reactive
module Chains =

    open System
    open System.Collections.Generic
    open System.Reactive.Subjects
    open System.Text.RegularExpressions
    open System.Threading.Tasks
    open FSharp.Control.Reactive
    open FSharp.Control.Tasks
    open FSharp.Control.Tasks.ContextInsensitive
    open FSharpx.Result

    // open Akka.Actor
    // open Akka.Configuration
    // open Akka.IO
    // open Akka.Persistence
    // open Akka.Persistence.Serialization
    // // open Akka.Persistence.Journal
    // open Akka.Streams
    // open Akka.Streams.Dsl
    // open Reactive.Streams

    // open Akkling
    // open Akkling.Persistence
    // // open Akkling.Cluster
    // // open Akkling.Cluster.Sharding
    // open Akkling.Streams

    // open Akka.Persistence.Query
    // open Akka.Persistence.Query.Sql   

    open Crypto
    open ChainDefs 
    open Database
    open Node

    // type PersistEvent = {   
    //     Pos: Pos
    //     Token: Result<string, IntegrityError> 
    // }

    // type PersistCommand<'T> =
    //     | Offer of 'T
    //     | PrintState
    //     | TakeSnapshot
    //     | GetPos

    // type GetPosResult = GetPosResult of int64

    // type PersistState<'T> = {
    //     LastPos: int64
    //     Latest: PersistEvent option
    // }



    // type PersistentActor<'T>(pid, snapshotInterval: int64, keyVault: IKeyVault) as actor =
    //     inherit UntypedPersistentActor()
    //     let mutable state = { LastPos = -1L; Latest = None }
    //     let updateState evt = 
    //         state <- { state with Latest = Some evt }
    //     let updateStatePos (pos: Pos) = 
    //         state <- { state with LastPos = pos |> int64 }        
                    
    //     override __.PersistenceId = pid
    //     override __.OnRecover evt =
    //         match evt with 
    //         | :? SnapshotOffer as snapshot when (snapshot.Snapshot :? PersistState<'T>) -> 
    //             state <- (snapshot.Snapshot :?> PersistState<'T>) 
    //         | :? PersistEvent as event -> 
    //             updateState event
    //             updateStatePos (event.Pos)
    //         | :? RecoveryCompleted -> __.Log.Info ("Recovery completed for: {0}", actor.Self.Path)
    //         | a -> __.Log.Error ("Unhandled event: {0}", a)
    //     override __.OnCommand cmd =
    //         match cmd with
    //         | :? PersistCommand<'T> as c ->
    //             match c with 
    //             | Offer v -> 
    //                 let newPos = state.LastPos + 1L |> uint64
    //                 let header = newPos |> Some |> toHeader 
    //                 let token = result {    let! jwt = toJwt keyVault.Active header v 
    //                                         return jwt.Token } 
    //                             |> Result.mapError SigningError
    //                 let evt = { Pos = newPos; Token = token }
    //                 updateStatePos newPos
    //                 actor.PersistAsync(evt, Action<_>(updateState))
    //                 if state.LastPos % snapshotInterval = 0L then actor.SaveSnapshot(state)
    //             | PrintState -> __.Log.Info ("Actor state: " + (state.ToString()) + " PosN: " + (actor.LastSequenceNr.ToString()))
    //             | TakeSnapshot -> actor.SaveSnapshot(state)
    //             | GetPos -> __.Sender.Tell (GetPosResult state.LastPos)
    //         | a -> __.Log.Info ("Unhandled command: {0}", a)    

    // let persistActorProps<'T> pid (snapshotInterval: int64) (keyVault: IKeyVault): Props<PersistCommand<'T>> =
    //     Props.Create<PersistentActor<'T>>(pid, snapshotInterval, keyVault)
    //     |> Props.From

    // let persistActor<'T> system pid (snapshotInterval: int64) keyVault : IActorRef<PersistCommand<'T>> =
    //     persistActorProps<'T> pid snapshotInterval keyVault
    //     |> spawn system pid 

    // let persistSink2 pid snapshotInterval keyVault = Sink.ofProps(persistActorProps pid snapshotInterval keyVault)

    // let spawnIfNotExists (system: IActorRefFactory) name spawner =
    //     let selection = select system ((if system :? ActorSystem then "/user/" else "") + name)
    //     async {
    //         let! reply = selection <? Identify("correlation-id")
    //         return match reply with
    //                 | ActorIdentity("correlation-id", Some(ref)) -> ref // found
    //                 | _ -> spawner system  // not found
    //     } |> Async.RunSynchronously

    // type Pid = string
    // type SinkActorMessage = | GetSinkActorRef of Pid
    // let rec sinkHolder<'T> (snapshotInterval: int64) keyVault (context: Actor<SinkActorMessage>) = 
    //     function
    //         | GetSinkActorRef pid -> 
    //             let act = spawnIfNotExists context pid (fun ctx -> persistActor<'T> ctx pid snapshotInterval keyVault)
    //             context.Sender() <! act
    //             ignored ()

    // let spawnSinkHolder<'T> system (snapshotInterval: int64) keyVault = 
    //     let name = "_" + Regex("[\[\]`]").Replace(typedefof<'T>.Name, "_")
    //     spawnIfNotExists system name (fun ctx -> spawn ctx name <| props (actorOf2 (sinkHolder<'T> snapshotInterval keyVault)))
            
    // let getSinkActor<'T> system (snapshotInterval: int64) keyVault pid =
    //     let sh = spawnSinkHolder<'T> system snapshotInterval keyVault
    //     async { let! (ar: IActorRef<PersistCommand<'T>>) = sh <? GetSinkActorRef pid
    //             return ar } |> Async.RunSynchronously

    // let persistSink<'T> (system: ActorSystem) snapshotInterval keyVault pid = 
    //     let ar = getSinkActor<'T> system (snapshotInterval: int64) keyVault pid
    //     ar |> Sink.toActorRef (Offer Unchecked.defaultof<'T>)

    // let readJournal system = PersistenceQuery.Get(system).ReadJournalFor<SqlReadJournal>(SqlReadJournal.Identifier);

    // let currentEventsSource<'T> keyVault system verify pid from count: Source<Result<JwtToken<'T>, IntegrityError>, Akka.NotUsed> = 
    //     (readJournal system).CurrentEventsByPersistenceId(pid, from, from + count) 
    //     |> Source.map (fun e -> result {let event = (e.Event :?> PersistEvent)
    //                                     let! token = event.Token 
    //                                     return! fromJwt<'T> keyVault verify token |> Result.mapError VerificationError })

    // let allEventsSource<'T> keyVault system pid verify from count = 
    //     (readJournal system).EventsByPersistenceId(pid, from, from + count) 
    //     |> Source.map(fun e -> result { let event = (e.Event :?> PersistEvent)
    //                                     let! token = event.Token 
    //                                     return! fromJwt<'T> keyVault verify token |> Result.mapError VerificationError }) 

    // let persistFlow<'T> keyVault system snapshotInterval verify pid = 
    //     let sink = persistSink<'T> system snapshotInterval keyVault pid 
    //     Flow.ofSinkAndSourceMat sink Keep.none 
    //         (allEventsSource keyVault system pid verify 0L Int64.MaxValue) 
    
    // let streamCurrentPos<'T> keyVault system (snapshotInterval: int64) pid = 
    //     let actor = getSinkActor<'T> system (snapshotInterval: int64) keyVault pid
    //     async { let! (pos: GetPosResult) = actor <? GetPos
    //             return match pos with GetPosResult p -> p }

    module PagedLog = 
        type EventLogError = 
            | IntegrityError of IntegrityError
            | DataStoreError of KVStore.ValueIssue

        type EventLogView<'T> = {
            GetCount:           unit -> Task<Pos>
            GetPage:            Pos -> PageSize -> Task<Result<'T, EventLogError>[]>
            GetPageToken:       Pos -> PageSize -> Task<Result<string, EventLogError>[]>
            GetPageJwt:         Pos -> PageSize -> Task<Result<JwtToken<'T>, EventLogError>[]>
            GetLastPage:        PageSize -> Task<Result<'T, EventLogError>[]>
            GetLastPageToken:   PageSize -> Task<Result<string, EventLogError>[]>
            GetLastPageJwt:     PageSize -> Task<Result<JwtToken<'T>, EventLogError>[]>
            Subscribe:          unit -> IConnectableObservable<Result<'T, 'T * EventLogError>>
        }

        type EventLog<'T> = {
            OfferAsync: 'T -> Task<Result<unit, EventLogError>> // TODO: Add error handling
            View: EventLogView<'T>
        }

        type LogCommand<'T> =
            | Offer of 'T
            | GetPage of indexStart: uint64 * pageSize: uint32
            | GetPageStreamed of requestId: Guid * indexStart: uint64 * pageSize: uint32
            | GetLastPage of pageSize: uint32
            | GetLastPageStreamed of requestId: Guid * pageSize: uint32
            | GetPos
        type LogReply<'T> =
            | Pos of int64
            | Event of LogEvent<'T>
            | EventPage of LogEvent<'T> list
            | SeqEvent of Guid * LogEvent<'T>
            | SeqComplete of Guid
        and LogEvent<'T> = {
            Pos: int64
            Val: 'T
            Hash: string
            Token: string
        }

        type StreamingConfig = {
//            Node: ACNode
            SnapshotInterval: int64
            // OverflowStrategy: OverflowStrategy
            QueueMaxBuffer: int
            Verify: bool 
            KeyVault: IKeyVault
        }

        // let mapLogError = Result.mapError IntegrityError
        let private mapLogPage f = Array.map (Result.map f)

//        let eventLog1<'T> (config: StreamingConfig) (pidPrefix: string): EventLog<'T> =
//            let pid = pidPrefix + "__" + typedefof<'T>.Name
//            let getCount() =  task { let! count = streamCurrentPos<_> config.KeyVault config.Node.System config.SnapshotInterval pid
//                                     return count + 1L |> uint64 }
//            let getPage from count = task { let! items = currentEventsSource<'T> config.KeyVault config.Node.System config.Verify pid (int64 from) (int64 count)
//                                                            |> Source.runWith (config.Node.System.Materializer()) (Akkling.Streams.Sink.fold [] (fun s e -> e :: s)) 
//                                            return items |> List.rev |> List.toArray |> Array.map (Result.mapError IntegrityError) }
//            let getLastPage count = task {  let countL = uint64(count)
//                                            let! length = getCount()
//                                            return! if length > uint64(countL) then getPage (length - countL) count
//                                                    else getPage 0UL count } 
//            let eventLogView() = {  GetCount = getCount
//                                    GetPageJwt = getPage
//                                    GetPage = fun from count -> task {  let! page = getPage from count 
//                                                                        return page |> mapLogPage (fun t -> t.Payload) } 
//                                    GetPageToken = fun from count -> task { let! page = getPage from count 
//                                                                            return page |> mapLogPage (fun t -> t.Token) }
//                                    GetLastPageJwt = getLastPage
//                                    GetLastPage = fun count -> task {  let! page = getLastPage count 
//                                                                       return page |> mapLogPage (fun t -> t.Payload) }
//                                    GetLastPageToken = fun count -> task { let! page = getLastPage count 
//                                                                           return page |> mapLogPage (fun t -> t.Token) }
//                                }
//                                
//            let queue: ISourceQueueWithComplete<'T> = Source.queue config.OverflowStrategy config.QueueMaxBuffer 
//                                                        |> Source.map (PersistCommand.Offer)
//                                                        |> Source.toMat (persistSink config.Node.System config.SnapshotInterval config.KeyVault pid) Keep.left 
//                                                        |> Graph.run config.Node.Mat
//            {   View = eventLogView()
//                OfferAsync = fun v -> task {let! _ = queue.OfferAsync v
//                                            return Ok () } }  // TODO: Handle error
        
        
        open KVStore
        open PagedLog
        open LightningDB
            
        let connectionString = """DataSource=./database.sqlite; Cache = Shared"""
        // let connectionString = """DataSource=:memory:; Cache = Shared"""
        let connectionStringReadOnly = """DataSource=./database.sqlite?mode=ro"""
        let connection = connect connectionString
        // let connectionReadOnly = connect connectionStringReadOnly

        let eventLog<'T> (config: StreamingConfig) (pidPrefix: string): Task<EventLog<'T>> = task {
            let pid = pidPrefix + "__" + typedefof<'T>.Name
            let! pageLog = createLog connection connection pid
            
            let getCount() = task {  let! countRes = pageLog.View.GetCount() 
                                     return match countRes with 
                                            | Ok count -> count 
                                            | Error e -> failwith (e.ToString()) }
            let getPage from count = task { let! items = pageLog.View.GetPage from count  
                                            let page = [| for i in from .. 1UL .. from + uint64(count) - 1UL -> result {
                                                            let! value = match items.TryGetValue i with
                                                                            | (true, v) -> v |> Result.mapError DataStoreError
                                                                            | _ -> NoDataExists |> ValueAccessIssue |> DataStoreError |> Error
                                                            let! jwt = fromJwt<'T> config.KeyVault config.Verify value.Value |> Result.mapError (VerificationError >> IntegrityError)
                                                            return jwt 
                                                        }|]
                                            return page }
            let getLastPage count = task {  let countL = uint64(count)
                                            let! length = getCount()
                                            return! if length > uint64(countL) then getPage (length - countL) count
                                                    else getPage 0UL count } 
            let subject = new Subject<_>()
            subject.Subscribe() |> ignore

            let eventLogView() = {  GetCount = getCount
                                    GetPageJwt = getPage
                                    GetPage = fun from count -> task {  let! page = getPage from count 
                                                                        return page |> mapLogPage (fun t -> t.Payload) } 
                                    GetPageToken = fun from count -> task { let! page = getPage from count 
                                                                            return page |> mapLogPage (fun t -> t.Token) }
                                    GetLastPageJwt = getLastPage
                                    GetLastPage = fun count -> task {  let! page = getLastPage count 
                                                                       return page |> mapLogPage (fun t -> t.Payload) }
                                    GetLastPageToken = fun count -> task { let! page = getLastPage count 
                                                                           return page |> mapLogPage (fun t -> t.Token) }
                                    Subscribe = fun () -> 
                                        let obs = subject |> Observable.publish
                                        obs.Connect() |> ignore
                                        obs
                                }
                                
            return {    EventLog.View = eventLogView()
                        OfferAsync = fun v -> task {    let! lastPos = getCount()
                                                        let newPos = lastPos + 1UL 
                                                        let header = newPos |> Some |> toHeader 
                                                        let tokenResult = result {  let! jwt = toJwt config.KeyVault.Active header v 
                                                                                    return jwt.Token } 
                                                                            |> Result.mapError (SigningError >> IntegrityError)
                                                        let! (offerResult: Result<unit, EventLogError>) = 
                                                            match tokenResult with 
                                                            | Ok token -> task {    let! offerResult = pageLog.OfferAsync token
                                                                                    return offerResult |> Result.mapError DataStoreError }
                                                            | Error e -> e |> Error |> Task.FromResult 
                                                        
                                                        offerResult // Update subscribers
                                                        |> Result.map (fun _ -> v) 
                                                        |> Result.mapError (fun e -> v, e) 
                                                        |> subject.OnNext 
                                                        
                                                        return offerResult
                                                        } }  
        }                                                             

        //////////////////////////////

        // let private env = newLightningEnvironment "ac"
        // let private kvStore = LmdbKVStore(logKeySerializer "", env, "streams", PutOptions.None) 
                                                
        // let eventLog<'T> (config: StreamingConfig) (pidPrefix: string): EventLog<'T> =
        //     let pid = pidPrefix + "__" + typedefof<'T>.Name
        //     let pageLog = createLogView kvStore pid
            
        //     let getCount() = async { let! countRes = pageLog.View.GetCount() |> Async.AwaitTask 
        //                              return match countRes with 
        //                                     | Ok count -> count 
        //                                     | Error e -> failwith (e.ToString()) }
        //     let getPage from count = async {let! items = pageLog.View.GetPage from count |> Async.AwaitTask 
        //                                     let page = [| for i in from .. from + uint64(count) -> result {
        //                                                     let! value = match items.TryGetValue i with
        //                                                                     | (true, v) -> v |> Result.mapError DataStoreError
        //                                                                     | _ -> NoDataExists |> ValueAccessIssue |> DataStoreError |> Error
        //                                                     let! jwt = fromJwt<'T> config.KeyVault config.Verify value.Value |> Result.mapError (VerificationError >> IntegrityError)
        //                                                     return jwt 
        //                                                 }|]
        //                                     return page }
        //     let getLastPage count = async { let countL = uint64(count)
        //                                     let! length = getCount()
        //                                     return! if length > uint64(countL) then getPage (length - countL) count
        //                                             else getPage 0UL count } 
        //     let eventLogView() = {  GetCount = getCount
        //                             GetPageJwt = getPage
        //                             GetPage = fun from count -> async { let! page = getPage from count 
        //                                                                 return page |> mapLogPage (fun t -> t.Payload) } 
        //                             GetPageToken = fun from count -> async {let! page = getPage from count 
        //                                                                     return page |> mapLogPage (fun t -> t.Token) }
        //                             GetLastPageJwt = getLastPage
        //                             GetLastPage = fun count -> async { let! page = getLastPage count 
        //                                                                return page |> mapLogPage (fun t -> t.Payload) }
        //                             GetLastPageToken = fun count -> async {let! page = getLastPage count 
        //                                                                    return page |> mapLogPage (fun t -> t.Token) }
        //                         }
                                
        //     let queue: ISourceQueueWithComplete<'T> = Source.queue config.OverflowStrategy config.QueueMaxBuffer 
        //                                                 |> Source.map (PersistCommand.Offer)
        //                                                 |> Source.toMat (persistSink config.Node.System config.SnapshotInterval config.KeyVault pid) Keep.left 
        //                                                 |> Graph.run config.Node.Mat
        //     {   View = eventLogView()
        //         OfferAsync = fun v -> async {   let! lastPos = getCount()
        //                                         let newPos = lastPos + 1UL 
        //                                         let header = newPos |> Some |> toHeader 
        //                                         let tokenResult = result {  let! jwt = toJwt config.KeyVault.Active header v 
        //                                                                     return jwt.Token } 
        //                                                             |> Result.mapError (SigningError >> IntegrityError)
        //                                         let! (offerResult: Result<unit, EventLogError>) = 
        //                                             match tokenResult with 
        //                                             | Ok token -> task {    let! offerResult = pageLog.OfferAsync token
        //                                                                     return offerResult |> Result.mapError DataStoreError }
        //                                             | Error e -> e |> Error |> Task.FromResult 
        //                                             |> Async.AwaitTask
        //                                         return match offerResult with
        //                                                 | Ok () -> ()
        //                                                 | Error e -> failwithf "Error on save '%A'" e
        //                                         } }                                                               