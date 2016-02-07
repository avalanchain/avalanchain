module Avalanchain.Cluster.SQLite

open Akka.Actor
open Akka.Cluster
open Akka.Persistence.Sqlite
open Akka.Cluster.Sharding
open Akka.FSharp
open Akka.FSharp.Actors
open Akka.FSharp.Spawn
open System
open FSharp.Core
open System.Collections.Immutable
open System.Data
open System.Data.SQLite
open Microsoft.FSharp.Quotations

open Avalanchain.Cluster.Messages
open Akka.Persistence.FSharp
open Akka.Persistence


[<Interface>]
type IAutomaticCluster = 
    inherit IDisposable

type AutomaticClusterSqlite (system) =
    let cluster = Cluster.Get(system);
    let persistence = SqlitePersistence.Get(system);
    let connectionFactory() = 
        let conn = new SQLiteConnection(persistence.JournalSettings.ConnectionString)
        conn.Open()
        conn
        
    let initializeNodesTable() =
        use cmd = new SQLiteCommand(connectionFactory())
        cmd.CommandText <- @"CREATE TABLE IF NOT EXISTS cluster_nodes (
            member_address VARCHAR(255) NOT NULL PRIMARY KEY
        );"
        cmd.ExecuteNonQuery() |> ignore // TODO: Add error handling

    let getClusterMembers() = // TODO: Replace with a TypeProvider
        let mutable result = []
        use cmd = new SQLiteCommand(@"SELECT member_address from cluster_nodes", connectionFactory())
        use reader = cmd.ExecuteReader()
        while reader.Read() do
            result <- (Address.Parse (reader.GetString(0))) :: result
        List.rev result

    let addClusterMember(address: Address) =
        use cmd = new SQLiteCommand(@"INSERT INTO cluster_nodes(member_address) VALUES (@addr)", connectionFactory())
        use tx = cmd.Connection.BeginTransaction()
        cmd.Transaction <- tx
        let addr = address.ToString()
        cmd.Parameters.Add("@addr", DbType.String).Value <- addr

        cmd.ExecuteNonQuery() |> ignore // TODO: Add error handling
        tx.Commit()

    let removeClusterMember(address: Address) =
        use cmd = new SQLiteCommand(@"DELETE FROM cluster_nodes WHERE member_address = @addr", connectionFactory())
        use tx = cmd.Connection.BeginTransaction()
        cmd.Transaction <- tx;
        let addr = address.ToString()
        cmd.Parameters.Add("@addr", DbType.String).Value <- addr

        cmd.ExecuteNonQuery() |> ignore // TODO: Add error handling
        tx.Commit()

    let joinCluster() = 
        initializeNodesTable()

        let members = getClusterMembers()
        match members with
        | [] -> 
            let self = cluster.SelfAddress
            addClusterMember(self)
            cluster.JoinSeedNodes(ImmutableList.Create(self))
        | _ ->
            cluster.JoinSeedNodes(ImmutableList.ToImmutableList(members))
            addClusterMember(cluster.SelfAddress)
    do joinCluster()

    interface IAutomaticCluster with
        member __.Dispose() = removeClusterMember(cluster.SelfAddress)
        

type EventSourcingLogic<'TAdminCommand, 'TBusinessCommand, 'TState, 'TEvent> = {
    UpdateState: 'TState -> 'TEvent -> 'TState
    ProcessBusinessCommand: 'TBusinessCommand -> 'TEvent option // TODO: Add Chessie error reporting
    ProcessAdminCommand: 'TAdminCommand -> 'TEvent option // TODO: Add Chessie error reporting
}

type ResActor<'TAdminCommand, 'TBusinessCommand, 'TState, 'TEvent> (eventSourcingLogic) as self = 
    inherit PersistentActor()
    let mutable state = Unchecked.defaultof<'TState>
    do (UntypedActor.Context.SetReceiveTimeout(Nullable(TimeSpan.FromMinutes(2.0))))
    member private __.Self = base.Self
    member private __.Context = UntypedActor.Context
    override __.PersistenceId with get() = (sprintf "Actor %s-%s" (self.Context.Parent.Path.Name) self.Self.Path.Name)
    override __.ReceiveRecover(msg: obj) = 
        match msg with 
        | :? 'TEvent as e -> 
            state <- eventSourcingLogic.UpdateState state e
            true
        | :? SnapshotOffer as so -> 
            match so.Snapshot with
            | :? 'TState as sos -> 
                state <- sos
                true
            | _ -> false
        | _ -> false
    override this.ReceiveCommand(msg: obj) = 
        match msg with 
        | :? 'TBusinessCommand as c -> 
            match eventSourcingLogic.ProcessBusinessCommand c with 
            | Some e -> this.Persist(e, (fun ee -> (state <- eventSourcingLogic.UpdateState state ee) |> ignore))
                        true
            | None -> false
        | :? 'TAdminCommand as c -> 
            match eventSourcingLogic.ProcessAdminCommand c with 
            | Some e -> this.Persist(e, (fun ee -> (state <- eventSourcingLogic.UpdateState state ee) |> ignore)) // TODO: Rethink Admin channel logic
                        true
            | None -> false
        | _ -> false

