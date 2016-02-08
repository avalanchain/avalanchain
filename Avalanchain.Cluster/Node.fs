module Avalanchain.Cluster.Node

open Avalanchain
open Avalanchain.Quorum
open System
open Base58Check
open Akka
open Akka.FSharp
open Akka.Actor
open Akka.Cluster
open Sharded

type RouterType = 
    | Nothing of string
    | Pool of RouterSetting
    | Group of RouterSetting
    with member this.Name = 
            match this with
            | Nothing name -> name
            | Pool rs | Group rs -> rs.Name

and RouterSetting = {
    Name: string
    MaxPerNode: uint32
    MaxTotal: uint32
    MinToOperate: uint32
}

let toBase58Hash (hashed: Hashed<'a>) = 
    Base58CheckEncoding.Encode hashed.Hash.Bytes

let rec processExecutionPolicy (hasher: DataHasher<ExecutionPolicy>) existingGroups (ep: ExecutionPolicy) : RouterType list =
    let id = ep |> hasher |> toBase58Hash
    match ep with
        | None -> Nothing(id) :: existingGroups
        | All eps -> 
            let self = Pool { Name = id; MaxPerNode = 1u; MaxTotal = 1000u; MinToOperate = uint32(eps |> Set.count) }
            self :: (eps |> (Set.fold (processExecutionPolicy hasher) existingGroups))
        | One (strategy, stake) -> 
            match (strategy, stake) with
            | Random, Percentage (p, t) -> 
                let self = Pool { Name = id; MaxPerNode = 1u; MaxTotal = t; MinToOperate = uint32(Math.Round(p * float(t))) }
                self :: existingGroups
            | Random, FixedCount fc -> 
                let self = Pool { Name = id; MaxPerNode = 1u; MaxTotal = fc * 2u; MinToOperate = fc }
                self :: existingGroups
            | Mandatory egs, Percentage (p, t) -> 
                let self = egs |> Set.map(fun (ExecutionGroup eg) -> 
                                            Pool { Name = eg; MaxPerNode = 1u; MaxTotal = t; MinToOperate = uint32(Math.Round(p * float(t))) }) |> Set.toList
                self @ existingGroups
            | Mandatory egs, FixedCount fc -> 
                let self = egs |> Set.map(fun (ExecutionGroup eg) -> 
                                            Pool { Name = eg; MaxPerNode = 1u; MaxTotal = fc * 2u; MinToOperate = fc }) |> Set.toList
                self @ existingGroups

let toSpawnOption routerType = match routerType with
                                | Nothing id -> routerType, []
                                | Pool rs -> 
                                    routerType, [ 
                                                    SpawnOption.Deploy (Deploy(ClusterScope.Instance))
                                                    SpawnOption.Router (
                                                        new Akka.Cluster.Routing.ClusterRouterPool(
                                                            new Akka.Routing.BroadcastPool(int(rs.MaxTotal)),
                                                            new Akka.Cluster.Routing.ClusterRouterPoolSettings(int(rs.MaxTotal), true, rs.Name, int(rs.MaxPerNode))))
                                                ]
                                | Group rs -> failwith "NotImplemented"


let createRouters (shardedSystem: ShardedSystem) routerTypes = 
    routerTypes 
    |> List.map (toSpawnOption >> fun (rt, options) -> shardedSystem.StartPersisted(rt.Name, options))


