namespace Avalanchain.Core


module KVStore =
    open System
    open System.Collections.Generic
    open System.Threading.Tasks
    open FSharp.Control.Tasks
    open LightningDB
    open EtcdGrpcClient
    open Crypto
    open ChainDefs

    type ValueSetIssue =
        | ValueAlreadySet
        | AccessDenied
        | WriteTechIssue of string

    type ValueAccessIssue = 
        | NoDataExists 
        | AccessDenied
        | DataIntegrityFailure
        | ReadTechIssue of string

    type ValueSlot = ValueSlot of string
    type KeySerializer<'TK> = {
        Serializer: 'TK -> string
        Deserializer: string -> 'TK 
    }

    [<Interface>]
    type IKVStore<'TK> = 
        abstract member Get : 'TK -> Task<Result<ValueSlot, ValueAccessIssue>>
        abstract member GetRange : 'TK * uint32 -> Task<Result<IDictionary<'TK, ValueSlot>, ValueAccessIssue>>
        abstract member GetRange : 'TK * 'TK * uint32  -> Task<Result<IDictionary<'TK, ValueSlot>, ValueAccessIssue>>
        abstract member Put : 'TK * string -> Task<Result<unit, ValueSetIssue>>
        abstract member Put : ('TK * string)[] -> Task<Result<unit, ValueSetIssue>>

    type IHashStore = IKVStore<Hash>

    let newLightningEnvironment envName =
        let env = new LightningEnvironment(envName)
        env.MaxDatabases <- 5
        env.MapSize <- 1073741824L
        env.Open()
        env


    type LmdbKVStore<'TK when 'TK: comparison>(keySerializer: KeySerializer<'TK>, env: LightningEnvironment, dbName, putOptions: PutOptions) =
        let keyBytes = keySerializer.Serializer >> getBytes
        let valueBytes = ValueSlot >> toJson >> getBytes
        let toKey = getString >> keySerializer.Deserializer
        interface IKVStore<'TK> with
            member __.Get key =
                use tx = env.BeginTransaction(TransactionBeginFlags.ReadOnly)
                use db = tx.OpenDatabase(dbName)
                let found, result = tx.TryGet(db, keyBytes key) // TODO: Add error handling
                if found then result |> getString |> fromJson<ValueSlot> |> Ok
                else NoDataExists |> Error
                |> Task.FromResult
            member __.GetRange (keyPrefix, pageSize) = 
                use tx = env.BeginTransaction(TransactionBeginFlags.ReadOnly)
                use db = tx.OpenDatabase(dbName)
                use cursor = tx.CreateCursor(db)
                let mutable i = pageSize
                let mutable hasCurrent = keyPrefix |> keyBytes |> cursor.MoveToFirstAfter
                let ar = ResizeArray<'TK * ValueSlot>()

                while i > 0u && hasCurrent do
                    let key = cursor.Current.Key |> toKey
                    let value = cursor.Current.Value |> getString |> fromJson<ValueSlot>
                    ar.Add(key, value)
                    hasCurrent <- cursor.MoveNext()
                    i <- i - 1u
                    
                ar  |> Map.ofSeq 
                    :> IDictionary<'TK, ValueSlot> 
                    |> Ok 
                    |> Task.FromResult
           
            member __.GetRange (keyFrom, keyTo, pageSize) = 
                use tx = env.BeginTransaction(TransactionBeginFlags.ReadOnly)
                use db = tx.OpenDatabase(dbName)
                use cursor = tx.CreateCursor(db)
                let mutable i = pageSize
                let mutable hasCurrent = keyFrom |> keyBytes |> cursor.MoveToFirstAfter
                let ar = ResizeArray<'TK * ValueSlot>()

                while i > 0u && hasCurrent do
                    let key = cursor.Current.Key |> toKey
                    hasCurrent <- 
                        if (keyTo = key) then false
                        else 
                            let value = cursor.Current.Value |> getString |> fromJson<ValueSlot>
                            ar.Add(key, value)
                            cursor.MoveNext()
                    i <- i - 1u
                    
                ar  |> Map.ofSeq 
                    :> IDictionary<'TK, ValueSlot> 
                    |> Ok 
                    |> Task.FromResult
            member __.Put (key, value) = 
                use tx = env.BeginTransaction()
                use db = tx.OpenDatabase(dbName, new DatabaseConfiguration (Flags = DatabaseOpenFlags.Create ))
                tx.Put(db, keyBytes key, valueBytes value, putOptions)
                tx.Commit() |> Ok
                |> Task.FromResult
            member __.Put keyValues = 
                use tx = env.BeginTransaction()
                use db = tx.OpenDatabase(dbName, new DatabaseConfiguration (Flags = DatabaseOpenFlags.Create ))
                for (key, value) in keyValues do
                    tx.Put(db, keyBytes key, valueBytes value, putOptions)
                tx.Commit() |> Ok
                |> Task.FromResult

   
    type EtcdKVStore<'TK when 'TK: comparison>(keySerializer: KeySerializer<'TK>, etcdClient: EtcdClient) =
        let keyBytes = keySerializer.Serializer 
        let valueBytes = ValueSlot >> toJson
        interface IKVStore<'TK> with
            member __.Get key = task {
                let! result = etcdClient.Get(keyBytes key)
                return  if isNull result then NoDataExists |> Error
                        else result |> fromJson<ValueSlot> |> Ok
            }
            member __.GetRange (keyPrefix, pageSize) = task {
                let! result = etcdClient.GetRange(keyBytes keyPrefix)
                return  if isNull result then NoDataExists |> Error
                        else seq { for kv in result |> Seq.truncate (int pageSize) -> 
                                    kv.Key |> keySerializer.Deserializer, kv.Value |> fromJson<ValueSlot> } 
                                |> Map.ofSeq
                                :> IDictionary<'TK, ValueSlot> 
                                |> Ok
            }
            member __.GetRange (keyFrom, keyTo, pageSize) = task {
                let! result = etcdClient.GetRange(keyBytes keyFrom, keyBytes keyTo)
                return  if isNull result then NoDataExists |> Error
                        else seq { for kv in result |> Seq.truncate (int pageSize) -> 
                                    kv.Key |> keySerializer.Deserializer, kv.Value |> fromJson<ValueSlot> } 
                                |> Map.ofSeq
                                :> IDictionary<'TK, ValueSlot> 
                                |> Ok
            }
            member __.Put (key, value) = task {
                let! result = etcdClient.Put(keyBytes key, value)
                return  if result |> isNull |> not then () |> Ok
                        else WriteTechIssue "No response" |> Error 
            }
            member __.Put keyValues = task {
                for (key, value) in keyValues do
                    let! result = etcdClient.Put(keyBytes key, value) // TODO: Add per-key error reporting
                    ()
                return  () |> Ok
            }