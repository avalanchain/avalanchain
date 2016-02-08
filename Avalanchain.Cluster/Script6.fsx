
// Define your library scripting code here

#time "on"
#I @"bin/Debug"

#r "Akka.dll"
#r "Akka.FSharp.dll"
#r "Akka.Remote.dll"
#r "Akka.Persistence.dll"
#r "Akka.Persistence.Sql.Common.dll"
#r "Akka.Persistence.Sqlite.dll"
#r "Akka.TestKit.dll"
#r "Akka.Cluster.dll"
#r "Akka.Cluster.Tools.dll"
#r "Akkling.dll"
#r "Akkling.Persistence.dll"
#r "Akkling.Cluster.Sharding.dll"
#r "Wire.dll"
#r "System.Data.SQLite.dll"
#r "Newtonsoft.Json.dll"
#r "FSharp.PowerPack.dll"
#r "FSharp.PowerPack.Linq.dll"
#r "Helios.dll"
#r "FsPickler.dll"
#r "Google.ProtocolBuffers.dll"
#r "Google.ProtocolBuffers.Serialization.dll"

#r "Avalanchain.dll"

open System
open Akka.FSharp
open Akka.Actor
open Akka.Cluster
//open Akkling
//open Akkling.Cluster
//open Akkling.Cluster.Sharding

//open Avalanchain.Cluster
open Avalanchain.Quorum

let rnd = new Random()
let hasher ep = 
    let bytes = Array.zeroCreate 10
    rnd.NextBytes bytes
    bytes


//let egs = [ExecutionGroup "EG1"; ExecutionGroup "EG2"; ExecutionGroup "EG3"]
//            |> List.map(fun (ExecutionGroup eg) -> Pool { Name = eg; MaxPerNode = 1u; MaxTotal = 1000u; MinToOperate = 2u })

let ep = ExecutionPolicy.All (set [
                                ExecutionPolicy.None
                                ExecutionPolicy.One(Random, FixedCount 2u)
                                ExecutionPolicy.One(Random, Percentage (0.5, 2u))
                                ExecutionPolicy.One(Mandatory (set[ExecutionGroup "EG4"]), FixedCount 2u)
                                ExecutionPolicy.One(Mandatory (set[ExecutionGroup "EG5"]), FixedCount 2u)
                                ])

let join (strs: string seq) = String.Join(",", strs)
let join2 (strs: string seq) = String.Join("", strs)

let routers = ep |> processExecutionPolicy egs |> List.rev
let rolesStr = "[" + join (routers |> List.map (fun r -> sprintf @"""%s""" r.Name)|> Array.ofList) + "]"

let buildHaconSection (rs: RouterSetting) = 
    let section = """
                /user/{0}Router {{
                  router = broadcast-group # routing strategy
                  routees.paths = ["/user/egs/{0}"] # path of routee on each node
                  nr-of-instances = {1} # max number of total routees
                  cluster {{
                     enabled = on
                     allow-local-routees = on
                     use-role = "{0}"
                     max-nr-of-instances-per-node = {2}
                  }}
                }}
            """
    String.Format(section, rs.Name, rs.MaxTotal, rs.MaxPerNode)
    
let haconSection = 
    routers 
    //|> List.take 2
    |> List.map (function 
                    | Nothing _ -> ""
                    | Group rs -> buildHaconSection rs
                    | Pool rs  -> buildHaconSection rs) 
    |> List.filter (String.IsNullOrWhiteSpace >> not)
    |> join2

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
            roles = """ + rolesStr + """
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
let actor1 = spawn system1 "printer1" <| (actorOf (fun msg -> printfn "SS1 Received: %s\n" msg))

// second cluster system with sharding region up and ready
let system2 = System.create "cluster-system" (configWithPort 5001)
let actor2 = spawn system2 "printer1" <| (actorOf (fun msg -> printfn "SS2 Received: %s\n" msg))

//let remoteNodeAddr = Address.Parse "akka.tcp://cluster-system@localhost:5001/"
//let actor3 = spawne system1 "printer3" <@ actorOf (fun msg -> printfn "SS3 Received: %s\n" msg) @>
//                [ 
//                   ]
//
//let actor13 = spawne system1 "printer13" <@ actorOf (fun msg -> printfn "SS13 Received: %s\n" msg) @>
//                [ 
//                  SpawnOption.Router (new Akka.Routing.RoundRobinGroup("/user/printer1", "/user/printer2", "/user/printer3"))]
//                  //SpawnOption.Router (new Akka.Routing.RoundRobinGroup([actor1; actor2; actor3]))]
//
let actor14 = spawne system2 "printer14" <@ actorOf2 (fun ctx msg -> printfn "%A Received: %s\n" ctx.Self.Path msg) @>
                [ 
                    SpawnOption.Deploy (Deploy (ClusterScope.Instance))
                    SpawnOption.Router (
                        new Akka.Cluster.Routing.ClusterRouterPool(
                            new Akka.Routing.BroadcastPool(8),
                            new Akka.Cluster.Routing.ClusterRouterPoolSettings(4, true, 2)))
                ]
//
//
//// shard region will distribute messages to entities in corresponding shards

//let actor3 = spawne system1 "EG5Router" <@ actorOf (fun msg -> printfn "SS3 Received: %s\n" msg) @> []

let actor5 = select "akka://cluster-system/user/printer14" system1

let c = 3
for i = 0 to c do actor1 <! sprintf "hello world %d" i
//for i = 0 to c do actor2 <! sprintf "hello world %d" i
//for i = 0 to c do actor5 <! sprintf "hello world %d" i
//for i = 0 to c do actor3 <! sprintf "hello world %d" i
//
//for i = 0 to c do actor13 <! sprintf "hello world %d" i
for i = 0 to c do actor14 <! sprintf "hello world %d" i
//



