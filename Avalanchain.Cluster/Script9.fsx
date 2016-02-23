// Define your library scripting code here

#time "on"
#r "../packages/Akka.1.0.7.154-beta/lib/net45/Akka.dll"
#r "../packages/Akka.Cluster.1.0.7.154-beta/lib/net45/Akka.Cluster.dll"
#r "../packages/Akka.Persistence.1.0.7.154-beta/lib/net45/Akka.Persistence.dll"
#r "../packages/Akka.Persistence.FSharp.1.0.7.154-beta/lib/net45/Akka.Persistence.FSharp.dll"
#r "../packages/Akka.Persistence.Sql.Common.1.0.7.154-beta/lib/net45/Akka.Persistence.Sql.Common.dll"
#r "../packages/Akka.Persistence.Sqlite.1.0.7.154-beta/lib/net45/Akka.Persistence.Sqlite.dll"
#r "../packages/Akka.Cluster.Sharding.1.0.7.154-beta/lib/net45/Akka.Cluster.Sharding.dll"
#r "../packages/Akka.Cluster.Tools.1.0.7.154-beta/lib/net45/Akka.Cluster.Tools.dll"
#r "../packages/FSharp.Core.4.0.0.1/lib/net40/FSharp.Core.dll"
#r "../packages/Akka.FSharp.1.0.7.154-beta/lib/net45/Akka.FSharp.dll"
#r "../packages/System.Collections.Immutable.1.1.37/lib/dotnet/System.Collections.Immutable.dll"
#r "../packages/System.Data.SQLite.Core.1.0.99.0/lib/net451/System.Data.SQLite.dll"
#r "../packages/Chessie.0.4.0/lib/net40/Chessie.dll"
#r "../packages/Base58Check.0.2.0/lib/Net40/Base58Check.dll"


#r "bin/Debug/Avalanchain.dll"


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

open Akka.Persistence
open Akka.Persistence.FSharp
open Avalanchain.Quorum
open Avalanchain.Cluster.AutomaticCluster
open Avalanchain.Cluster.Extension
open Avalanchain.Cluster.Sharded
open Avalanchain.RefsAndPathes
open Avalanchain.SecPrimitives
open Avalanchain.SecKeys
open Avalanchain.StreamEvent
open Avalanchain.EventStream



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
    
   

type NodeCommand<'TT> = 
    | Post of Transaction<'TT>
    | Admin of NodeAdminCommand
    | Confirmation of ConfirmationPacket
and Transaction<'TT> = Transaction of 'TT
and NodeAdminCommand =
    | AddNode of NodeRef
    | RemoveNode of NodeRef
    //| AllNodes // expects Set<NodeRef>
and StreamAdminCommand<'TS, 'TD when 'TS: equality and 'TD: equality> =
    | AddStream of Hashed<EventStreamDef<'TS, 'TD>>
and ConfirmationPacket = {
    StreamRef: Hashed<EventStreamRef>
    EventHash: Hash
    StateHash: Hash
    NodeProof: Proof // eventHash*stateHash signed
}
 
type NodeChildActors = {
    CommandLog: string
    NodeRefStore: string 
}

let childActors = {
    CommandLog = "command-log"
    NodeRefStore = "node-ref-store"
}

module CommandLog =
    type CommandWrapper<'TT> = {
        Command: NodeCommand<'TT>
        TimeStamp: DateTimeOffset
    }

    type NodeEvent<'TT> = 
        | Command of CommandWrapper<'TT>
        | SnapshotOffer of SnapshotOffer

    type Aggregate<'TT> = Eventsourced<NodeCommand<'TT>, NodeEvent<'TT>, NodeEvent<'TT> list>

    let createActor<'TT> (system: IActorRefFactory) =
        let snapshotWindow = 10
        let mutable afterSnapshot = 0

        let toEvent cmd = 
            Command {
                Command = cmd
                TimeStamp = DateTimeOffset.UtcNow
            }

        let update state e = (e)::state
    
        let apply (mailbox: Aggregate<'TT>) state event = 
            match event with
            | Command c -> update state event
            | SnapshotOffer so -> so.Snapshot :?> NodeEvent<'TT> list

        let exec (mailbox: Aggregate<'TT>) state cmd =
            printfn "Cmd is: %A" cmd
            // TODO: Add source validation
            mailbox.PersistEvent (update state) [toEvent cmd]
            afterSnapshot <- afterSnapshot + 1
            if afterSnapshot >= snapshotWindow then
                mailbox.SaveSnapshot state
                afterSnapshot <- 0
            ()

        spawnPersist system childActors.CommandLog {
            state = []
            apply = apply
            exec = exec
        } []


module NodeRefStore =
    type CommandWrapper = {
        Command: NodeAdminCommand
        TimeStamp: DateTimeOffset
    }

    type NodeEvent = 
        | Command of CommandWrapper
        | SnapshotOffer of SnapshotOffer

    type Aggregate = Eventsourced<NodeAdminCommand, NodeEvent, Set<NodeRef>>

    let createActor (system: IActorRefFactory) =
        let toEvent cmd = 
            Command {
                Command = cmd
                TimeStamp = DateTimeOffset.UtcNow
            }

        let update state = 
            function
            | Command c -> match c.Command with 
                            | AddNode nr -> Set.add nr state 
                            | RemoveNode nr -> Set.remove nr state
            | SnapshotOffer _ -> state
            
    
        let apply (mailbox: Aggregate) state event = 
            match event with
            | Command c -> update state event
            | SnapshotOffer so -> so.Snapshot :?> Set<NodeRef>

        let exec (mailbox: Aggregate) state cmd =
            printfn "Cmd is: %A" cmd
            // TODO: Add source validation
            mailbox.PersistEvent (update state) [toEvent cmd]
            mailbox.SaveSnapshot state

        spawnPersist system childActors.NodeRefStore {
            state = set[]
            apply = apply
            exec = exec
        } []

let createNodeActor<'TT> (system: IActorRefFactory) =
    spawn system "node"
        <| fun mailbox ->
                // define child actor
                let commandLog = CommandLog.createActor<'TT> mailbox
                let nodeRefStore = NodeRefStore.createActor mailbox
                    
                // define parent behavior
                let rec parentLoop() =
                    actor {
                        let! (msg: NodeCommand<'TT>) = mailbox.Receive()
                        commandLog.Forward(msg)  // forward all messages through
                        match msg with
                        | Admin c -> nodeRefStore.Forward(msg)
                        | Post t -> () // TODO:
                        | Confirmation c -> () // TODO:
                        return! parentLoop()
                    }
                parentLoop()


type AddData<'TS, 'TD when 'TS: equality and 'TD: equality> =
    | Event of HashedEvent<'TD>
    | State of HashedState<'TS>
    | Frame of HashedFrame<'TS, 'TD>


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
