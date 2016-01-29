
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
              serializers {
                wire = "Akka.Serialization.WireSerializer, Akka.Serialization.Wire"
              }
              serialization-bindings {
                "System.Object" = wire
              }
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
    
let produceMessages (system: ActorSystem) (shardRegion: IActorRef)
    let[<Literal>] entitiesCount = 20
    let[<Literal>] shardsCount = 10
    let rand = new Random();

    system.Scheduler.Advanced.ScheduleRepeatedly(TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(5), 
        fun () ->
            for i = 0 to 1 do
                let shardId = rand.Next(shardsCount)
                let entityId = rand.Next(entitiesCount)

                shardRegion.Tell({shardId.ToString(); entityId.ToString(); "hello world"})
    )

let runExample (system: ActorSystem)
{
    let sharding = ClusterSharding.Get(system)
    let shardRegion = sharding.Start(
        typeName: "printer",
        entityProps: Props.Create<Printer>(),
        settings: ClusterShardingSettings.Create(system),
        messageExtractor: new MessageExtractor());

    Thread.Sleep(5000);
    Console.Write("Press ENTER to start producing messages...");
    Console.ReadLine();

    ProduceMessages(system, shardRegion);

// first cluster system with sharding region up and ready
let system1 = System.create "sharded-cluster-system" (configWithPort 5000)
//let actor1 = spawn system1 "printer1" <| props (Behaviors.printf "SS1 Received: %s\n")
let actor1 = spawn system1 "printer1" <| (actorOf (fun msg -> printfn "SS1 Received: %s\n" msg))

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
