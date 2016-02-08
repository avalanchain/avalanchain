
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
    

type Message =
    | Subscribe
    | Unsubscribe
    | Msg of IActorRef * string

let subscriber =
    spawn system1 "subscriber"
        (actorOf2 (fun mailbox msg ->
            let eventStream = mailbox.Context.System.EventStream
            match msg with
            | Msg (sender, content) -> printfn "%A says %s" (sender.Path) content
            | Subscribe -> subscribe typeof<Message> mailbox.Self eventStream |> ignore
            | Unsubscribe -> unsubscribe typeof<Message> mailbox.Self eventStream |> ignore ))

let publisher =
    spawn system1 "publisher"
        (actorOf2 (fun mailbox msg ->
            publish msg mailbox.Context.System.EventStream))

subscriber <! Subscribe
// Wait 50 milliseconds for subscribe to complete (race condition between subscription and message to subscriber)
Async.Sleep 50 |> Async.RunSynchronously
publisher  <! Msg (publisher, "hello") // console output: "*publisher Path* says hello"
subscriber <! Unsubscribe
// Again wait 50 milliseconds for unsubscribe to complete



publisher  <! Msg (publisher, "hello again") // no output, subscriber is not subscribed to stream