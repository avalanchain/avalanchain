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

type DbHelper (connectionFactory: unit -> SQLiteConnection) =
    member this.InitializeNodesTable() =
        use cmd = new SQLiteCommand(connectionFactory())
        cmd.CommandText <- @"CREATE TABLE IF NOT EXISTS cluster_nodes (
            member_address VARCHAR(255) NOT NULL PRIMARY KEY
        );"
        cmd.ExecuteNonQuery() |> ignore // TODO: Add error handling

    member this.GetClusterMembers() = // TODO: Replace with a TypeProvider
        let mutable result = []
        use cmd = new SQLiteCommand(@"SELECT member_address from cluster_nodes", connectionFactory())
        use reader = cmd.ExecuteReader()
        while reader.Read() do
            result <- (Address.Parse (reader.GetString(0))) :: result
        List.rev result

    member this.AddClusterMember(address: Address) =
        use cmd = new SQLiteCommand(@"INSERT INTO cluster_nodes(member_address) VALUES (@addr)", connectionFactory())
        use tx = cmd.Connection.BeginTransaction()
        cmd.Transaction <- tx
        let addr = address.ToString()
        cmd.Parameters.Add("@addr", DbType.String).Value <- addr

        cmd.ExecuteNonQuery() |> ignore // TODO: Add error handling
        tx.Commit()

    member this.RemoveClusterMember(address: Address) =
        use cmd = new SQLiteCommand(@"DELETE FROM cluster_nodes WHERE member_address = @addr", connectionFactory())
        use tx = cmd.Connection.BeginTransaction()
        cmd.Transaction <- tx;
        let addr = address.ToString()
        cmd.Parameters.Add("@addr", DbType.String).Value <- addr

        cmd.ExecuteNonQuery() |> ignore // TODO: Add error handling
        tx.Commit()


type AutomaticCluster (system) =
    let cluster = Cluster.Get(system);
    let persistence = SqlitePersistence.Get(system);
    let dbHelper = new DbHelper(
                    fun () ->
                        let conn = new SQLiteConnection(persistence.JournalSettings.ConnectionString)
                        conn.Open()
                        conn
                    )
    member this.Join() =
        dbHelper.InitializeNodesTable()

        let members = dbHelper.GetClusterMembers()
        match members with
        | [] -> 
            let self = cluster.SelfAddress
            dbHelper.AddClusterMember(self)
            cluster.JoinSeedNodes(ImmutableList.Create(self))
        | _ ->
            cluster.JoinSeedNodes(ImmutableList.ToImmutableList(members))
            dbHelper.AddClusterMember(cluster.SelfAddress)
            

    member this.Leave() =
        dbHelper.RemoveClusterMember(cluster.SelfAddress)


type ResActor<'TAdminCommand, 'TBusinessCommand, 'TState, 'TEvent>
    (
        updateState: 'TState -> 'TEvent -> 'TState, 
        processBusinessCommand: 'TBusinessCommand -> 'TEvent option, // TODO: Add Chessie error reporting
        processAdminCommand: 'TAdminCommand -> 'TEvent option // TODO: Add Chessie error reporting
    ) as self = 
    inherit PersistentActor()
    let mutable state = Unchecked.defaultof<'TState>
    do (UntypedActor.Context.SetReceiveTimeout(Nullable(TimeSpan.FromMinutes(2.0))))
    member private __.Self = base.Self
    member private __.Context = UntypedActor.Context
    override __.PersistenceId with get() = (sprintf "Actor %s-%s" (self.Context.Parent.Path.Name) self.Self.Path.Name)
    override __.ReceiveRecover(msg: obj) = 
        match msg with 
        | :? 'TEvent as e -> 
            state <- updateState state e
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
            match processBusinessCommand c with 
            | Some e -> this.Persist(e, (fun ee -> (state <- updateState state ee) |> ignore))
                        true
            | None -> false
        | :? 'TAdminCommand as c -> 
            match processAdminCommand c with 
            | Some e -> this.Persist(e, (fun ee -> (state <- updateState state ee) |> ignore))
                        true
            | None -> false
        | _ -> false

type ShardedSystem (system) =
    let automaticCluster = new AutomaticCluster(system)
    let sharding = ClusterSharding.Get(system)
    do automaticCluster.Join()
    member this.System = system
    member this.StartShardRegion<'Message, 'Returned> messageExtractor regionName (expr: Expr<Actor<'Message> -> Cont<'Message, 'Returned>>) (options : SpawnOption list)  = 
        let e = Linq.Expression.ToExpression(fun () -> new FunActor<'Message, 'Returned>(expr))
        let props = applySpawnOptions (Props.Create e) options
        sharding.Start(regionName, props, ClusterShardingSettings.Create(system), messageExtractor)
    member this.StartShardRegion2<'Message, 'Actor when 'Actor :> ActorBase> messageExtractor regionName props = 
        sharding.Start(regionName, props, ClusterShardingSettings.Create(system), messageExtractor)
    member this.StartShardRegion3<'Message, 'TAdminCommand, 'TBusinessCommand, 'TState, 'TEvent> messageExtractor regionName updateState processBusinessCommand processAdminCommand (options : SpawnOption list) = 
        //let props = Props.Create<ResActor<'TAdminCommand, 'TBusinessCommand, 'TState, 'TEvent>>([|updateState, processBusinessCommand, processAdminCommand|])
        let expr = <@ fun () -> new ResActor<'TAdminCommand, 'TBusinessCommand, 'TState, 'TEvent>(updateState, processBusinessCommand, processAdminCommand) @>
        let props = Props.Create (Linq.Expression.ToExpression(expr))
        let appliedProps = applySpawnOptions props options
        sharding.Start(regionName, appliedProps, ClusterShardingSettings.Create(system), messageExtractor)
    interface IDisposable with
        member this.Dispose() = automaticCluster.Leave() // TODO: Implement the pattern properly



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
    let shardedSystem = new ShardedSystem (system)
    let sharding = ClusterSharding.Get(system)
    let messageExtractor = 
        {new IMessageExtractor with 
            member __.EntityId(message) = match message with
                                            | :? ShardedMessage as msg -> msg.EntityId
                                            | _ -> null
            member __.ShardId(message) = match message with
                                            | :? ShardedMessage as msg -> msg.ShardId
                                            | _ -> null
            member __.EntityMessage(message) = match message with
                                                    | :? ShardedMessage as msg -> msg.Message :> Object
                                                    | _ -> null}
    //let shardRegion = shardedSystem.StartShardRegion messageExtractor "printer" <@ actorOf (fun msg -> printfn "Shard Received: %s\n" msg) @> []
    //let shardRegion = shardedSystem.StartShardRegion2 messageExtractor "printer" (Props.Create<ResActor>())

    let shardRegion = shardedSystem.StartShardRegion3 messageExtractor "printer" 
                        (fun state e -> e::state) (fun cmd -> Some(sprintf "Received '%s'" (cmd.ToString()))) (fun ac -> None) []

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