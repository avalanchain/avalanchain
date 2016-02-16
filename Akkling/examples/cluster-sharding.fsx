#I "../src/Akkling.Cluster.Sharding/bin/Debug"
#r "Akka.dll"
#r "Wire.dll"
#r "Newtonsoft.Json.dll"
#r "FSharp.PowerPack.dll"
#r "FSharp.PowerPack.Linq.dll"
#r "Akkling.dll"
#r "Helios.dll"
#r "FsPickler.dll"
#r "Google.ProtocolBuffers.dll"
#r "Google.ProtocolBuffers.Serialization.dll"
#r "Akka.Remote.dll"
#r "System.Data.SQLite.dll"

#r "Akka.Persistence.Sqlite.dll"
#r "Akka.Persistence.Sql.Common.dll"
#r "Akka.Persistence.dll"
#r "Akka.Persistence.FSharp.dll"


#r "Akkling.Persistence.dll"
#r "Akka.Cluster.dll"
#r "Akka.Cluster.Sharding.dll"
#r "Akka.Cluster.Tools.dll"
#r "Akkling.Cluster.Sharding.dll"

open System
open Akka.Actor
open Akka.Persistence
open Akka.Cluster
open Akka.Cluster.Sharding
open Akkling
open Akkling.Cluster
open Akkling.Cluster.Sharding
open Akka.Persistence.FSharp

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
              port = """ + port.ToString() + """
            }
          }
          cluster {
            auto-down-unreachable-after = 5s
            seed-nodes = [ "akka.tcp://cluster-system@localhost:5000/" ]
          }
          persistence {
            journal {
              # Path to the journal plugin to be used
              plugin = "akka.persistence.journal.inmem"

              # In-memory journal plugin.
              inmem {

                # Class name of the plugin.
                class = "Akka.Persistence.Journal.MemoryJournal, Akka.Persistence"

                # Dispatcher for the plugin actor.
                plugin-dispatcher = "akka.actor.default-dispatcher"
              }
            }
            snapshot-store {
              plugin = "akka.persistence.journal.inmem"

              # In-memory journal plugin.
              inmem {

                # Class name of the plugin.
                class = "Akka.Persistence.Journal.MemoryJournal, Akka.Persistence"

                # Dispatcher for the plugin actor.
                plugin-dispatcher = "akka.actor.default-dispatcher"
              }
            }
          }
        }
        """)
    config.WithFallback(Tools.Singleton.ClusterSingletonManager.DefaultConfig())
    
// first cluster system with sharding region up and ready
let system1 = System.create "cluster-system" (configWithPort 5000)
let shardRegion1 = spawnSharded id system1 "printer" <| props (Behaviors.printf "Received: %s\n")

// second cluster system with sharding region up and ready
let system2 = System.create "cluster-system" (configWithPort 5001)
let shardRegion2 = spawnSharded id system2 "printer" <| props (Behaviors.printf "Received: %s\n")

// shard region will distribute messages to entities in corresponding shards
shardRegion1 <! ("shard-1", "entity-1", "hello world 1")
shardRegion1 <! ("shard-2", "entity-1", "hello world 2")
shardRegion1 <! ("shard-3", "entity-1", "hello world 3")
shardRegion1 <! ("shard-4", "entity-1", "hello world 4")