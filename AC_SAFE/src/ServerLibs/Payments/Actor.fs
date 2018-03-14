namespace Avalanchain.Core

open Proto.FSharp
[<RequireQualifiedAccess>]
module Actor = 
    open Proto
    open Proto.Persistence
    open Proto.Persistence.SnapshotStrategies
    open Proto.FSharp

    // type PersistenceActor() =
    //     interface IActor with

    let persistWithCommandSourcingAndSnapshotingDetailed 
        (eventStore: IEventStore) 
        (snapshotStore: ISnapshotStore) 
        (processCommand: 'State -> int64 -> 'Command -> Result<'Event, 'CommandError>)
        (recoverEvent: 'State -> int64 -> 'Event -> 'State) 
        (replayEvent: 'State -> int64 -> 'Event -> 'State)
        (persistedEvent: 'State -> int64 -> 'Event -> 'State)
        (recoverSnapshot: (int64 -> 'State -> 'State) option) 
        (persistedSnapshot: (int64 -> 'State -> 'State) option) 
        (initialState: 'State)
        (snapshotStrategy: ISnapshotStrategy)
        (persistentID: string) 
        = 
        let mutable state = initialState
        let applyEvent (evt: Event) =
            match evt with
            | :? RecoverEvent as e -> 
                match e.Data with 
                | :? 'Event as event -> state <- recoverEvent state evt.Index event 
                | data -> failwithf "Unsupported recover event type: '%A'" (data.GetType())
            | :? ReplayEvent as e -> 
                match e.Data with 
                | :? 'Event as event -> state <- replayEvent state evt.Index event 
                | data -> failwithf "Unsupported replay event type: '%A'" (data.GetType())
            | :? PersistedEvent as e -> 
                match e.Data with 
                | :? 'Event as event -> state <- persistedEvent state evt.Index event 
                | data -> failwithf "Unsupported persisted event type: '%A'" (data.GetType())
            | e -> failwithf "Unhandled event: '%A'" e

        let applySnapshot (snapshot: Snapshot) =
            match snapshot with
            | :? RecoverSnapshot as rs ->
                match rs.State with
                | :? 'State as st ->    match recoverSnapshot with 
                                        | Some rs -> state <- rs snapshot.Index st
                                        | None -> ()
                | _ -> failwithf "Unsupported snapshot type: '%A'" (snapshot.State.GetType())
            | :? PersistedSnapshot as ps -> 
                match ps.State with
                | :? 'State as st ->    match persistedSnapshot with 
                                        | Some rs -> state <- rs snapshot.Index st
                                        | None -> ()
                | _ -> failwithf "Unsupported snapshot type: '%A'" (snapshot.State.GetType())
            | _ -> printfn "Unsupported snapshot type: '%A'" (snapshot.State.GetType())

        let persistence = 
            Persistence.WithEventSourcingAndSnapshotting(
                eventStore, 
                snapshotStore, 
                persistentID,
                System.Action<_>(applyEvent), 
                System.Action<_>(applySnapshot),
                snapshotStrategy, 
                fun () -> state :> obj)

        let systemHandler (ctx: IContext) sm: Async<unit> = 
            match sm with 
            | Started _ -> async { do! persistence.RecoverStateAsync() }
            | _ -> async {()}

        let handler (ctx: IContext) (cmd: 'Command): Async<unit> = 
            let res = processCommand state persistence.Index cmd
            match res with
            | Ok evt -> async { do! persistence.PersistEventAsync evt |> Async.AwaitTask 
                                ctx.Sender <! Ok() }
            | Error e -> async { e >! ctx.Sender }

        Actor.create3Async systemHandler handler


    let persistWithEventSourcingAndSnapshotingDetailed
        (eventStore: IEventStore) 
        (snapshotStore: ISnapshotStore) 
        (recoverEvent: 'State -> int64 -> 'Event -> 'State) 
        (replayEvent: 'State -> int64 -> 'Event -> 'State)
        (persistedEvent: 'State -> int64 -> 'Event -> 'State)
        (recoverSnapshot: (int64 -> 'State -> 'State) option) 
        (persistedSnapshot: (int64 -> 'State -> 'State) option) 
        (initialState: 'State)
        (snapshotStrategy: ISnapshotStrategy)
        (persistentID: string)
        = 
        persistWithCommandSourcingAndSnapshotingDetailed 
            eventStore 
            snapshotStore
            (fun _ _ cmd -> Ok cmd)
            recoverEvent
            replayEvent
            persistedEvent
            recoverSnapshot
            persistedSnapshot
            initialState
            snapshotStrategy
            persistentID

    let persistWithEventSourcingAndSnapshoting
        (provider: IProvider) 
        (onEvent: 'State -> int64 -> 'Event -> 'State) 
        (recoverSnapshot: (int64 -> 'State -> 'State) option) 
        (persistedSnapshot: (int64 -> 'State -> 'State) option) 
        (initialState: 'State)
        (snapshotStrategy: ISnapshotStrategy)
        (persistentID: string)
        = 
        persistWithEventSourcingAndSnapshotingDetailed 
            provider 
            provider
            onEvent
            onEvent
            onEvent
            recoverSnapshot
            persistedSnapshot
            initialState
            snapshotStrategy
            persistentID    