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


        // initialize set
        // let set = [ for i in 0 .. 9999 -> i ] |> List.fold (++) ORSet.empty

        // let set = chainDefs 
        //             |> List.map (fun cd -> cd.Token)
        //             |> List.fold (++) ORSet.empty

        member __.Get() = async {   let! result = ddata.AsyncGet(ddKey) // readAll (TimeSpan.FromSeconds 1.))
                                    //let! res2 = ddata.GetAsync(orsetKey, readLocal) |> Async.AwaitTask
                                    return result |> Option.defaultValue ORSet.empty }

        member private __.Modify_ stateUpdater = async {let! state = __.Get()
                                                        let newState = stateUpdater state
                                                        return! ddata.AsyncUpdate(ddKey, newState)   }

        member private __.Modify stateUpdater = 
            async { let! state = __.Get()
                    //let newState = stateUpdater state
                    let doLog = true
                    let! reply = (retype replicator) <? update stateUpdater writeLocal ORSet<'T>.Empty ddKey
                    match reply (*.Value*) with
                    | UpdateSuccess(k, v) ->    if doLog then printfn "State modified for uid '%A'" k
                                                return ()
                    | DataDeleted k ->          printfn "State already deleted: '%A'" k
                                                return ()
                    | UpdateTimeout k ->        printfn "Update of value for the uid '%A' timed out" k
                                                return ()   }


        member __.Add items = __.Modify <| fun state -> items |> List.fold (++) state
        member __.Remove items = __.Modify <| fun state -> items |> List.fold (--) state

        member __.Clear () = __.Modify <| fun state -> ORSet.clear cluster state

        member __.Delete() = ddata.AsyncDelete(ddKey) 

    type ORSetHelper2<'T> (system: ActorSystem, key) = 
        let cluster = Akka.Cluster.Cluster.Get system
        let ddata = DistributedData.Get system
        let replicator = ddata.TypedReplicator

        let ddKey = ORSet.key<'T> key //"chainDefs"

        // some helper functions
        let (++) set e = ORSet.add cluster e set
        let (--) set e = ORSet.remove cluster e set


        // initialize set
        // let set = [ for i in 0 .. 9999 -> i ] |> List.fold (++) ORSet.empty

        // let set = chainDefs 
        //             |> List.map (fun cd -> cd.Token)
        //             |> List.fold (++) ORSet.empty

        member __.Get() = async {   let! result = ddata.GetAsync(ddKey, readLocal) |> Async.AwaitTask
                                    return result }

        member private __.Modify stateUpdater = async { //let! state = replicator <! Dsl.Update(orsetKey, WriteLocal.Instance, Func<_,_>(fun x -> x.Merge()))
                                                        let! state = __.Get()
                                                        let newState = stateUpdater state
                                                        return! ddata.AsyncUpdate(ddKey, newState, writeLocal)
                                                        //ddata.Replicator.Tell (Dsl.Update(orsetKey, newState))
                                                        }

        member __.Add items = __.Modify <| fun state -> items |> ORSet.ofSeq cluster |> ORSet.merge state

        member __.Remove items = __.Modify <| fun state -> items |> List.fold (--) state

        member __.Clear () = __.Modify <| fun state -> ORSet.clear cluster state

        member __.Delete() = ddata.AsyncDelete(ddKey, writeLocal) 

    type GSetHelper<'T> (system: ActorSystem, key) = 
        let cluster = Akka.Cluster.Cluster.Get system
        let ddata = DistributedData.Get system
        let replicator = ddata.TypedReplicator
        let ddKey = GSet.key<'T> key 

        // some helper functions
        let (++) set e = GSet.add e set

        member __.Get() = async {   let! result = ddata.AsyncGet(ddKey, readLocal)
                                    return result |> Option.defaultValue GSet.empty }

        member private __.Modify stateUpdater = async { let! state = __.Get()
                                                        let newState = stateUpdater state
                                                        do! ddata.AsyncUpdate(ddKey, newState, writeLocal)   }

        member __.Add items = __.Modify <| fun state -> items |> GSet.ofSeq |> GSet.merge state

        member __.Delete() = ddata.AsyncDelete(ddKey, writeLocal) 

    type GCounterHelper (system: ActorSystem, key) = 
        let cluster = Akka.Cluster.Cluster.Get system
        let ddata = DistributedData.Get system
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
        let ddkey = ORMultiValueDictionaryKey<'K, 'V> key 

        member __.Get() = async {   let! result = ddata.AsyncGet(ddkey)
                                    return result |> Option.defaultValue ORMultiMap<'K, 'V>.Empty }
        member __.Modify newState = ddata.AsyncUpdate(ddkey, newState) 

        member __.AddItem k v = async { let! state = __.Get()
                                        return! ORMultiMap.addItem cluster k v state |> __.Modify }

        member __.AddItems kvs = async {let! state = __.Get()
                                        let newState = kvs |> Seq.fold (fun st (k, v) -> ORMultiMap.addItem cluster k v st) state
                                        return! newState |> __.Modify }


    //     member private __.Modify stateUpdater = async { let! state = __.Get()
    //                                                     let newState = stateUpdater state
    //                                                     return! ddata.AsyncUpdate(orsetKey, newState, writeLocal)   }

    //     member __.Add items = __.Modify <| fun state -> items |> ORSet.ofSeq cluster |> ORMultiMap.merge state

    //     member __.Remove items = __.Modify <| fun state -> items |> List.fold (--) state

    //     member __.Clear () = __.Modify <| fun state -> ORSet.clear cluster state

    //     member __.Delete() = ddata.AsyncDelete(orsetKey, writeLocal)         
    

    type LWWMapHelper<'K, 'V> (system: ActorSystem, key) = 
        let cluster = Akka.Cluster.Cluster.Get system
        let ddata = DistributedData.Get system
        let ddkey: LWWDictionaryKey<'K, 'V> = LWWMap.key key 

        member __.Get() = async {   let! result = ddata.AsyncGet(ddkey)
                                    return result |> Option.defaultValue LWWMap.empty }
        member __.Modify newState = ddata.AsyncUpdate(ddkey, newState) 

        member __.AddItem k v = async { let! state = __.Get()
                                        return! LWWMap.add cluster k v state |> __.Modify }

        member __.AddItems kvs = async {let! state = __.Get()
                                        let newState = kvs |> Seq.fold (fun st (k, v) -> LWWMap.add cluster k v st) state
                                        return! newState |> __.Modify }

    let chainDefs system = ORSetHelper(system, "chainDefs")



