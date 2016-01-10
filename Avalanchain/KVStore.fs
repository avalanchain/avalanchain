module Avalanchain.KVStore

open SecKeys

[<Interface>]
type IKVStore = 
    abstract member Get<'TData> : Hash -> Hashed<'TData> option
    abstract member GetAll<'TData> : MerkleTree -> Merkled<'TData> list

type PublicKeysStore = {
    ValidateKey: PublicKey -> bool
    AllValidKeys: PublicKey list
}
    