type ShardedSystem (system, clusterFactory: ActorSystem -> IAutomaticCluster) =
    let automaticCluster = clusterFactory(system)
    let sharding = ClusterSharding.Get(system)
    member this.System = system
    member this.StartShardRegion<'Message, 'TAdminCommand, 'TBusinessCommand, 'TState, 'TEvent> messageExtractor eventSourcingLogic regionName (options : SpawnOption list) = 
        let expr = <@ fun () -> new ResActor<'TAdminCommand, 'TBusinessCommand, 'TState, 'TEvent>(eventSourcingLogic) @>
        let props = Props.Create (Linq.Expression.ToExpression(expr))
        let appliedProps = applySpawnOptions props options
        sharding.Start(regionName, appliedProps, ClusterShardingSettings.Create(system), messageExtractor)
    member this.StartPersisted<'Message, 'TAdminCommand, 'TBusinessCommand, 'TState, 'TEvent> eventSourcingLogic name (options : SpawnOption list) = 
        let expr = <@ fun () -> new ResActor<'TAdminCommand, 'TBusinessCommand, 'TState, 'TEvent>(eventSourcingLogic) @>
        let props = Props.Create (Linq.Expression.ToExpression(expr))
        let appliedProps = applySpawnOptions props options
        system.ActorOf(appliedProps, name)
    interface IDisposable with
        member __.Dispose() = automaticCluster.Dispose() // TODO: Implement the pattern properly

 
type ShardedMessageExtractor() =
    interface IMessageExtractor with 
        member __.EntityId(message) = match message with
                                        | :? ShardedMessage as msg -> msg.EntityId
                                        | _ -> null
        member __.ShardId(message) = match message with
                                        | :? ShardedMessage as msg -> msg.ShardId
                                        | _ -> null
        member __.EntityMessage(message) = match message with
                                                | :? ShardedMessage as msg -> msg.Message :> Object
                                                | _ -> null

let simpleEventSourcingLogic = {
    UpdateState = (fun state e -> e::state)
    ProcessBusinessCommand = (fun cmd -> Some(sprintf "Received '%s'" (cmd.ToString())))
    ProcessAdminCommand = (fun ac -> None)
}


let produceMessages (system: ActorSystem) (shardRegion: IActorRef) =
    let entitiesCount = 20
    let shardsCount = 10
    let rand = new Random()

    system.Scheduler.Advanced.ScheduleRepeatedly(
        TimeSpan.FromSeconds(1.0), 
        TimeSpan.FromSeconds(0.001), 
        fun () ->
            for i = 0 to 1 do
                let shardId = rand.Next(shardsCount)
                let entityId = rand.Next(entitiesCount)

                shardRegion.Tell({ShardId = shardId.ToString(); EntityId = entityId.ToString(); Message = "hello world"})
    )

let runExample (system: ActorSystem) =
    let shardedSystem = new ShardedSystem (system, (fun s -> new AutomaticClusterSqlite(s) :> IAutomaticCluster))
    let messageExtractor = new ShardedMessageExtractor()
    //let shardRegion = shardedSystem.StartShardRegion messageExtractor "printer" <@ actorOf (fun msg -> printfn "Shard Received: %s\n" msg) @> []
    //let shardRegion = shardedSystem.StartShardRegion2 messageExtractor "printer" (Props.Create<ResActor>())

    let shardRegion = shardedSystem.StartShardRegion messageExtractor simpleEventSourcingLogic "printer" []

//////    // general update state method
//////    let update state e = 
//////        e::state
//////
//////    // apply is invoked when actor receives a recovery event
//////    let apply _ = update
//////
//////    // exec is invoked when a actor receives a new message from another entity
//////    let exec (mailbox: Eventsourced<_,ShardedMessage,_>) state cmd = 
//////        printfn "Cmd Received: %A\n" cmd
//////        mailbox.PersistEvent (update state) [cmd]
////////        match cmd with
////////        | "print" -> printf "State is: %A\n" state          // print current actor state
////////        | s       -> mailbox.PersistEvent (update state) [s]     // persist event and call update state on complete
//////
//////    let shardRegion = 
//////        spawnPersist system "s0" {  // s0 identifies actor uniquelly across different incarnations
//////            state = []              // initial state
//////            apply = apply           // recovering function
//////            exec = exec             // command handler
//////        } []  

    System.Threading.Thread.Sleep(2000)
    //Console.Write("Press ENTER to start producing messages...")
    //Console.ReadLine() |> ignore

    produceMessages system shardRegion