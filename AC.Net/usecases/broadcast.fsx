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

type Endpoint = {
    IP: string
    Port: uint16
}
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
              maximum-frame-size = 4000000b
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
                                    return (v |> unbox |> Some)
        | DataDeleted k ->          printfn "State already deleted: '%A'" k
                                    return None
        | UpdateTimeout k ->        printfn "Update of value for the uid '%A' timed out" k
                                    return None
    }
    member __.Add value: Async<'T option> = updateState key value
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
    

let endpoint1 = { IP = "127.0.0.1"; Port = 5000us }
let endpoint2 = { IP = "127.0.0.1"; Port = 5001us }
let endpoint3 = { IP = "127.0.0.1"; Port = 5002us }
let node1 = setupNode endpoint1 [endpoint1; endpoint2]
Threading.Thread.Sleep 5000
let node2 = setupNode endpoint2 [endpoint1; endpoint2]
Threading.Thread.Sleep 2000
let node3 = setupNode endpoint3 [endpoint1; endpoint2]

let keyUID = (1).ToString()
let b1_uid1 = keyUID |> broadcaster<string> node1
let b2_uid1 = keyUID |> broadcaster<string> node2
let b3_uid1 = keyUID |> broadcaster<string> node3

let inserts max (broadcaster: Broadcaster<string>) = asyncSeq {
    for i in 1 .. max do
        let v = sprintf "value: %s" (i.ToString())
        // printfn "Sent %s" v
        yield broadcaster.Add v
} 

let printAsync av = async { let! vo = av; 
                            printfn "Added: %s" (match vo with Some v -> v | None -> "<<Error>>") }

let read f (broadcaster: Broadcaster<string>) = 
    async {
        let! vo = broadcaster.Read()
        match vo with
        | Some v -> f v
        | None -> ()
    }
    |> Async.RunSynchronously

let printCount (broadcaster: Broadcaster<string>) = read (fun v -> printfn "Size: %d" v.Count) broadcaster
let printState (broadcaster: Broadcaster<string>) = read (fun v -> printfn "Value: %A" v) broadcaster


#time;;

inserts 100000 b1_uid1
|> AsyncSeq.iterAsync (Async.Ignore)
|> Async.RunSynchronously


printState b1_uid1
printState b2_uid1
printState b3_uid1

printCount b1_uid1
printCount b2_uid1
printCount b3_uid1