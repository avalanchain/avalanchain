#r "../bin/Avalanchain/Akka.dll"
#r "../bin/Avalanchain/Hyperion.dll"
#r "../bin/Avalanchain/Newtonsoft.Json.dll"
#r "../bin/Avalanchain/FSharp.PowerPack.dll"
#r "../bin/Avalanchain/FSharp.PowerPack.Linq.dll"
#r "../bin/Avalanchain/Akkling.dll"
#r "../bin/Avalanchain/Google.ProtocolBuffers.dll"
#r "../bin/Avalanchain/Akka.Persistence.dll"
#r "../bin/Avalanchain/Akkling.Persistence.dll"
#r "../bin/Avalanchain/Akka.Streams.dll"
#r "../bin/Avalanchain/Akkling.Streams.dll"
#r "../bin/Avalanchain/Reactive.Streams.dll"
#r "../bin/Avalanchain/System.Collections.Immutable.dll"


open System
open Akka.Streams
open Akka.Streams.Dsl
open Reactive.Streams
open Akka.Persistence
open Newtonsoft

open Akkling
open Akkling.Persistence
open Akkling.Streams


let configWith() =
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
    config.WithFallback <| Configuration.defaultConfig()


let system = System.create "persisting-streams-sys" <| configWith()
let mat = system.Materializer()

type CounterChanged =
    { Delta : int }

type CounterCommand =
    | Inc
    | Dec
    | GetState

type CounterMessage =
    | Command of CounterCommand
    | Event of CounterChanged

let persistActor (queue: ISourceQueue<CounterChanged>) =
    propsPersist(fun mailbox ->
        let rec loop state =
            actor {
                let! msg = mailbox.Receive()
                match msg with
                | Event(changed) ->
                    queue.AsyncOffer(changed) |!> retype mailbox.Self
                    return! loop (state + changed.Delta)
                | Command(cmd) ->
                    match cmd with
                    | GetState ->
                        mailbox.Sender() <! state
                        return! loop state
                    | Inc -> return Persist (Event { Delta = 1 })
                    | Dec -> return Persist (Event { Delta = -1 })
            }
        loop 0)

let persistentQueue<'T> system pid (overflowStrategy: OverflowStrategy) (maxBuffer: int) =
    Source.queue overflowStrategy maxBuffer
    |> Source.mapMaterializedValue(persistActor >> spawn system pid)

let source = persistentQueue<CounterChanged> system "pa1" OverflowStrategy.DropNew 1000

let actorRef, arr = async {
                        return source
                                |> Source.toMat (Sink.forEach(printfn "Piu: %A")) Keep.both
                                |> Graph.run mat
                    }
                    |> Async.RunSynchronously

arr |> Async.Start

let persistView pid (queue: ISourceQueue<obj>) =
    propsView pid (fun mailbox ->
        let rec loop state =
            actor {
                let! msg = mailbox.Receive()
                match msg with
                | Event(changed) ->
                    queue.AsyncOffer(changed) |!> retype mailbox.Self
                    return! loop (state + changed.Delta)
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

let sourceView = persistentViewQueue system "pa1" OverflowStrategy.DropNew 1000

let aa, aar = async {
                    return sourceView
                            |> Source.toMat (Sink.forEach(printfn "Piu2: %A")) Keep.both
                            |> Graph.run mat
                }
                |> Async.RunSynchronously

aar |> Async.Start

retype aa <! (Akka.Persistence.Update true)


actorRef <! Command Inc
actorRef <! Command Inc
actorRef <! Command Dec
async { let! reply = actorRef <? Command GetState
        printfn "Current state of %A: %i" actorRef reply.Value } |> Async.RunSynchronously

async { let! reply = aa <? Command GetState
        printfn "Current state of %A: %i" actorRef reply.Value } |> Async.RunSynchronously

//////

type Sender = Sender of string
type Packet<'k, 'v when 'v: comparison> = {
    Sender: Sender
    Key: 'k
    Value: 'v
}
type ConfirmationStatus<'v when 'v: comparison> =
    | Confirmed of Value: 'v
    | Reconfirmed of Value: 'v * PreviousValue: 'v * PreviousCount: uint32
    | NotConfirmed
type ConfirmationState<'v when 'v: comparison> = {
    Status: ConfirmationStatus<'v>
    MinConfCount: uint32
    Values: Map<'v, Set<Sender>>
    ValuesCount: uint32
}
    with
        static member Empty() = {   Status = NotConfirmed
                                    MinConfCount = UInt32.MaxValue
                                    Values = Map.empty
                                    ValuesCount = 0u }
        member __.Value = match __.Status with Confirmed v | Reconfirmed (v, _, _) -> Some v | NotConfirmed -> None

type ConfirmationAttemptResult<'v when 'v: comparison> =
    | Added of ConfirmationState<'v>
    | SenderCheckFailed of ConfirmationState<'v>
    member __.State = match __ with Added s -> s | SenderCheckFailed s -> s

type Confirmations<'k, 'v when 'v: comparison>(senderChecker: Packet<'k, 'v> -> bool, minConfCount: uint32) =
    let mutable state: ConfirmationState<'v> = { ConfirmationState<'v>.Empty() with MinConfCount = minConfCount }
    member __.State = state
    member __.TryAdd(packet: Packet<'k, 'v>) =
        if senderChecker packet then
            let oldSendersForValue = match state.Values |> Map.tryFind packet.Value with
                                        | Some vv -> vv
                                        | None -> Set.empty
            let newSendersForValue = oldSendersForValue |> Set.add packet.Sender
            let newValues = state.Values |> Map.add packet.Value newSendersForValue
            let newValue, newCount =
                newValues
                |> Map.toSeq
                |> Seq.map(fun (v, ss) -> (v, ss |> Set.count |> uint32))
                |> Seq.maxBy(fun (_, c) -> c) // The Seq _will_ always have at list one element as we just added it

            let newStatus =
                if newCount < state.MinConfCount then NotConfirmed
                else
                    match state.Status with
                    | NotConfirmed -> Confirmed newValue
                    | Confirmed (value) when value = newValue || (value <> newValue && state.ValuesCount = newCount) -> state.Status // Keep existing value if counts the same
                    | Confirmed (value) -> Reconfirmed (newValue, value, state.ValuesCount)
                    | Reconfirmed _ -> Confirmed (newValue)

            let newState = { state with Values = newValues; ValuesCount = newCount; Status = newStatus }
            state <- newState
            Added state
        else
            SenderCheckFailed state


let confs = Confirmations<int, string>((fun p -> p.Sender <> Sender "wrong"), 3u)
let state = confs.State

confs.TryAdd { Sender = Sender "good 1"; Key = 1; Value = "val 1" }
confs.TryAdd { Sender = Sender "good 2"; Key = 1; Value = "val 1" }
confs.TryAdd { Sender = Sender "wrong"; Key = 1; Value = "val 2" }
confs.TryAdd { Sender = Sender "good 3"; Key = 1; Value = "val 2" }
confs.TryAdd { Sender = Sender "good 4"; Key = 1; Value = "val 4" }
confs.TryAdd { Sender = Sender "good 5"; Key = 1; Value = "val 1" }
confs.TryAdd { Sender = Sender "good 5"; Key = 1; Value = "val 2" } // Can vote for two values
confs.TryAdd { Sender = Sender "good 6"; Key = 1; Value = "val 2" }
confs.TryAdd { Sender = Sender "good 7"; Key = 1; Value = "val 2" }
