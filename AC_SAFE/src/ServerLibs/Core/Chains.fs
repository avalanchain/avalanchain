namespace Avalanchain.Core

module Chains =

    open System
    open System.Collections.Generic
    open System.Reactive
    open System.Reactive.Subjects
    open System.Reactive.Linq
    open System.Text.RegularExpressions
    open System.Threading.Tasks
    open FSharp.Control.Reactive
    open FSharp.Control.Tasks
    open FSharp.Control.Tasks.ContextInsensitive
    open FSharpx.Result

    open Crypto
    open ChainDefs 
    open Database

    module PagedLog = 
        type EventLogError = 
            | IntegrityError of IntegrityError
            | DataStoreError of KVStore.ValueIssue

        type LogOfferReceipt = {
            // Account: TradingAccount // TODO: Change to PartitionId
            Token: string
            Json: string
            // Ref: TokenRef
            // Hash: Hash
            Pos: Pos option
        }

        type LogOfferReceipt<'T> = {
            Token: string
            Payload: 'T
            Pos: Pos option
        }        

        type EventLogView<'T> = {
            GetCount:           unit -> Task<Pos>
            GetPage:            Pos -> PageSize -> Task<Result<'T, EventLogError>[]>
            GetPageToken:       Pos -> PageSize -> Task<Result<string, EventLogError>[]>
            GetPageJwt:         Pos -> PageSize -> Task<Result<JwtToken<'T>, EventLogError>[]>
            GetLastPage:        PageSize -> Task<Result<'T, EventLogError>[]>
            GetLastPageToken:   PageSize -> Task<Result<string, EventLogError>[]>
            GetLastPageJwt:     PageSize -> Task<Result<JwtToken<'T>, EventLogError>[]>
            Subscribe:          unit -> IConnectableObservable<Result<LogOfferReceipt<'T>, 'T * EventLogError>>
        }

        type EventLog<'T> = {
            OfferAsync: 'T -> Task<Result<LogOfferReceipt<'T>, EventLogError>> // TODO: Add error handling
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
            let subject = Subject.broadcast
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
                                                        let tokenResult = result {  let! jwtToken = toJwt config.KeyVault.Active header v 
                                                                                    return jwtToken } 
                                                                            |> Result.mapError (SigningError >> IntegrityError)
                                                        let! (offerResult: Result<_, EventLogError>) = 
                                                            match tokenResult with 
                                                            | Ok jwtToken -> task { let! offerResult = pageLog.OfferAsync jwtToken.Token
                                                                                    return offerResult 
                                                                                            |> Result.map (fun _ -> {   Token = jwtToken.Token
                                                                                                                        // Json = jwtToken.Json
                                                                                                                        Payload = jwtToken.Payload
                                                                                                                        Pos = jwtToken.Header.pos } )
                                                                                            |> Result.mapError DataStoreError }
                                                            | Error e -> e |> Error |> Task.FromResult 
                                                        
                                                        offerResult // Update subscribers
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