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
    

    type ORSetHelper<'T when 'T: null> (system: ActorSystem, key) = 
        let cluster = Akka.Cluster.Cluster.Get system
        let ddata = DistributedData.Get system

        let orsetKey = ORSet.key<'T> key //"chainDefs"

        // some helper functions
        let (++) set e = ORSet.add cluster e set
        let (--) set e = ORSet.remove cluster e set


        // initialize set
        // let set = [ for i in 0 .. 9999 -> i ] |> List.fold (++) ORSet.empty

        // let set = chainDefs 
        //             |> List.map (fun cd -> cd.Token)
        //             |> List.fold (++) ORSet.empty

        member __.Get() = async {   let! result = ddata.AsyncGet(orsetKey, readLocal)
                                    let! res2 = ddata.GetAsync(orsetKey, readLocal) |> Async.AwaitTask
                                    return result |> Option.defaultValue ORSet.empty }

        member private __.Modify stateUpdater = async { let! state = __.Get()
                                                        let newState = stateUpdater state
                                                        return! ddata.AsyncUpdate(orsetKey, newState, writeLocal)   }

        member __.Add items = __.Modify <| fun state -> items |> ORSet.ofSeq cluster |> ORSet.merge state

        member __.Remove items = __.Modify <| fun state -> items |> List.fold (--) state

        member __.Clear () = __.Modify <| fun state -> ORSet.clear cluster state

        member __.Delete() = ddata.AsyncDelete(orsetKey, writeLocal) 

    type GSetHelper<'T when 'T: null> (system: ActorSystem, key) = 
        let cluster = Akka.Cluster.Cluster.Get system
        let ddata = DistributedData.Get system

        let gsetKey = GSet.key<'T> key 

        // some helper functions
        let (++) set e = GSet.add e set

        // initialize set
        // let set = [ for i in 0 .. 9999 -> i ] |> List.fold (++) ORSet.empty

        // let set = chainDefs 
        //             |> List.map (fun cd -> cd.Token)
        //             |> List.fold (++) ORSet.empty

        member __.Get() = async {   let! result = ddata.AsyncGet(gsetKey, readLocal)
                                    return result |> Option.defaultValue GSet.empty }

        member private __.Modify stateUpdater = async { let! state = __.Get()
                                                        let newState = stateUpdater state
                                                        return! ddata.AsyncUpdate(gsetKey, newState, writeLocal)   }

        member __.Add items = __.Modify <| fun state -> items |> GSet.ofSeq |> GSet.merge state

        member __.Delete() = ddata.AsyncDelete(gsetKey, writeLocal) 

    // type ORMultiMapHelper<'T when 'T: null> (system: ActorSystem, key) = 
    //     let cluster = Akka.Cluster.Cluster.Get system
    //     let ddata = DistributedData.Get system

    //     let orsetKey = ORMultiMap.key key //"chainDefs"

    //     // some helper functions
    //     let (++) set e = ORSet.add cluster e set
    //     let (--) set e = ORSet.remove cluster e set


    //     // initialize set
    //     // let set = [ for i in 0 .. 9999 -> i ] |> List.fold (++) ORSet.empty

    //     // let set = chainDefs 
    //     //             |> List.map (fun cd -> cd.Token)
    //     //             |> List.fold (++) ORSet.empty

    //     member __.Get() = async {   let! result = ddata.AsyncGet(orsetKey, readLocal)
    //                                 return result |> Option.defaultValue ORMultiMap.empty }

    //     member private __.Modify stateUpdater = async { let! state = __.Get()
    //                                                     let newState = stateUpdater state
    //                                                     return! ddata.AsyncUpdate(orsetKey, newState, writeLocal)   }

    //     member __.Add items = __.Modify <| fun state -> items |> ORSet.ofSeq cluster |> ORMultiMap.merge state

    //     member __.Remove items = __.Modify <| fun state -> items |> List.fold (--) state

    //     member __.Clear () = __.Modify <| fun state -> ORSet.clear cluster state

    //     member __.Delete() = ddata.AsyncDelete(orsetKey, writeLocal)         
    

    let chainDefs system = ORSetHelper(system, "chainDefs")


    type PaymentAccountRef = {
        Address: string
    }

    type PaymentAmount = decimal

    type PaymentTransaction = {
        From: PaymentAccountRef
        To: (PaymentAccountRef * PaymentAmount)[]
    }

    let transactions system = ORSetHelper(system, "transactions")

    type PaymentAccount = {
        Ref: PaymentAccountRef
        // PublicKey: SigningPublicKey
        Name: string
        // CryptoContext: CryptoContext
    }


    type PaymentBalances(system: ActorSystem, key) =
        let cluster = Akka.Cluster.Cluster.Get system
        let ddata = DistributedData.Get system

        let orsetKey = ORSet.key<'T> key //"chainDefs"    


