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

    let persistWithCommandSourcingAndSnapshoting 
        (provider: IProvider) 
        (processCommand: 'State -> int64 -> 'Command -> Result<'Event, 'CommandError>)
        (recoverEvent: 'State -> int64 -> 'Event -> 'State) 
        (replayEvent: 'State -> int64 -> 'Event -> 'State)
        (persistedEvent: 'State -> int64 -> 'Event -> 'State)
        (recoverSnapshot: (int64 -> 'State -> 'State) option) 
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
                | :? 'State as st -> state <-   match recoverSnapshot with 
                                                | Some rs -> rs snapshot.Index st
                                                | None -> st
                | _ -> failwithf "Unsupported snapshot type: '%A'" (snapshot.State.GetType())
            | :? PersistedSnapshot as ps -> () // TODO: Add logging
            | _ -> printfn "Unsupported snapshot type: '%A'" (snapshot.State.GetType())

        let persistence = 
            Persistence.WithEventSourcingAndSnapshotting(
                provider, 
                provider, 
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


    let persistWithEventSourcingAndSnapshoting = persistWithCommandSourcingAndSnapshoting 