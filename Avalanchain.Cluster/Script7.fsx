
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
#load "Sharded.fs"


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
    //let shardRegion = shardedSystem.StartShardRegion messageExtractor "printer" <@ actorOf (fun msg -> printfn "Shard Received: %s\n" msg) @> []
    //let shardRegion = shardedSystem.StartShardRegion2 messageExtractor "printer" (Props.Create<ResActor>())


    let shardRegion = shardedSystem.StartShardRegion ("printer", [])

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

// first cluster system with sharding region up and ready
let system = System.create "sharded-cluster-system" (configWithPort 5000)
//let actor1 = spawn system1 "printer1" <| props (Behaviors.printf "SS1 Received: %s\n")
//let actor1 = spawn system1 "printer1" <| (actorOf (fun msg -> printfn "SS1 Received: %s\n" msg))

// second cluster system with sharding region up and ready
let system2 = System.create "sharded-cluster-system" (configWithPort 5001)

// third cluster system with sharding region up and ready
let system3 = System.create "sharded-cluster-system" (configWithPort 5002)


Avalanchain.Cluster.SQLite.runExample system
Avalanchain.Cluster.SQLite.runExample system2
Avalanchain.Cluster.SQLite.runExample system3


//let actor2 = spawn system2 "printer2" <| props (Behaviors.printf "SS2 Received: %s\n")
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
