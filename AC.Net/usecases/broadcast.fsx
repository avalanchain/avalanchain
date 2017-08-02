module Avalanchain

open System
open System.IO
#if INTERACTIVE
let cd = Path.Combine(__SOURCE_DIRECTORY__, "../bin/Avalanchain")
System.IO.Directory.SetCurrentDirectory(cd)
#endif

#r "../bin/Avalanchain/System.Collections.Immutable.dll"
#r "../bin/Avalanchain/Akka.dll"
#r "../bin/Avalanchain/Newtonsoft.Json.dll"
#r "../bin/Avalanchain/FSharp.PowerPack.Linq.dll"
#r "../bin/Avalanchain/DotNetty.Common.dll"
#r "../bin/Avalanchain/DotNetty.Buffers.dll"
#r "../bin/Avalanchain/DotNetty.Codecs.dll"
#r "../bin/Avalanchain/DotNetty.Handlers.dll"
#r "../bin/Avalanchain/DotNetty.Transport.dll"
#r "../bin/Avalanchain/FsPickler.dll"
#r "../bin/Avalanchain/Google.ProtocolBuffers.dll"
#r "../bin/Avalanchain/Google.ProtocolBuffers.Serialization.dll"
#r "../bin/Avalanchain/Akka.Remote.dll"
#r "../bin/Avalanchain/Google.ProtocolBuffers.dll"
#r "../bin/Avalanchain/Akka.Cluster.dll"
#r "../bin/Avalanchain/Akka.DistributedData.dll"
#r "../bin/Avalanchain/Akka.Serialization.Hyperion.dll"
#r "../bin/Avalanchain/Akkling.dll"
#r "../bin/Avalanchain/Akkling.DistributedData.dll"
#r "../bin/Avalanchain/FSharp.Control.AsyncSeq.dll"

open System.Collections.Immutable
open FSharp.Control

open Akka.Cluster
open Akka.DistributedData
open Akkling
open Akkling.DistributedData
open Akkling.DistributedData.Consistency

    
module Network =
    type Endpoint = {
        IP: string
        Port: uint16
    }

open Network

let setupNode endpoint (seedNodes: Endpoint list) =
    let systemName = "ac"
    let seedNodes = seedNodes 
                    |> List.map (fun ep -> sprintf "\"akka.tcp://%s@%s:%d/\"" systemName ep.IP ep.Port) 
                    |> fun l -> "[" + String.Join(", ", l) + "]"
    printfn "%s" seedNodes
    sprintf """
    akka {
        actor {
            provider = "Akka.Cluster.ClusterActorRefProvider, Akka.Cluster"
            serializers {
                hyperion = "Akka.Serialization.HyperionSerializer, Akka.Serialization.Hyperion"
            }
            serialization-bindings {
                "System.Object" = hyperion
            }
        }
        remote {
            helios.tcp {
            public-hostname = "%s"
            hostname = "%s"
            port = %d
            maximum-frame-size = 40000000b
            }
        }
        cluster {
            auto-down-unreachable-after = 5s
            seed-nodes = %s
            distributed-data {
                max-delta-elements = 10000
            }
        }
        persistence {
            journal.plugin = "akka.persistence.journal.inmem"
            snapshot-store.plugin = "akka.persistence.snapshot-store.local"
        }
        }
    """ endpoint.IP endpoint.IP endpoint.Port seedNodes
    |> Configuration.parse
    |> System.create systemName 

type Broadcaster<'T> (system, uid, initList: 'T list, doLog) = 
    let cluster = Cluster.Get system
    let replicator = getReplicator system
    // some helper functions
    let (++) set e = ORSet.add cluster e set
    let toSet initSet = List.fold (++) initSet
    // initialize set
    let initSet = initList |> toSet ORSet.empty
    let key = ORSet.key uid 
    let updateState key (data: 'T) = async {
        let! reply = (retype replicator) <? update (fun (set: ORSet<'T>) -> set ++ data) writeLocal initSet key
        match reply.Value with
        | UpdateSuccess(k, v) ->    if doLog then printfn "State modified for uid '%A'" k
                                    return Some ()
        | DataDeleted k ->          printfn "State already deleted: '%A'" k
                                    return None
        | UpdateTimeout k ->        printfn "Update of value for the uid '%A' timed out" k
                                    return None
    }
    member __.Add value: Async<unit option> = updateState key value
        // async {
        // let! reply = (retype replicator) <? get readLocal key
        // match reply.Value with
        // | GetSuccess(k, (data : ORSet<'T>), _) -> return! updateState key (valueList |> toSet data)
        // | NotFound k -> return! updateState key (valueList |> toSet initSet)
        // | DataDeleted k -> printfn "Data for key '%A' has been deleted" k; return None
        // | GetFailure(k, _) -> printfn "Data for key '%A' didn't received in time" k; return None
    // } 

    member __.Read(): Async<IImmutableSet<'T> option> = async {
        let! reply = (retype replicator) <? get readLocal key
        match reply.Value with
        | GetSuccess(k, (data : ORSet<'T>), _) -> 
            let value = data |> ORSet.value
            // printfn "Data for key %A: %A" k value
            return (value |> Some)

        | NotFound k -> printfn "Data for key '%A' not found" k; return None
        | DataDeleted k -> printfn "Data for key '%A' has been deleted" k; return None
        | GetFailure(k, _) -> printfn "Data for key '%A' didn't received in time" k; return None
    } 

let broadcaster<'T> node uid = Broadcaster<'T>(node, uid, [], false)
    
type HashedValue = {
    value: string
    hash: string
}
