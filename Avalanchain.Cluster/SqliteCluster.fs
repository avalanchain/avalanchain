module Avalanchain.Cluster.SQLite

open Akka.Actor
open Akka.Cluster
open Akka.Persistence.Sqlite
open Akka.Cluster.Sharding
open System
open System.Collections.Immutable
open System.Data
open System.Data.SQLite

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


type ShardedSystem (system) =
    let automaticCluster = new AutomaticCluster(system)
    let sharding = ClusterSharding.Get(system)
    let joined = automaticCluster.Join()
    member this.System = system
    member this.GetRegion regionName props messageExtractor = 
        sharding.Start(regionName, props, ClusterShardingSettings.Create(system), messageExtractor)
    interface IDisposable with
        member this.Dispose() = automaticCluster.Leave() // TODO: Implement pattern properly
    