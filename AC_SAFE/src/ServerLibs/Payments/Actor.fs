namespace Avalanchain.Core

open System
open FSharp.Control.AsyncSeqExtensions
open FSharp.Control
open Proto
open Proto.Persistence
open Proto.FSharp.Persistence
open Proto.Persistence.SnapshotStrategies
open Proto.FSharp

module Observable =
    open System.Reactive.Linq
    open FSharp.Control.Reactive

    /// Creates an observable sequence from the specified Subscribe method implementation.
    let create (subscribe: IObserver<'T> -> unit -> unit) =
        Observable.Create(Func<_,_>(fun o -> Action(subscribe o)))

module Persistence = 
    open Proto
    open Proto.Persistence
    open Proto.Persistence.SnapshotStrategies
    open Proto.FSharp
    open Proto.FSharp.Persistence

    let getEventsObservable<'T> (eventStore: IEventStore) (persistentID: string) (indexStart: int64) (indexEnd: int64) =
        Observable.create (fun observer -> 
                            fun () -> async {   let! _ = getEvents<'T> eventStore persistentID indexStart indexEnd observer.OnNext
                                                observer.OnCompleted() } |> Async.RunSynchronously)

    let eventSourcingAsyncSeqSink<'T> cancellationToken (eventStore: IEventStore) (persistentID: string) =
        let sinkSeqSrc = AsyncSeqSrc.create<'T>()
        let persistencePID = EventSourcing.persistLight eventStore persistentID |> Actor.spawnPropsPrefix persistentID

        sinkSeqSrc 
        |> AsyncSeqSrc.toAsyncSeq
        |> AsyncSeq.iter (fun e -> persistencePID <! e)
        |> fun aseq -> Async.Start (aseq, cancellationToken)

        sinkSeqSrc
    
    let getEventsAsyncSeq<'T> (maxPageSize: uint32) (eventStore: IEventStore) (persistentID: string) (indexStart: uint64) (indexEnd: uint64) =
        let indexStart = if indexStart > uint64 Int64.MaxValue then Int64.MaxValue else int64 indexStart
        let indexEnd = if indexEnd > uint64 Int64.MaxValue then Int64.MaxValue else int64 indexEnd
        let indexEnd = if indexEnd - indexStart + 1L > int64 maxPageSize then indexStart + (int64 maxPageSize) - 1L else indexEnd
        let sinkSeqSrc = AsyncSeqSrc.create()
        async { let! _ = getEvents<'T> eventStore persistentID indexStart indexEnd  (fun e -> sinkSeqSrc |> AsyncSeqSrc.put e)
                () } |> Async.Start
        sinkSeqSrc |> AsyncSeqSrc.toAsyncSeq


module PagedLog = 
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
    and LogError<'T> = 
        | InternalLogError of string

    let pagedLogHandler<'T> (hasher: 'T -> string) (toToken: 'T -> string) (maxPageSize: uint32) (provider: IProvider) (persistentID: string) =
        let commandProcessor: CommandProcessor<LogCommand<'T>, LogReply<'T>, string, LogError<'T>> =
            fun si _ i (cmd: LogCommand<'T>) -> async {
                let siTell e = si |> Option.iter(fun senderInfo -> senderInfo.Tell e) 
                match cmd with
                | Offer v -> 
                    return (Event { Pos = i; Val = v; Hash = hasher v; Token = toToken v } |> Some, true) |> Ok 
                | GetPos -> 
                    return (Pos i |> Some, false) |> Ok 
                | GetPage (indexStart, pageSize) -> 
                    let pageSize = if pageSize > maxPageSize then maxPageSize else pageSize
                    let indexStart = if indexStart > uint64 Int64.MaxValue then Int64.MaxValue - int64(pageSize) else int64 indexStart
                    let indexEnd = indexStart + int64(pageSize) - 1L
                    let mutable events = []
                    let! _ = getEvents provider persistentID indexStart indexEnd (fun e -> events <- e :: events)
                    return (EventPage (events |> List.rev) |> Some, false) |> Ok 
                | GetPageStreamed (guid, indexStart, pageSize) -> 
                    let pageSize = if pageSize > maxPageSize then maxPageSize else pageSize
                    let indexStart = if indexStart > uint64 Int64.MaxValue then Int64.MaxValue - int64(pageSize) else int64 indexStart
                    let indexEnd = indexStart + int64(pageSize) - 1L
                    let! _ = getEvents provider persistentID indexStart indexEnd (fun e -> (guid, e) |> SeqEvent |> siTell)
                    return (SeqComplete guid |> Some, false) |> Ok 
                | GetLastPage pageSize -> 
                    let pageSize = if pageSize > maxPageSize then maxPageSize else pageSize
                    let pageSize = int64(pageSize) - 1L // Just to make the math simpler
                    let indexStart, indexEnd =  if i < pageSize then 0L, i
                                                else i - pageSize, i
                    let mutable events = []
                    let! _ = getEvents provider persistentID indexStart indexEnd (fun e -> events <- e :: events)
                    return (EventPage (events |> List.rev) |> Some, false) |> Ok                    
                | GetLastPageStreamed (guid, pageSize) -> 
                    let pageSize = if pageSize > maxPageSize then maxPageSize else pageSize
                    let pageSize = int64(pageSize) - 1L // Just to make the math simpler
                    let indexStart, indexEnd =  if i < pageSize then 0L, i
                                                else i - pageSize, i
                    let! _ = getEvents provider persistentID indexStart indexEnd (fun e -> (guid, e) |> SeqEvent |> siTell)
                    return (SeqComplete guid |> Some, false) |> Ok 
            }
        CommandSourcingAndSnapshotting.persist<LogCommand<'T>, LogReply<'T>, string, LogError<'T>>
            provider
            commandProcessor
            (fun _ _ _ -> "None")
            (printfn "pagedLogHandler: %s")
            (IntervalStrategy 100)
            persistentID 
            "None"

    let getPage indexStart pageSize (pid: PID): Async<LogEvent<_> list> = async {
        let! res = pid <? GetPage(indexStart, pageSize)
        return match res with
                | Ok r -> match r with
                            | EventPage events -> events
                            | Pos _ | Event _ | SeqEvent _ | SeqComplete _ -> failwithf "Incorrect GetPage result type '%A'" (r.GetType())
                | Error e -> failwithf "Error during GetPage call: '%A'" (e)
    }

    let getLastPage pageSize (pid: PID): Async<LogEvent<_> list> = async {
        let! res = pid <? GetLastPage(pageSize)
        return match res with
                | Ok r -> match r with
                            | EventPage events -> events
                            | Pos _ | Event _ | SeqEvent _ | SeqComplete _ -> failwith "Incorrect GetLastPage result type"
                | Error e -> failwithf "Error during GetLastPage call: '%A'" (e)
    }
    
    let getPos (pid: PID): Async<int64> = async {
        let! res = pid <? GetPos
        return match res with
                | Ok r -> match r with
                            | Pos i -> i + 1L
                            | EventPage _ | Event _ | SeqEvent _ | SeqComplete _ -> failwith "Incorrect getPos result type"
                | Error e -> failwithf "Error during getPos call: '%A'" (e)                
    }

    let offerWithAck (pid: PID) (o: 'T): Async<LogEvent<'T>> = async {

        let! res = pid <? Offer o
        return match res with
                | Ok r -> match r with
                            | Event e -> e
                            | EventPage _ | Pos _ | SeqEvent _ | SeqComplete _ -> failwith "Incorrect Offer result type"
                | Error e -> failwithf "Error during Offer call: '%A'" (e)                
    }

    let offer (pid: PID) o = pid <! Offer o

    let a = EventStream.Instance.Subscribe<DeadLetterEvent>(Action<_>(fun o -> 
                                                                        printfn "DEAD LETTER: %A" o))