// Define your library scripting code here

#time "on"
#r "../packages/Akka.1.0.7.154-beta/lib/net45/Akka.dll"
#r "../packages/Wire.0.0.6/lib/Wire.dll"
#r "../packages/Newtonsoft.Json.8.0.2/lib/net45/Newtonsoft.Json.dll"
#r "../packages/FSPowerPack.Core.Community.3.0.0.0/Lib/Net40/FSharp.PowerPack.dll"
#r "../packages/FSPowerPack.Linq.Community.3.0.0.0/Lib/Net40/FSharp.PowerPack.Linq.dll"
#r "../packages/Google.ProtocolBuffers.2.4.1.555/lib/net40/Google.ProtocolBuffers.dll"
#r "../packages/Google.ProtocolBuffers.2.4.1.555/lib/net40/Google.ProtocolBuffers.Serialization.dll"
#r "../packages/Akka.Cluster.1.0.7.154-beta/lib/net45/Akka.Cluster.dll"
#r "../packages/Akka.Persistence.1.0.7.154-beta/lib/net45/Akka.Persistence.dll"
#r "../packages/Akka.Persistence.FSharp.1.0.7.154-beta/lib/net45/Akka.Persistence.FSharp.dll"
#r "../packages/Akka.Persistence.Sql.Common.1.0.7.154-beta/lib/net45/Akka.Persistence.Sql.Common.dll"
#r "../packages/Akka.Persistence.Sqlite.1.0.7.154-beta/lib/net45/Akka.Persistence.Sqlite.dll"
#r "../packages/Akka.Cluster.Sharding.1.0.7.154-beta/lib/net45/Akka.Cluster.Sharding.dll"
#r "../packages/Akka.Cluster.Tools.1.0.7.154-beta/lib/net45/Akka.Cluster.Tools.dll"
//#r "../packages/FSharp.Core.4.0.0.1/lib/net40/FSharp.Core.dll"
#r "../packages/Akka.1.0.7.154-beta/lib/net45/Akka.dll"
#r "../packages/Akka.FSharp.1.0.7.154-beta/lib/net45/Akka.FSharp.dll"
#r "../packages/System.Collections.Immutable.1.1.37/lib/dotnet/System.Collections.Immutable.dll"
#r "../packages/System.Data.SQLite.Core.1.0.99.0/lib/net451/System.Data.SQLite.dll"
#r "../packages/Chessie.0.4.0/lib/net40/Chessie.dll"
#r "../packages/Base58Check.0.2.0/lib/Net40/Base58Check.dll"
#r "../packages/Helios.1.4.2/lib/net45/Helios.dll"
#r "../packages/FsPickler.1.7.2/lib/net45/FsPickler.dll"
#r "../packages/Akka.Cluster.1.0.7.154-beta/lib/net45/Akka.Cluster.dll"
#r "../packages/Akka.Cluster.Sharding.1.0.7.154-beta/lib/net45/Akka.Cluster.Sharding.dll"
#r "../packages/Akka.Cluster.Tools.1.0.7.154-beta/lib/net45/Akka.Cluster.Tools.dll"
#r "../packages/Akkling.0.3.0/lib/net45/Akkling.dll"
#r "../packages/Akkling.Persistence.0.3.0/lib/net45/Akkling.Persistence.dll"
#r "../packages/Akkling.Cluster.Sharding.0.3.0/lib/net45/Akkling.Cluster.Sharding.dll"
//#r "../packages/"
//#r "../packages/"

#I "bin/Debug/"

//#r "bin/Debug/Newtonsoft.Json.dll"
//#r "bin/Debug/Akka.dll"
////#r "bin/Debug/Akka.Remote.dll"
////#r "bin/Debug/Akka.Persistence.dll"
//#r "bin/Debug/Akkling.dll"
//#r "bin/Debug/Akkling.Persistence.dll"
//#r "bin/Debug/Google.ProtocolBuffers.dll"
//#r "bin/Debug/Akka.Persistence.dll"
//#r "bin/Debug/Akkling.Persistence.dll"
//#r "bin/Debug/Akka.Cluster.dll"
////#r "bin/Debug/Akka.Cluster.Sharding.dll"
////#r "bin/Debug/Akka.Cluster.Tools.dll"
//#r "bin/Debug/Akkling.Cluster.Sharding.dll"
#r "bin/Debug/Avalanchain.dll"


#load "Messages.fs"
#load "AutomaticCluster.fs"
#load "Actors.fs"
#load "Node.fs"
#load "Extension.fs"
#load "Sharded.fs"
//#load "SqliteCluster.fs"

open System
open System.Threading
open System.Collections.Immutable
open Akka.Actor
//open Akka.FSharp
//
open Akka.Persistence
//open Akka.Persistence.FSharp

open Akka.Cluster
open Akka.Cluster.Tools
open Akka.Cluster.Sharding

