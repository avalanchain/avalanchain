[<AutoOpen>]
module Avalanchain.SecPrimitives

open System
open FSharpx.Collections
open FSharp.Core.Fluent

type Nonce = uint32

type Serialized = byte array
and Serializer<'TData> = 'TData -> Serialized
and Deserializer<'TData> = Serialized -> 'TData 

type Hash = Hash of Serialized // TODO: Add boundaries, algorithm, etc
    with member inline this.Bytes = match this with Hash h -> h
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
        | Empty -> Hash([||])
        | Leaf h -> h
        | Tree hd -> hd.Hash
and Merkled<'TData> = { Merkle: MerkleTree; Value: 'TData }

// TODO: Fix merkles
let toMerkle (serializer: Serializer<'T>) hasher optionalInit data : MerkleTree =
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
