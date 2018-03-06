namespace Avalanchain.Core

module Crdt =
    type ReplicaId = string
    type Pos = uint64

    module GCounter =  
        type GCounter = Map<ReplicaId, Pos>
        let zero: GCounter = Map.empty
        let value (c: GCounter) = 
            c |> Map.fold (fun acc _ v -> acc + v) 0UL
        let inc r (c: GCounter) =
            match Map.tryFind r c with
            | Some x -> Map.add r (x + 1UL) c
            | None   -> Map.add r 1UL c
        let merge (a: GCounter) (b: GCounter): GCounter =
            a |> Map.fold (fun acc ka va ->
                match Map.tryFind ka acc with
                | Some vb -> Map.add ka (max va vb) acc
                | None    -> Map.add ka va acc) b
                

    type Ord =  
    | Lt = -1  // lower
    | Eq = 0   // equal
    | Gt = 1   // greater
    | Cc = 2   // concurrent

    type VClock = GCounter.GCounter
    module VClock =  
        let zero = GCounter.zero
        let inc = GCounter.inc
        let merge = GCounter.merge
        let compare (a: VClock) (b: VClock): Ord = 
            let valOrDefault k map =
                match Map.tryFind k map with
                | Some v -> v
                | None   -> 0UL
            let akeys = a |> Map.toSeq |> Seq.map fst |> Set.ofSeq
            let bkeys = b |> Map.toSeq |> Seq.map fst |> Set.ofSeq
            (akeys + bkeys)
            |> Seq.fold (fun prev k ->
                let va = valOrDefault k a
                let vb = valOrDefault k b
                match prev with
                | Ord.Eq when va > vb -> Ord.Gt
                | Ord.Eq when va < vb -> Ord.Lt
                | Ord.Lt when va > vb -> Ord.Cc
                | Ord.Gt when va < vb -> Ord.Cc
                | _ -> prev ) Ord.Eq