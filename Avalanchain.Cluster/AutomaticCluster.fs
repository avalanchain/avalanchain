module Avalanchain.Cluster.AutomaticCluster

open Akka.Actor
open Akka.Cluster
open Akka.Persistence.Sqlite
open Akka.Persistence.Sqlite.Journal
open System
open FSharp.Core
open System.Collections.Immutable
open System.Data
open System.Data.SQLite


[<Interface>]
type IAutomaticCluster = 
    inherit IDisposable

type AutomaticClusterSqlite (system) =
    let cluster = Cluster.Get(system);
    let persistence = SqlitePersistence.Get(system);
    let connectionFactory() = 
        let conn = new SQLiteConnection(persistence.DefaultJournalConfig.GetString("connection-string"))
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