namespace Avalanchain

module DData =

    open System
    open System.Collections.Generic
    open Akka.Actor
    open Akka.Configuration
    open Akka.IO
    open Akka.Persistence
    open Akka.Persistence.Serialization
    // open Akka.Persistence.Journal
    open Akka.Streams
    open Akka.Streams.Dsl
    open Reactive.Streams

    open Hyperion

    open Akkling
    open Akkling.Persistence
    open Akkling.Cluster
    open Akkling.Streams

    open Akka.DistributedData
    open Akkling
    open Akkling.DistributedData
    open Akkling.DistributedData.Consistency
    

    type ORSetHelper<'T> (system: ActorSystem, key) = 
        let cluster = Akka.Cluster.Cluster.Get system
        let ddata = DistributedData.Get system
        let replicator = ddata.TypedReplicator
        let ddKey = ORSet.key<'T> key //"chainDefs"

        // some helper functions
        let (++) set e = ORSet.add cluster e set
        let (--) set e = ORSet.remove cluster e set

        member __.Get() = async {   let! result = ddata.AsyncGet(ddKey) // readAll (TimeSpan.FromSeconds 1.))
                                    //let! res2 = ddata.GetAsync(orsetKey, readLocal) |> Async.AwaitTask
                                    return result |> Option.defaultValue ORSet.empty }

        member private __.Modify_ stateUpdater = async {let! state = __.Get()
                                                        let newState = stateUpdater state
                                                        return! ddata.AsyncUpdate(ddKey, newState)   }

        member __.Modify stateUpdater = 
            async { let doLog = true
                    let! reply = (retype replicator) <? update stateUpdater writeLocal ORSet<'T>.Empty ddKey
                    match reply with
                    | UpdateSuccess(k, v) ->    if doLog then printfn "ORSet State modified for uid '%A'" k
                                                return ()
                    | DataDeleted k ->          printfn "State already deleted: '%A'" k
                                                return ()
                    | UpdateTimeout k ->        printfn "Update of value for the uid '%A' timed out" k
                                                return ()   }


        member __.Add items = __.Modify <| fun state -> items |> List.fold (++) state
        member __.Remove items = __.Modify <| fun state -> items |> List.fold (--) state

        member __.Clear () = __.Modify <| fun state -> ORSet.clear cluster state

        member __.Delete() = ddata.AsyncDelete(ddKey) 


    type GSetHelper<'T> (system: ActorSystem, key) = 
        let cluster = Akka.Cluster.Cluster.Get system
        let ddata = DistributedData.Get system
        let replicator = ddata.TypedReplicator
        let ddKey = GSet.key<'T> key 

        // some helper functions
        let (++) set e = GSet.add e set

        member __.Get() = async {   let! result = ddata.AsyncGet(ddKey, readLocal)
                                    return result |> Option.defaultValue GSet.empty }

        member __.Modify stateUpdater = 
            async { let doLog = true
                    let! reply = (retype replicator) <? update stateUpdater writeLocal GSet<'T>.Empty ddKey
                    match reply with
                    | UpdateSuccess(k, v) ->    if doLog then printfn "GSet State modified for uid '%A'" k
                                                return ()
                    | DataDeleted k ->          printfn "State already deleted: '%A'" k
                                                return ()
                    | UpdateTimeout k ->        printfn "Update of value for the uid '%A' timed out" k
                                                return ()   }

        member __.Add items = __.Modify <| fun state -> items |> GSet.ofSeq |> GSet.merge state

        member __.Delete() = ddata.AsyncDelete(ddKey, writeLocal) 

    type GCounterHelper (system: ActorSystem, key) = 
        let cluster = Akka.Cluster.Cluster.Get system
        let ddata = DistributedData.Get system
        let replicator = ddata.TypedReplicator
        let ddKey = GCounter.key key 

        member __.Get() = async {   let! result = ddata.AsyncGet(ddKey, readLocal)
                                    return result |> Option.defaultValue GCounter.empty }

        member private __.Modify stateUpdater = async { let! state = __.Get()
                                                        let newState = stateUpdater state
                                                        return! ddata.AsyncUpdate(ddKey, newState, writeLocal)   }

        member __.Inc () = __.Modify <| GCounter.inc cluster 1UL
        member __.Inc times = __.Modify <| GCounter.inc cluster times


    type ORMultiMapHelper<'K, 'V> (system: ActorSystem, key) = 
        let cluster = Akka.Cluster.Cluster.Get system
        let ddata = DistributedData.Get system
        let replicator = ddata.TypedReplicator
        let ddKey = ORMultiValueDictionaryKey<'K, 'V> key 

        member __.Get() = async {   let! result = ddata.AsyncGet(ddKey)
                                    return result |> Option.defaultValue ORMultiMap<'K, 'V>.Empty }
        member __.Modify stateUpdater = 
            async { let doLog = true
                    let! reply = (retype replicator) <? update stateUpdater writeLocal ORMultiMap<'K, 'V>.Empty ddKey
                    match reply with
                    | UpdateSuccess(k, v) ->    if doLog then printfn "ORMultiMap State modified for uid '%A'" k
                                                return ()
                    | DataDeleted k ->          printfn "State already deleted: '%A'" k
                                                return ()
                    | UpdateTimeout k ->        printfn "Update of value for the uid '%A' timed out" k
                                                return ()   }


        member __.AddItem k v = ORMultiMap.addItem cluster k v |> __.Modify 

        member __.AddItems kvs = __.Modify <| fun state -> kvs |> Seq.fold (fun st (k, v) -> ORMultiMap.addItem cluster k v st) state
    

    type LWWMapHelper<'K, 'V> (system: ActorSystem, key) = 
        let cluster = Akka.Cluster.Cluster.Get system
        let ddata = DistributedData.Get system
        let replicator = ddata.TypedReplicator
        let ddKey: LWWDictionaryKey<'K, 'V> = LWWMap.key key 

        member __.Get() = async {   let! result = ddata.AsyncGet(ddKey)
                                    return result |> Option.defaultValue LWWMap.empty }
        member __.Modify stateUpdater = 
            async { let doLog = true
                    let! reply = (retype replicator) <? update stateUpdater writeLocal LWWMap<'K, 'V>.Empty ddKey
                    match reply with
                    | UpdateSuccess(k, v) ->    if doLog then printfn "LWWMap State modified for uid '%A'" k
                                                return ()
                    | DataDeleted k ->          printfn "State already deleted: '%A'" k
                                                return ()
                    | UpdateTimeout k ->        printfn "Update of value for the uid '%A' timed out" k
                                                return ()   }

        member __.AddItem k v = LWWMap.add cluster k v |> __.Modify 

        member __.AddItems kvs = __.Modify <| fun state -> kvs |> Seq.fold (fun st (k, v) -> LWWMap.add cluster k v st) state

    let chainDefs system = ORSetHelper(system, "chainDefs")



