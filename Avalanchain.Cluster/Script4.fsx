
// Define your library scripting code here

#time "on"
#load "Bootstrap.fsx"

open System
open Akka.FSharp
open Akka.Actor
open Akka.Cluster
//open Akkling
//open Akkling.Cluster
//open Akkling.Cluster.Sharding

open Avalanchain.Cluster

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
let system1 = System.create "cluster-system" (configWithPort 5000)
//let actor1 = spawn system1 "printer1" <| props (Behaviors.printf "SS1 Received: %s\n")
let actor1 = spawn system1 "printer1" <| (actorOf (fun msg -> printfn "SS1 Received: %s\n" msg))

// second cluster system with sharding region up and ready
let system2 = System.create "cluster-system" (configWithPort 5001)
//let actor2 = spawn system2 "printer2" <| props (Behaviors.printf "SS2 Received: %s\n")
let actor2 = spawn system2 "printer2" <| (actorOf (fun msg -> printfn "SS2 Received: %s\n" msg))

let remoteNodeAddr = Address.Parse "akka.tcp://cluster-system@localhost:5001/"
let actor3 = spawne system1 "printer3" <@ actorOf (fun msg -> printfn "SS3 Received: %s\n" msg) @>
                [ 
                    //SpawnOption.SupervisorStrategy (Strategy.OneForOne <@ fun error ->
                    //match error with
                    //| :? ArithmeticException -> Directive.Escalate
                    //| _ -> SupervisorStrategy.DefaultDecider error @>)
                  SpawnOption.Deploy (Deploy (RemoteScope remoteNodeAddr)) ]



// shard region will distribute messages to entities in corresponding shards
let c = 3
for i = 0 to c do actor1 <! sprintf "hello world %d" i
for i = 0 to c do actor2 <! sprintf "hello world %d" i
for i = 0 to c do actor3 <! sprintf "hello world %d" i

let actor4 = select "akka://cluster-system/user/printer3" system1
for i = 0 to c do actor4 <! sprintf "hello world %d" i

let actor5 = select "akka://cluster-system/user/printer3" system2
for i = 0 to c do actor4 <! sprintf "hello world %d" i

