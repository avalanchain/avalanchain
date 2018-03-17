namespace Avalanchain.Core

open System
open FSharp.Control.AsyncSeqExtensions

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

    // type PersistenceActor() =
    //     interface IActor with

    let getEventsObservable<'T> (persistentID: string) (indexStart: int64) (indexEnd: int64) (eventStore: IEventStore) =
        Observable.create (fun observer -> 
                            fun () -> async {   let! _ = getEvents<'T> observer.OnNext persistentID indexStart indexEnd eventStore
                                                observer.OnCompleted() } |> Async.RunSynchronously)

    // let getEventsAsyncSeq<'T> (persistentID: string) (indexStart: int64) (indexEnd: int64) (eventStore: IEventStore) =
    //     let pid = (printfn "Hello from actor: %A") |> Actor.create |> Actor.spawnProps 
    //     async { let! _ = getEvents<'T> (pid.Tell) persistentID indexStart indexEnd eventStore 
    //             pid.Stop() }
        //|> AsyncSeq.m