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
#load "NodeCommand.fs"
#load "CommandLog.fs"
#load "NodeRefStore.fs"
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

open Avalanchain
open Avalanchain.Quorum
open Avalanchain.RefsAndPathes
open Avalanchain.SecPrimitives
open Avalanchain.SecKeys
open Avalanchain.StreamEvent
open Avalanchain.EventStream
open Avalanchain.NodeContext
open Avalanchain.Cluster.AutomaticCluster
open Avalanchain.Cluster.Extension
open Avalanchain.Cluster.Sharded
open Avalanchain.Cluster.Actors
open Avalanchain.Cluster.CommandLog
open Avalanchain.Cluster.NodeRefStore



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
    
   


type NodeChildActors = {
    CommandLog: string
    NodeRefStore: string 
}

let childActors = {
    CommandLog = "command-log"
    NodeRefStore = "node-ref-store"
}



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