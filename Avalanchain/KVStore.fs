module Avalanchain.KVStore

open SecKeys

[<Interface>]
type IKVStore = 
    abstract member Get<'TData> : Hash -> Hashed<'TData> option
    abstract member GetAll<'TData> : MerkleTree -> Merkled<'TData> list

type PublicKeysStore1 = {
    ValidateKey: PublicKey -> bool
    AllValidKeys: Set<PublicKey>
}

type PublicKeysStore (adminPublicKeyChecker: ((PublicKey * Signature) -> bool), validKeys: Set<PublicKey>) = 
    let adminKey = adminPublicKeyChecker
    let mutable keys = validKeys
    //let keysCheck = validKeys |> Set.map(fun k -> adminPublicKeyChecker k)
    member this.ValidateKey key = keys.Contains key
    //member this.AddKey (key, signature) = 
