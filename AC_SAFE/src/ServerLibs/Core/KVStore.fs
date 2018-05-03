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
        | WriteAccessDenied
        | WriteTechIssue of string

    type ValueAccessIssue = 
        | NoDataExists 
        | ReadAccessDenied
        | DataIntegrityFailure
        | ReadTechIssue of string

    type ValueSlot = ValueSlot of string
    type KeySerializer<'TK> = {
        Serializer: 'TK -> string
        Deserializer: string -> 'TK 
    }

    [<Interface>]
    type IKVStore<'TK> = 
        abstract member Get : key: 'TK -> Task<Result<ValueSlot, ValueAccessIssue>>
        abstract member GetRange : keyPrefix: 'TK * pageSize: PageSize -> Task<IDictionary<'TK, Result<ValueSlot, ValueAccessIssue>>>
        abstract member GetRange : 'TK * 'TK * pageSize: PageSize -> Task<IDictionary<'TK, Result<ValueSlot, ValueAccessIssue>>>
        abstract member Put : key: 'TK * value: string -> Task<Result<unit, ValueSetIssue>>
        abstract member Put : keyValues: ('TK * string)[] -> Task<Result<unit, ValueSetIssue>>

    module PagedLog =
        type LogView = {
            GetCount:    unit -> Task<uint64>
            GetPage:     Pos -> PageSize -> Task<IDictionary<Pos, Result<ValueSlot, ValueAccessIssue>>>
            GetLastPage: PageSize -> Task<IDictionary<Pos, Result<ValueSlot, ValueAccessIssue>>>
        }
        
        type Log = {
            OfferAsync:  string -> Task<Pos> // TODO: Add error handling
            View:        LogView
        }
        
        type LogKey = 
            | Length of LogId: string
            | Item of LogId: string * Pos: Pos
        
        let createLogView (store: IKVStore<LogKey>) logId = task {
            let setLength (length: Pos) = task {
                let! putResult = store.Put(Length logId, length.ToString())
                match putResult with    | Ok _ -> () 
                                        | Error ValueAlreadySet -> ()
                                        | Error WriteAccessDenied -> failwith "AccessDenied write length" 
                                        | Error (WriteTechIssue err) -> failwith err
            }
            let getLength() = task {
                let! lengthResult = store.Get(Length logId)  
                let length = match lengthResult with  
                                | Ok v -> Some v
                                | Error NoDataExists -> None 
                                | Error ReadAccessDenied -> failwith "AccessDenied read length"
                                | Error DataIntegrityFailure -> failwith "DataIntegrityFailure read length" 
                                | Error (ReadTechIssue err) -> failwith err
                if length.IsNone then do! setLength 0UL
                return match length with 
                        | Some (ValueSlot vs) -> vs |> UInt64.Parse
                        | None -> 0UL
            }
            
            let extractPos = function   | Item (_, pos) -> pos
                                        | Length _ -> failwith "Inconsistent Data"
                                                    
            let getPage from pageSize = task { 
                let! page = store.GetRange (Item (logId, from), pageSize)
                return page 
                        |> Seq.map (fun kv -> extractPos kv.Key, kv.Value) 
                        |> Map.ofSeq
                        :> IDictionary<Pos, Result<ValueSlot, ValueAccessIssue>>
            }
            let getLastPage pageSize = task {
                let! length = getLength()
                return! 
                    if length <= uint64(pageSize) then getPage 0UL (uint32 length)
                    else getPage (length - uint64(pageSize) - 1UL) pageSize
            }

            let view = {   LogView.GetCount =   getLength
                           GetPage =            getPage
                           GetLastPage =        getLastPage }
                           
            let offer str = task {
                let! lastPos = getLength()
                let newPos = lastPos + 1UL
                let! _ = store.Put(Item (logId, newPos), str) // TODO: Handle error
                return newPos
            }

            return {    OfferAsync = offer
                        View       = view }
        }
            

    type IHashStore = IKVStore<Hash>

    let newLightningEnvironment envName =
        let env = new LightningEnvironment(envName)
        env.MaxDatabases <- 10
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
                let ar = ResizeArray<'TK * Result<ValueSlot, ValueAccessIssue>>()

                while i > 0u && hasCurrent do
                    let key = cursor.Current.Key |> toKey
                    let value = cursor.Current.Value |> getString |> fromJson<ValueSlot> |> Ok 
                    ar.Add(key, value)
                    hasCurrent <- cursor.MoveNext()
                    i <- i - 1u
                    
                ar  |> Map.ofSeq 
                    :> IDictionary<'TK, Result<ValueSlot, ValueAccessIssue>> 
                    |> Task.FromResult
           
            member __.GetRange (keyFrom, keyTo, pageSize) = 
                use tx = env.BeginTransaction(TransactionBeginFlags.ReadOnly)
                use db = tx.OpenDatabase(dbName)
                use cursor = tx.CreateCursor(db)
                let mutable i = pageSize
                let mutable hasCurrent = keyFrom |> keyBytes |> cursor.MoveToFirstAfter
                let ar = ResizeArray<'TK * Result<ValueSlot, ValueAccessIssue>>()

                while i > 0u && hasCurrent do
                    let key = cursor.Current.Key |> toKey
                    hasCurrent <- 
                        if (keyTo = key) then false
                        else 
                            let value = cursor.Current.Value |> getString |> fromJson<ValueSlot> |> Ok
                            ar.Add(key, value)
                            cursor.MoveNext()
                    i <- i - 1u
                    
                ar  |> Map.ofSeq 
                    :> IDictionary<'TK, Result<ValueSlot, ValueAccessIssue>> 
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
                return  if isNull result then Map.empty
                        else seq { for kv in result |> Seq.truncate (int pageSize) -> 
                                    kv.Key |> keySerializer.Deserializer, kv.Value |> fromJson<ValueSlot> |> Ok } 
                                |> Map.ofSeq
                        :> IDictionary<'TK, Result<ValueSlot, ValueAccessIssue>> 
            }
            member __.GetRange (keyFrom, keyTo, pageSize) = task {
                let! result = etcdClient.GetRange(keyBytes keyFrom, keyBytes keyTo)
                return  if isNull result then Map.empty
                        else seq { for kv in result |> Seq.truncate (int pageSize) -> 
                                    kv.Key |> keySerializer.Deserializer, kv.Value |> fromJson<ValueSlot> |> Ok } 
                                |> Map.ofSeq
                        :> IDictionary<'TK, Result<ValueSlot, ValueAccessIssue>> 
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