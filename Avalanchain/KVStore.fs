module Avalanchain.KVStore

[<Interface>]
type IKVStore = 
    abstract member Get<'TData> : Hash -> Hashed<'TData> option
    abstract member GetAll<'TData> : MerkleTree -> Merkled<'TData> list