open Akkling
open Akkling.Persistence
open Avalanchain.Quorum
open Avalanchain.Cluster.AutomaticCluster
open Avalanchain.Cluster.Extension
open Avalanchain.Cluster.Sharded
open Avalanchain.Cluster.Actors
open Avalanchain.RefsAndPathes
open Avalanchain.SecPrimitives
open Avalanchain.SecKeys
open Avalanchain.StreamEvent
open Avalanchain.EventStream
open Avalanchain
open Avalanchain.NodeContext



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
            seed-nodes = [ "akka.tcp://sys-1@localhost:5000/" ]
          }
          persistence {
            journal.plugin = "akka.persistence.journal.inmem"
            snapshot-store.plugin = "akka.persistence.snapshot-store.local"
          }
        }
        """)
    config.WithFallback(Tools.Singleton.ClusterSingletonManager.DefaultConfig())
    
   

type NodeCommand<'TD when 'TD: equality> = 
    | Post of Transaction<'TD>
    | Admin of NodeAdminCommand
    | Confirmation of ConfirmationPacket
    | Monitor of NodeMonitorQuery
and Transaction<'TD> = Hashed<EventStreamRef> * 'TD
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
and NodeMonitorQuery =
    | Streams                          // Set<StreamStatusData>
    | Stream of Hashed<EventStreamRef> // StreamStatusData option
    | KnownNodeRefs
and StreamStatusData<'TS when 'TS: equality> = {
    Ref: Hashed<EventStreamRef>
    State: Hashed<'TS>
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
    type CommandWrapper<'TD when 'TD: equality> = {
        Command: NodeCommand<'TD>
        TimeStamp: DateTimeOffset
    }

    type CommandEvent<'TD when 'TD: equality> = 
        | Command of CommandWrapper<'TD>
        | SnapshotOffer of SnapshotOffer


    type CommandLogMessage<'TD when 'TD: equality> = 
        | Command of NodeCommand<'TD>
        | Event of CommandEvent<'TD>

    let toEvent (cmd: NodeCommand<'TD>) = 
        CommandEvent.Command {
            Command = cmd
            TimeStamp = DateTimeOffset.UtcNow
        } |> Event

    let createActor<'TD> (system: IActorRefFactory) = 
        spawn system "command-log" <| propsPersist(fun mailbox -> 
            let rec loop state = 
                actor { 
                    let! msg = mailbox.Receive()
                    match msg with 
                        | Command cmd -> return Persist ((cmd |> toEvent))
                        | Event e -> return! loop (e::state)
//                            match e with
//                            | NodeEvent.Command c -> return! loop (e::state)
//                            | SnapshotOffer so -> mailbox.s
                }
            loop [])

module NodeRefStore =
    type CommandWrapper = {
        Command: NodeAdminCommand
        TimeStamp: DateTimeOffset
    }

    type NodeRefEvent = 
        | Command of CommandWrapper
        | SnapshotOffer of SnapshotOffer

    type NodeRefQuery = 
        | All 
        | IsKnown of NodeRef

    type NodeMessage = 
        | Command of NodeAdminCommand
        | Event of NodeRefEvent
        | Query of NodeRefQuery

    let toEvent cmd = 
        NodeRefEvent.Command {
            Command = cmd
            TimeStamp = DateTimeOffset.UtcNow
        } |> Event

    let createActor<'TD> (system: IActorRefFactory) = 
        spawn system "node-ref-store" <| propsPersist(fun mailbox -> 
            let rec loop state = 
                actor { 
                    let! msg = mailbox.Receive()
                    match msg with 
                        | Command cmd -> return Persist ((cmd |> toEvent))
                        | Event e -> 
                            match e with
                            | NodeRefEvent.Command c -> 
                                match c.Command with 
                                    | AddNode nr -> return! loop (Set.add nr state)
                                    | RemoveNode nr -> return! loop (Set.remove nr state)
                            //| SnapshotOffer so -> mailbox.s
                            | _ -> return! loop state 
                        | Query q ->
                            match q with
                            | All -> 
                                mailbox.Sender() <! state
                                return! loop state
                            | IsKnown nr -> 
                                mailbox.Sender() <! state.Contains nr
                                return! loop state
                }
            loop (set[]))

//module Post =
//    type PostQuery = 
//        | All // TODO: Add fundamental queries
//
//    type PostMessage<'TD> = 
//        | Command of Transaction<'TD>
//        | Event of HashedEvent<'TD>
//        | Query of PostQuery
//
//    let toEvent eventHasher cmd = 
//        let Transaction data = cmd
//        PostEvent.Command {
//            Command = cmd
//            TimeStamp = DateTimeOffset.UtcNow
//        } |> Event
//
//    let internal createActor<'TState, 'TData when 'TData: equality and 'TState: equality> (system: IActorRefFactory) 
//        (streamLogicContext: Stream2.StreamLogicContext<'TState, 'TData>) streamDef =
//
//        let eventSourcingLogic = Stream2.streamLogic<'TState, 'TData, EventProcessingMsg> streamLogicContext streamDef
//
//        spawn system "node-store" <| propsPersist(fun mailbox -> 
//            let rec loop (frame: 'TFrame option) = 
//                actor { 
//                    let! (msg: PostMessage<'TData>) = mailbox.Receive()
//                    let getState() = match frame with
//                                        | Some f -> eventSourcingLogic.Unbundle f |> snd
//                                        | None -> eventSourcingLogic.InitialState
//                    match msg with 
//                        | Command t -> 
//                            let state = getState()
//                            let event = eventSourcingLogic.Process state t
//                            return Persist ((cmd |> toEvent))
//                        | Event e -> 
//                            match e with
//                            | PostEvent.Command c -> return! loop (c::state)
//                            //| SnapshotOffer so -> mailbox.s
//                            | _ -> return! loop state 
//                        | Query q ->
//                            match q with
//                            | All -> 
//                                mailbox.Sender() <! getState()
//                                return! loop frame
//                }
//            loop (None))

let createNodeActor<'TS, 'TD when 'TS: equality and 'TD: equality> (system: IActorRefFactory) nodePath =
    spawn system "node"
        <| props(fun mailbox ->
                // define child actor
                let commandLog = CommandLog.createActor<'TD> mailbox
                let nodeRefStore = NodeRefStore.createActor mailbox
                let nodeExtension = ChainNode.Get mailbox.System
                let node() = nodeExtension.GetNode<'TS, 'TD>(nodePath, [ExecutionGroup.Default])
                    
                // define parent behavior
                let rec loop() =
                    actor {
                        let! (msg: NodeCommand<'TD>) = mailbox.Receive()
                        commandLog.Forward(msg)  // forward all messages through the log
                        match msg with
                        | Admin c -> nodeRefStore <! NodeRefStore.NodeMessage.Command c
                        | Post post -> 
                            let streamRef, t = post
                            let ret = node().Push streamRef t
                            mailbox.Sender() <! ret
                        | Confirmation c -> 
                            () // TODO:
                        | Monitor m ->
                            match m with 
                            | Streams -> 
                                let ret = node().States
                                mailbox.Sender() <! ret
                            | Stream streamRef -> 
                                let ret = node().State streamRef
                                mailbox.Sender() <! ret
                            | KnownNodeRefs -> 
                                let refs = 
                                    async {
                                            return! nodeRefStore <? NodeRefStore.Query (NodeRefStore.NodeRefQuery.All)
                                        } |> Async.RunSynchronously // TODO: Remove sync 
                                mailbox.Sender() <! refs
                            
                        return! loop()
                    }
                loop())


type AddData<'TS, 'TD when 'TS: equality and 'TD: equality> =
    | Event of HashedEvent<'TD>
    | State of HashedState<'TS>
    | Frame of HashedFrame<'TS, 'TD>


let system = System.create "sys-1" <| configWithPort 5000

Thread.Sleep(2000)

let system1 = System.create "sys-1" <| configWithPort 5001

let system2 = System.create "sys-1" <| configWithPort 5002

//let system = System.create "persisting-sys" <| Configuration.defaultConfig()


let ct = Utils.cryptoContext
let nc = NodeContext.buildNodeContext<double, double> ct
let nodeActor = createNodeActor<double, double> system "/"

let nr = ("aaa", [| 1uy; 2uy |]) |> nc.DataHashers.nodeRefDh
nodeActor <! Admin(AddNode (nr))

let refs: AskResult<obj> =
    async {
        return! nodeActor <? Monitor(KnownNodeRefs)
    } |> Async.RunSynchronously


//type INodeStore =
//    member __.Node<'TS, 'TD when 'TS: equality and 'TD: equality> : unit -> Node<'TS, 'TD>



//    let inline ofArray (source: 'T[]) : Stream<'T> =
//       fun k ->
//          let mutable i = 0
//          while i < source.Length do
//                k source.[i]
//                i <- i + 1          
//
//    let inline filter (predicate: 'T -> bool) (stream: Stream<'T>) : Stream<'T> =
//       fun k -> stream (fun value -> if predicate value then k value)
//
//
//    let inline iter (iterF: 'T -> unit) (stream: Stream<'T>) : unit =
//       stream iterF 
//
//    let inline toArray (stream: Stream<'T>) : 'T [] =
//       let acc = new List<'T>()
//       stream |> iter (fun v -> acc.Add(v))
//       acc.ToArray()
//
//    let inline fold (foldF:'State->'T->'State) (state:'State) (stream:Stream<'T>) =
//       let acc = ref state
//       stream (fun v -> acc := foldF !acc v)
//       !acc
//
//    let inline reduce (reducer: ^T -> ^T -> ^T) (stream: Stream< ^T >) : ^T
//          when ^T : (static member Zero : ^T) =
//       fold (fun s v -> reducer s v) LanguagePrimitives.GenericZero stream
//
//    let inline sum (stream : Stream< ^T>) : ^T
//          when ^T : (static member Zero : ^T)
//          and ^T : (static member (+) : ^T * ^T -> ^T) =
//       fold (+) LanguagePrimitives.GenericZero stream