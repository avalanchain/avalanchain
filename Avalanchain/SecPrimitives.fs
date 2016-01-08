[<AutoOpen>]
module Avalanchain.SecPrimitives

open System
open FSharpx.Collections
open FSharp.Core.Fluent

type Nonce = uint64

type Serialized = byte array
and Serializer<'TData> = 'TData -> Serialized
and Deserializer<'TData> = Serialized -> 'TData 

type Hash = Hash of Serialized // TODO: Add boundaries, algorithm, etc
    with 
        member inline this.Bytes = match this with Hash h -> h
        static member inline Zero = Hash([||])
and Hashed<'TData> = { Hash: Hash; Value: 'TData }
and Hasher = Serialized -> Hash
and DataHasher<'TData> = 'TData -> Hashed<'TData> 


type MerkleTree = 
    | Empty
    | Leaf of Hash
    | Tree of Hashed<MerkleTree * MerkleTree>
with 
    member this.Hash = 
        match this with
        | Empty -> Hash.Zero
        | Leaf h -> h
        | Tree hd -> hd.Hash
    member this.OwnHash = 
        match this with
        | Empty -> Hash.Zero
        | Leaf h -> h
        | Tree hd -> (fst hd.Value).Hash // Left leaf has value of the newly added element
and Merkled<'TData> = { Merkle: MerkleTree; Value: 'TData }

// TODO: Fix merkles
let toMerkle (serializer: Serializer<'T>) hasher optionalInit (data: 'T list) : MerkleTree =
    let bytesList = data |> List.map serializer
    let fold init els =
        els
        |> Seq.fold (fun merkle d -> 
                        let leaf = Leaf(hasher d)
                        Tree{Hash = (Array.append d merkle.Hash.Bytes) |> hasher; Value = (leaf, merkle)}
                    ) init
        
    match bytesList, optionalInit with
    | [], None -> Empty
    | [], Some init -> init
    | x :: [], None -> Leaf(hasher x)
    | x :: [], Some init -> fold init ([(hasher x).Bytes])
    | x :: xs, None -> fold (Leaf(hasher x)) (xs)   
    | x :: xs, Some init -> fold init (bytesList) 

let toMerkled (serializer: Serializer<'TData>) hasher optionalInit data : Merkled<'TData> =
    let mt = toMerkle serializer hasher optionalInit [data]
    { Merkle = mt; Value = data }

let hashToMerkle hasher optionalInit hashes : MerkleTree =
    let fold init els =
        els
        |> Seq.fold (fun merkle d -> 
                        let leaf = Leaf(d)
                        Tree{Hash = (Array.append d.Bytes merkle.Hash.Bytes) |> hasher; Value = (leaf, merkle)}
                    ) init
        
    match hashes, optionalInit with
    | [], None -> Empty
    | [], Some init -> init
    | x :: [], None -> Leaf(x)
    | x :: [], Some init -> fold init ([x])
    | x :: xs, None -> fold (Leaf(x)) (xs)   
    | x :: xs, Some init -> fold init (hashes) 

