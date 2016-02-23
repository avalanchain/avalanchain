// Define your library scripting code here

#time "on"
#load "Bootstrap.fsx"


#load "Messages.fs"
#load "AutomaticCluster.fs"
#load "Actors.fs"
#load "Node.fs"
#load "Extension.fs"
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
open Avalanchain.Cluster.AutomaticCluster
open Avalanchain.Cluster.Extension
open Avalanchain.Cluster.Sharded




let configWithPort port = 
    let config = Configuration.parse("""
        akka {
            loglevel = DEBUG
            actor {
              provider = "Akka.Cluster.ClusterActorRefProvider, Akka.Cluster"
              debug {
                lifecycle = on
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
                seed-nodes = [ "akka.tcp://cluster-system@localhost:5000/" ]
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
                //printfn "Message# - %d" i
                //shardRegion.Tell({ShardId = shardId.ToString(); EntityId = entityId.ToString(); Message = "hello world"})
                shardRegion.Tell("hello world")
    )

type Command =
    | Update of string      // update actor's internal state
    | TakeSnapshot          // order actor to save snapshot of it's current state
    | Print                 // print actor's internal state
    | Crash                 // order actor to blow up itself with exception
    | Query

let runExample (system: ActorSystem) =
    //let shardedSystem = new ShardedSystem (system, (fun s -> new AutomaticClusterSqlite(s) :> IAutomaticCluster))
    let shardedSystem = new ShardedSystem (system, (fun s -> {new IAutomaticCluster 
                                                                interface IDisposable with member __.Dispose() = ()}))

//    let kvRef = shardedSystem.StartKeyValue<double, double, double, double * double, string>("aa", []) 
//
    let update state e = (e.ToString())::state
    
    let apply (mailbox: Eventsourced<Command,obj,string list>) state (event:obj) = 
        printfn "Cmd is: %A" event
        match event with
        | :? string as e -> update state e
        | :? SnapshotOffer as o -> o.Snapshot :?> string list
        | x -> 
            mailbox.Unhandled x
            state

    let exec (mailbox: Eventsourced<Command,obj,string list>) state cmd =
        printfn "Cmd is: %A" cmd
        match cmd with
        | Update s -> mailbox.PersistEvent (update state) [s]
        | TakeSnapshot -> mailbox.SaveSnapshot state
        | Print -> printf "State is: %A\n" state
        | Crash -> failwith "planned crash"
        | Query -> mailbox.Sender() <! List.head state

    let s1 = 
            spawnPersist system "s10" {
                state = []
                apply = apply
                exec = exec
            } []

    s1 <! Command.Update "aaa"
    s1 <! Update "a"
    s1 <! Print
    s1 <! Crash
    s1 <! Print
    s1 <! Update "b"
    s1 <! Print
    s1 <! TakeSnapshot
    s1 <! Update "c"
    s1 <! Print

    let ret() = async { 
        let! msg = s1 <? Query
        printfn "Remote actor responded: %A" msg
        return! msg
    } 
//    
//    let retRes = ret() |> Async.RunSynchronously
//
//    Console.WriteLine(retRes.ToString())


    //System.Threading.Thread.Sleep(2000)
    //Console.Write("Press ENTER to start producing messages...")
    //Console.ReadLine() |> ignore

    //produceMessages system kvRef

    let s2 = spawn system "s2" <| actorOf (fun msg -> printfn "Remote actor responded: %A" msg)

    s2 <! "Hello"

    let s4 = spawne system "s4" <@ actorOf (fun msg -> printfn "Remote actor responded: %A" msg)@> []

    s4 <! "Hello"


// first cluster system with sharding region up and ready
let system = System.create "sharded-cluster-system" (configWithPort 5000)
//let actor1 = spawn system1 "printer1" <| props (Behaviors.printf "SS1 Received: %s\n")
//let actor1 = spawn system1 "printer1" <| (actorOf (fun msg -> printfn "SS1 Received: %s\n" msg))

//runExample system
// kvRef <! Avalanchain.Cluster.Actors.KeyValue.NewValue(0.1);;
