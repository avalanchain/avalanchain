
// Define your library scripting code here

#time "on"
#load "Bootstrap.fsx"


open System
open System.Collections.Immutable
open Akka.FSharp
open Akka.Actor
open Akka.Cluster
//open Akkling
//open Akkling.Cluster
//open Akkling.Cluster.Sharding

open Akka.Persistence
open Akka.Persistence.FSharp
open Avalanchain.Quorum

#load "Messages.fs"
#load "SqliteCluster.fs"


let configWithPort port = 
    let config = Configuration.parse("""
        akka {
            actor {
              provider = "Akka.Cluster.ClusterActorRefProvider, Akka.Cluster"
            }
            remote {
              helios.tcp {
                public-hostname = "localhost"
                hostname = "localhost"
                port = 0
              }
            }
            cluster {
                auto-down-unreachable-after = 15s
              }
            persistence {
              journal {
                plugin = "akka.persistence.journal.sqlite"
                sqlite {
                  connection-string = "Data Source=.\\store.db;Version=3;"
                  auto-initialize = true
                }
              }
              snapshot-store {
                plugin = "akka.persistence.snapshot-store.sqlite"
                sqlite {
                  connection-string = "Data Source=.\\store.db;Version=3;"
                  auto-initialize = true
                }
              }
            }
          }
        """)
    config.WithFallback(Tools.Singleton.ClusterSingletonManager.DefaultConfig())
    


// first cluster system with sharding region up and ready
let system = System.create "sharded-cluster-system" (configWithPort 5000)
//let actor1 = spawn system1 "printer1" <| props (Behaviors.printf "SS1 Received: %s\n")
//let actor1 = spawn system1 "printer1" <| (actorOf (fun msg -> printfn "SS1 Received: %s\n" msg))
Avalanchain.Cluster.SQLite.runExample system

//// second cluster system with sharding region up and ready
//let system2 = System.create "cluster-system" (configWithPort 5001)
////let actor2 = spawn system2 "printer2" <| props (Behaviors.printf "SS2 Received: %s\n")
//let actor2 = spawn system2 "printer1" <| (actorOf (fun msg -> printfn "SS2 Received: %s\n" msg))
//
//let remoteNodeAddr = Address.Parse "akka.tcp://cluster-system@localhost:5001/"
//let actor3 = spawne system1 "printer3" <@ actorOf (fun msg -> printfn "SS3 Received: %s\n" msg) @>
//                [ 
//                  SpawnOption.Deploy (Deploy (RemoteScope remoteNodeAddr)) ]
//
//let actor13 = spawne system1 "printer13" <@ actorOf (fun msg -> printfn "SS13 Received: %s\n" msg) @>
//                [ 
//                  SpawnOption.Router (new Akka.Routing.RoundRobinGroup("/user/printer1", "/user/printer2", "/user/printer3"))]
//                  //SpawnOption.Router (new Akka.Routing.RoundRobinGroup([actor1; actor2; actor3]))]
//
//let actor14 = spawne system1 "printer14" <@ actorOf (fun msg -> printfn "SS13 Received: %s\n" msg) @>
//                [ 
//                  SpawnOption.Router (new Akka.Routing.BroadcastGroup("/user/printer1", "/user/printer2", "/user/printer3"))]
//
//
//// shard region will distribute messages to entities in corresponding shards
//let c = 3
//for i = 0 to c do actor1 <! sprintf "hello world %d" i
//for i = 0 to c do actor2 <! sprintf "hello world %d" i
//for i = 0 to c do actor3 <! sprintf "hello world %d" i
//
//for i = 0 to c do actor13 <! sprintf "hello world %d" i
//for i = 0 to c do actor14 <! sprintf "hello world %d" i
//
//
//
