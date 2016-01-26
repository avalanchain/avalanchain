
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
open Avalanchain.Quorum

let configWithPort port = 
    let config = Configuration.parse("""
        akka {
          actor {
            provider = "Akka.Cluster.ClusterActorRefProvider, Akka.Cluster"
            deployment {
                /api/myClusterGroupRouter {
                  router = broadcast-group # routing strategy
                  routees.paths = ["/user/api"] # path of routee on each node
                  nr-of-instances = 3 # max number of total routees
                  cluster {
                     enabled = on
                     allow-local-routees = on
                     use-role = "testrole"
                  }
                }
              }
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
            roles = ["testrole"]
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
let actor2 = spawn system2 "printer1" <| (actorOf (fun msg -> printfn "SS2 Received: %s\n" msg))

let remoteNodeAddr = Address.Parse "akka.tcp://cluster-system@localhost:5001/"
let actor3 = spawne system1 "printer3" <@ actorOf (fun msg -> printfn "SS3 Received: %s\n" msg) @>
                [ 
                  SpawnOption.Deploy (Deploy (RemoteScope remoteNodeAddr)) ]

let actor13 = spawne system1 "printer13" <@ actorOf (fun msg -> printfn "SS13 Received: %s\n" msg) @>
                [ 
                  SpawnOption.Router (new Akka.Routing.RoundRobinGroup("/user/printer1", "/user/printer2", "/user/printer3"))]
                  //SpawnOption.Router (new Akka.Routing.RoundRobinGroup([actor1; actor2; actor3]))]

let actor14 = spawne system1 "printer14" <@ actorOf (fun msg -> printfn "SS13 Received: %s\n" msg) @>
                [ 
                  SpawnOption.Router (new Akka.Routing.BroadcastGroup("/user/printer1", "/user/printer2", "/user/printer3"))]


// shard region will distribute messages to entities in corresponding shards
let c = 3
for i = 0 to c do actor1 <! sprintf "hello world %d" i
for i = 0 to c do actor2 <! sprintf "hello world %d" i
for i = 0 to c do actor3 <! sprintf "hello world %d" i

for i = 0 to c do actor13 <! sprintf "hello world %d" i
for i = 0 to c do actor14 <! sprintf "hello world %d" i

let rnd = new Random()
let hasher ep = 
    let bytes = Array.zeroCreate 10
    rnd.NextBytes bytes
    bytes

let toString bytes = 
    System.Text.Encoding.ASCII.GetString bytes



let rec processExecutionPolicy (ep: ExecutionPolicy) =
    let id = ep |> hasher |> toString
    match ep with
    | None -> ()
    | All eps -> eps |> Set.iter processExecutionPolicy
    | One (strategy, stake) -> 
        match (strategy, stake) with
        | Random, Percentage p -> 
            let actor3 = spawne system1 id <@ actorOf (fun msg -> printfn "%s Received: %s\n" id msg) @> [ ]
            actor3 |> ignore
        | Random, FixedCount fc -> 
            let actor3 = spawne system1 id <@ actorOf (fun msg -> printfn "%s Received: %s\n" id msg) @> [ ]
            actor3 |> ignore
        | Mandatory egs, Percentage p -> 
            let actor3 = spawne system1 id <@ actorOf (fun msg -> printfn "%s Received: %s\n" id msg) @> [ ]
            actor3 |> ignore
        | Mandatory egs, FixedCount fc -> 
            let actor3 = spawne system1 id <@ actorOf (fun msg -> printfn "%s Received: %s\n" id msg) @> [ ]
            actor3 |> ignore


