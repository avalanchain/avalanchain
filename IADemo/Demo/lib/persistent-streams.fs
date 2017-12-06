namespace Avalanchain

module PersistentStreams =

    open System
    open Akka.Streams
    open Akka.Streams.Dsl
    open Reactive.Streams
    open Akka.Persistence
    open Newtonsoft

    open Akkling
    open Akkling.Persistence
    open Akkling.Streams
    open System.Threading.Tasks


    let configPersistence(fallback) =
        let config = Configuration.parse("""
            akka {
                persistence {
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
                      max-buffer-size = 100
                    }
                }
            }
            """)
        config.WithFallback <| fallback


    //let system = System.create "persisting-streams-sys" <| configWith <| Configuration.defaultConfig()
    //let mat = system.Materializer()

    type JwtToken = string

    type TokenEvent = {
        jwt: JwtToken
    }

    type TokenCommand =
        | Save of JwtToken 
        | Complete
        | GetState

    type TokenMessage =
        | Command of TokenCommand
        | Event of TokenEvent

    let internal persistActor (queue: ISourceQueue<TokenEvent>) =
        propsPersist(fun mailbox ->
            let rec loop (pos, completed) =
                actor {
                    let! msg = mailbox.Receive()
                    match msg with
                    | Event(changed) ->
                        queue.AsyncOffer(changed) |!> retype mailbox.Self
                        return! loop (pos + 1, completed)
                    | Command(cmd) ->
                        match cmd with
                        | GetState ->
                            mailbox.Sender() <! (pos, completed)
                            return! loop (pos, completed)
                        | Complete -> return! loop (pos, true)
                        | Save jwt ->   if completed then return! loop (pos, completed)
                                        else return Persist (Event { jwt = jwt })
                }
            loop (0, false))

    type QueueProxy<'t> = {
        mutable Queue: ISourceQueueWithComplete<'t> option
    }
    with 
        interface ISourceQueue<'t> with
            member __.OfferAsync el = match __.Queue with
                                        | Some queue -> queue.OfferAsync el
                                        | None -> Task.FromResult(Exception("Receiving queue is not setup") |> QueueOfferResult.Failure :> IQueueOfferResult) 
            member __.WatchCompletionAsync() = match __.Queue with
                                                | Some queue -> queue.WatchCompletionAsync()
                                                | None -> Task.FromException(Exception("Receiving queue is not setup"))
        interface ISourceQueueWithComplete<'t> with
            member __.Complete() = match __.Queue with
                                    | Some queue -> queue.Complete()
                                    | None -> raise (Exception("Receiving queue is not setup"))
            member __.Fail ex = match __.Queue with
                                | Some queue -> queue.Fail ex
                                | None -> raise (Exception("Receiving queue is not setup"))

    let persistentQueue<'T> system pid (overflowStrategy: OverflowStrategy) (maxBuffer: int) =
        Source.queue overflowStrategy maxBuffer
        |> Source.mapMaterializedValue(persistActor >> spawn system pid)

    let persistentFlow<'T> system pid (overflowStrategy: OverflowStrategy) (maxBuffer: int) =
        let queueProxy: QueueProxy<TokenEvent> = { Queue = None }
        let sink = Sink.toActorRef (Command Complete) (queueProxy |> persistActor |> spawn system pid)
        Source.queue overflowStrategy maxBuffer
        |> Flow.ofSinkAndSourceMat (sink) 
            (fun ar sourceQueue -> queueProxy.Queue <- Some(sourceQueue); (ar, sourceQueue))

    let internal persistView pid (queue: ISourceQueue<TokenEvent>) =
        propsView pid (fun mailbox ->
            let rec loop state =
                actor {
                    let! msg = mailbox.Receive()
                    match msg with
                    | Event(changed) ->
                        queue.AsyncOffer(changed) |!> retype mailbox.Self
                        return! loop (state + 1)
                    | Command(cmd) ->
                        match cmd with
                        | GetState ->
                            mailbox.Sender() <! state
                            return! loop state
                        | _ -> return! loop (state) // ignoring
                }
            loop 0)

    let persistentViewQueue system pid (overflowStrategy: OverflowStrategy) (maxBuffer: int) =
        Source.queue overflowStrategy maxBuffer
        |> Source.mapMaterializedValue(persistView pid >> spawnAnonymous system)



    // let source = persistentQueue<TokenEvent> system "pa1" OverflowStrategy.DropNew 1000

    // let actorRef, arr = async {
    //                         return source
    //                                 |> Source.toMat (Sink.forEach(printfn "Piu: %A")) Keep.both
    //                                 |> Graph.run mat
    //                     }
    //                     |> Async.RunSynchronously

    // arr |> Async.Start

    // let sourceView = persistentViewQueue system "pa1" OverflowStrategy.DropNew 1000

    // let aa, aar = async {
    //                     return sourceView
    //                             |> Source.toMat (Sink.forEach(printfn "Piu2: %A")) Keep.both
    //                             |> Graph.run mat
    //                 }
    //                 |> Async.RunSynchronously

    // aar |> Async.Start

    // retype aa <! (Akka.Persistence.Update true)


    // actorRef <! Command Inc
    // actorRef <! Command Inc
    // actorRef <! Command Dec
    // async { let! reply = actorRef <? Command GetState
    //         printfn "Current state of %A: %i" actorRef reply } |> Async.RunSynchronously

    // async { let! reply = aa <? Command GetState
    //         printfn "Current state of %A: %i" actorRef reply } |> Async.RunSynchronously

