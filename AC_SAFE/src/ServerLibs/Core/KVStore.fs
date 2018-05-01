namespace Avalanchain.Core


module KVStore =
    open LightningDB
    open Crypto
    open ChainDefs

    type ValueSetIssue =
        | ValueAlreadySet
        | AccessDenied

    type ValueAccessIssue = 
        | NoDataExists 
        | AccessDenied
        | DataIntegrityFailure
        | TemporalTechProblem of string

    type ValueSlot = ValueSlot of string

    [<Interface>]
    type IKVStore<'TK> = 
        abstract member Get : 'TK -> Result<ValueSlot, ValueAccessIssue>
        abstract member Put : 'TK -> string -> Result<unit, ValueAccessIssue>

    type IHashStore = IKVStore<Hash>

    let newLightningEnvironment envName =
        let env = new LightningEnvironment(envName)
        env.MaxDatabases <- 5
        env.MapSize <- 1073741824L
        env.Open()
        env


    type LmdbKVStore<'TK>(keySerializer: 'TK -> string, env: LightningEnvironment, dbName, putOptions: PutOptions) =

        interface IKVStore<'TK> with
            member __.Get key =
                use tx = env.BeginTransaction(TransactionBeginFlags.ReadOnly)
                use db = tx.OpenDatabase(dbName)
                let keyBytes = key |> keySerializer |> getBytes
                tx.Get(db, keyBytes) |> getString |> fromJson<ValueSlot> |> Ok // TODO: Add error handling
            //: 'TK -> Result<ValueSlot, ValueAccessIssue>
            member __.Put key value = 
                use tx = env.BeginTransaction()
                use db = tx.OpenDatabase(dbName, new DatabaseConfiguration (Flags = DatabaseOpenFlags.Create ))
                let keyBytes = key |> keySerializer |> getBytes
                tx.Put(db, keyBytes, getBytes value, putOptions)
                tx.Commit() |> Ok
            //: 'TK -> string -> Result<unit, ValueAccessIssue>
