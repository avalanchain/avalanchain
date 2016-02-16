// Define your library scripting code here

#time "on"
#load "Bootstrap.fsx"


#load "Messages.fs"
#load "AutomaticCluster.fs"
#load "Actors.fs"
#load "Extension.fs"
#load "Node.fs"
#load "Sharded.fs"
//#load "SqliteCluster.fs"

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
                auto-down-unreachable-after = 5s
                seed-nodes = [ "akka.tcp://cluster-system@localhost:4053/" ]
              }
              persistence {
                journal {
                  # Path to the journal plugin to be used
                  plugin = "akka.persistence.journal.inmem"

                  # In-memory journal plugin.
                  inmem {

                    # Class name of the plugin.
                    class = "Akka.Persistence.Journal.MemoryJournal, Akka.Persistence"
                  }
                }
                snapshot-store {
                  plugin = "akka.persistence.journal.inmem"

                  # In-memory journal plugin.
                  inmem {

                    # Class name of the plugin.
                    class = "Akka.Persistence.Journal.MemoryJournal, Akka.Persistence"
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

Avalanchain.Cluster.Sharded.runExample system
