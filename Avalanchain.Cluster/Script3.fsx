

// Define your library scripting code here

#time "on"
#load "Bootstrap.fsx"

open System
open Akka.Actor
open Akka.Configuration
open Akka.FSharp
open Akka.TestKit

//let system = System.create "system" <| Configuration.parse """
//    akka {
//        loglevel = DEBUG
//        actor {
//            serialization-bidnings {
//                "System.Object" = wire
//            }
//        }
//        debug {
//          receive = on
//          autoreceive = on
//          lifecycle = on
//          fsm = on
//          event-stream = on
//          unhandled = on
//          router-misconfiguration = on
//        }
//    }
//"""

open System
open Akkling
open Akkling.Cluster
open Akkling.Cluster.Sharding
open Akka.Actor
open Akka.Cluster

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
            roles = ["testrole"]
          }
          deployment {
            /api/myRouter {
              router = round-robin-pool # routing strategy
              nr-of-instances = 10 # max number of total routees
              cluster {
                 enabled = on
                 allow-local-routees = on
                 use-role = crawler
                 max-nr-of-instances-per-node = 1
              }
            }
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
    
let printf (fmt: Printf.TextWriterFormat<'Message->unit>) (context: Actor<'Message>) : Behavior<'Message> =
    let rec loop () = actor {
        let! msg = context.Receive()
        printf fmt msg
        //let! a = for i = 0 to 10000000 do (i + i)
        //a |> ignore
        return! loop ()    
    }
    loop ()

//let spawnRemote addr sys name actor = 
//    let options = [SpawnOption.Deploy(Deploy(RemoteScope(Address.Parse addr)))]
//    spawne options sys name actor

// first cluster system with sharding region up and ready
let system1 = System.create "cluster-system" (configWithPort 5000)
let actor1 = spawn system1 "printer" <| props (printf "Received SS1: %s\n")
//let actor3 = spawne system1 "printer" <@ actorOf2 (fun ctx msg -> printfn "%A received: %s" ctx.Self msg) @>
//                [SpawnOption.Deploy (Deploy(RemoteScope(Address.Parse "akka.tcp://cluster-system@127.0.0.1:5001/")))]
//let actor3 = 
//    spawnRemote "akka.tcp://cluster-system@127.0.0.1:5001/" system1 "printer" 
//        <@ actorOf2 (fun ctx msg -> printfn "%A received: %s" ctx.Self msg) @>

let remoteProps addr actor = { propse actor with Deploy = Some (Deploy(RemoteScope(Address.Parse addr))) }
//let actor3 = spawn system1 "cluster-system1" (remoteProps "akka.tcp://cluster-system@127.0.0.1:5000/api/myRouter" <@ actorOf2 (fun ctx msg -> printfn "%A received: %s" ctx.Self msg |> ignored) @>)

//let actor3 =
//    spawne system1 "remote-actor" <@ actorOf2 handleMessage @>
//        [   SpawnOption.SupervisorStrategy (
//                Strategy.OneForOne 
//                    <@ (fun error ->
//                        match error with
//                        | :? ArithmeticException -> Directive.Escalate
//                        | _ -> SupervisorStrategy.DefaultDecider error ) @>)
//            SpawnOption.Deploy (Deploy (RemoteScope remoteNodeAddr)) ]

// second cluster system with sharding region up and ready
let system2 = System.create "cluster-system" (configWithPort 5001)
let actor2 = spawn system2 "printer" <| props (printf "Received SS2: %s\n")
//let actor4 = spawne system2 "printer" <@ actorOf (printf "Received SS1: %s\n") @>
//                [SpawnOption.Deploy (Deploy(RemoteScope(Address.Parse "akka.tcp://cluster-system@127.0.0.1:5000/")))]



let c = 3
let _ = for i = 0 to c do actor1 <! sprintf "hello world %d" i
let _ = for i = 0 to c do actor2 <! sprintf "hello world %d" i
//let _ = for i = 0 to c do actor3 <! sprintf "hello world %d" i
//let _ = for i = 0 to c do actor4 <! sprintf "hello world %d" i

//let sref = select "akka://local-system/user/printer" system2
//sref <! "Hello again"


let aref =  
    spawn system2 "listener"
    <| fun mailbox ->
        // subscribe for cluster events at actor start 
        // and usubscribe from them when actor stops
        let cluster = Cluster.Get (mailbox.Context.System)
        cluster.Subscribe (mailbox.Self, [| typeof<ClusterEvent.IMemberEvent> |])
        mailbox.Defer <| fun () -> cluster.Unsubscribe (mailbox.Self)
        printfn "Created an actor on node [%A] with roles [%s]" cluster.SelfAddress (String.Join(",", cluster.SelfRoles))
        let rec seed () = 
            actor {
                let! (msg: obj) = mailbox.Receive ()
                match msg with
                | :? ClusterEvent.IMemberEvent -> printfn "Cluster event %A" msg
                | _ -> printfn "Received: %A" msg
                return! seed () }
        seed ()