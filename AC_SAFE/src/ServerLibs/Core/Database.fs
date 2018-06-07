namespace Avalanchain.Core

module Database =

    open System
    open System.Diagnostics
    open System.Data.Common
    open System.Collections.Generic
    open System.Threading.Tasks
    open Microsoft.Data.Sqlite
    open FSharp.Control.Tasks
    open FSharp.Control.Tasks.ContextInsensitive
    open Dapper

    let inline (=>) k v = k, box v

    let execute (connection:#DbConnection) (sql:string) (data:_) =
        task {
            try
                let! res = connection.ExecuteAsync(sql, data)
                return Ok res
            with
            | ex -> return Error ex
        }

    let query (connection:#DbConnection) (sql:string) (parameters:IDictionary<string, obj> option) =
        task {
            try
                let! res =
                    match parameters with
                    | Some p -> connection.QueryAsync<'T>(sql, p)
                    | None -> connection.QueryAsync<'T>(sql)
                return Ok res
            with
            | ex -> return Error ex
        }

    let querySingle (connection:#DbConnection) (sql:string) (parameters:IDictionary<string, obj> option) =
        task {
            try
                let! res =
                    match parameters with
                    | Some p -> connection.QuerySingleOrDefaultAsync<'T>(sql, p)
                    | None -> connection.QuerySingleOrDefaultAsync<'T>(sql)
                return
                    if isNull (box res) then Ok None
                    else Ok (Some res)

            with
            | ex -> return Error ex
        }

    let queryFirst (connection:#DbConnection) (sql:string) (parameters:IDictionary<string, obj> option) =
        task {
            try
                let! res =
                    match parameters with
                    | Some p -> connection.QueryFirstAsync<'T>(sql, p)
                    | None -> connection.QueryFirstAsync<'T>(sql)
                return
                    if isNull (box res) then Ok None
                    else Ok (Some res)

            with
            | ex -> return Error ex
        }

    type Pos = uint64
      
    type ITableName = 
        abstract TableName: string 
    let tableName tn = { new ITableName with member __.TableName = tn }

    type [<CLIMutable>] ChainItem = {
        Id: Pos
        Hash: string
        Data: string
        TableName: ITableName
    }

    let connect connectionString = 
        let connection = new SqliteConnection(connectionString)
        connection.Open()
        connection

    let createTable connection (tableName: ITableName) = 
        execute connection ("CREATE TABLE IF NOT EXISTS `" + tableName.TableName + "` ( `Id` INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT UNIQUE, `Hash` TEXT NOT NULL, `Data` TEXT NOT NULL )") None
        
    let prepareDatabase connection =
        createTable connection (tableName "Stream")

    let insert connection (v: ChainItem): Task<Result<int,exn>> =
        //execute connection "INSERT INTO Stream(id, hash, data) VALUES (@id, @password, @login)" v
        execute connection ("INSERT INTO " + v.TableName.TableName + "(hash, data) VALUES (@Hash, @Data)") v

    let createChainItems n =
        let rnd = System.Random()
        let rndStr n =
            let bts = Array.zeroCreate n
            rnd.NextBytes bts 
            bts |> System.Convert.ToBase64String
        [| for i in 1UL .. n -> { Id = i; Hash = rndStr 64; Data = rndStr 1024; TableName = tableName "Streams" }|]

    let insertBatch (connection: #DbConnection) (items: ChainItem seq) = 
        let transaction = connection.BeginTransaction()
        let sw = Stopwatch()
        sw.Start()
        for item in items do
            connection.ExecuteAsync("INSERT INTO " + item.TableName.TableName + "(hash, data) VALUES (@Hash, @Data)", item, transaction) |> ignore
        transaction.Commit()
        transaction.Dispose()
        sw.Stop()
        printfn "batch inserts: '%A'" sw.Elapsed

    let insertBatchAsync (connection: #DbConnection) (items: ChainItem seq) = task {
        use transaction = connection.BeginTransaction()
        let sw = Stopwatch()
        sw.Start()
        for item in items do
          let! _ = connection.ExecuteAsync("INSERT INTO " + item.TableName.TableName + "(hash, data) VALUES (@Hash, @Data)", item, transaction)
          ()
        transaction.Commit()
        sw.Stop()
        printfn "batch inserts: '%A'" sw.Elapsed
    }    


    let insert10000 connection tn = task {
        let sw = Stopwatch()
        sw.Start()
        for i in 1UL .. 10000UL do
          let v = { Id = i; Hash = "H" + i.ToString(); Data = "D" + i.ToString(); TableName = tableName tn }
          let! _ = insert connection v
          ()
        sw.Stop()
        printfn "1b1 inserts: '%A'" sw.Elapsed
    }

    let getById connection (tableName: ITableName) id : Task<Result<ChainItem option, exn>> =
        querySingle connection ("SELECT id, hash, data FROM " + tableName.TableName + " WHERE id=@id") (Some <| dict ["id" => id])

    let getMaxId connection (tableName: ITableName) : Task<Result<UInt64 option, exn>> = task {
        let! (nullable: Result<Nullable<UInt64> option, exn>) = queryFirst connection ("SELECT MAX(id) FROM " + tableName.TableName) None
        return nullable |> Result.map (Option.map (Option.ofNullable) >> Option.flatten)
    }

    let getPage connection (tableName: ITableName) startId pageSize : Task<Result<ChainItem seq, exn>> =
        query connection ("SELECT id, hash, data FROM " + tableName.TableName + " WHERE id>=@startId AND id<@startId + @pageSize") (Some <| dict ["startId" => startId; "pageSize" => pageSize])

    //   let getAll connectionString : Task<Result<User seq, exn>> =
    //     use connection = new SqliteConnection(connectionString)
    //     query connection "SELECT id, password, login FROM Users" None

    //   let getById connectionString id : Task<Result<User option, exn>> =
    //     use connection = new SqliteConnection(connectionString)
    //     querySingle connection "SELECT id, password, login FROM Users WHERE id=@id" (Some <| dict ["id" => id])

    //   let update connectionString v : Task<Result<int,exn>> =
    //     use connection = new SqliteConnection(connectionString)
    //     execute connection "UPDATE Users SET id = @id, password = @password, login = @login WHERE id=@id" v

    //   let insert connectionString v : Task<Result<int,exn>> =
    //     use connection = new SqliteConnection(connectionString)
    //     execute connection "INSERT INTO Users(id, password, login) VALUES (@id, @password, @login)" v

    //   let delete connectionString id : Task<Result<int,exn>> =
    //     use connection = new SqliteConnection(connectionString)
    //     execute connection "DELETE FROM Users WHERE id=@id" (dict ["id" => id])
