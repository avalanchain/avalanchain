module ServerTests.Tests

open Expecto
open System
open Proto
open Proto.FSharp
open Proto.FSharp.Persistence
open Proto.Persistence.Sqlite
open Microsoft.Data.Sqlite
open FSharp.Control.Reactive
open FSharp.Control.Reactive.Builders
open Avalanchain.Core.Crypto
open Avalanchain.Core.Persistence
open System.Reactive.Linq
// open ServerCode
// open ServerCode.Storage

let coreTests =
  testList "Core" [
    testList "Crypto" [
      testCase "generate keys, sign and verify simple string" <| fun _ -> 
        let ctx = CryptoContext.generate()
        let testStr = "Test string"
        let token = sign ctx testStr
        Expect.isSome token "Signing failed"

        let verificationResult = verify ctx token.Value
        Expect.isSome verificationResult "Verification failed"
    ]

    testList "Persistence" [
      testCase "create and read 10 events" <| fun _ -> 
        let dataStore = "dataStore.db"
        if IO.File.Exists dataStore then 
          IO.File.Delete dataStore
        let eventStore = SqliteProvider(SqliteConnectionStringBuilder ( DataSource = dataStore ))
        let eventSourcing = EventSourcing.persistLight eventStore "0" |> Actor.spawnProps

        let msgs = [| for i in 0 .. 9 -> "Item" + i.ToString() |] 
        
        msgs |> Array.iter (fun m -> m >! eventSourcing)

        Threading.Thread.Sleep 100

        let storedMsgs = ResizeArray<_>()
        eventStore 
        |> getEvents<string> (fun e -> printfn "E %A" e; storedMsgs.Add e) "0" 0L 9L 
        |> Async.RunSynchronously 
        |> ignore

        // Expect.equal (storedMsgs.ToArray()) msgs ""

        Threading.Thread.Sleep 2000

      testCase "create and read 10 events via Observable" <| fun _ -> 
        let dataStore = "dataStore1.db"
        if IO.File.Exists dataStore then IO.File.Delete dataStore
        let eventStore = SqliteProvider(SqliteConnectionStringBuilder ( DataSource = dataStore ))
        let eventSourcing = EventSourcing.persistLight eventStore "0" |> Actor.spawnProps

        let msgs = [| for i in 0 .. 9 -> "Item" + i.ToString() |] 
        
        msgs |> Array.iter (fun m -> m >! eventSourcing)

        let mutable storedMsgs = [||]
        eventStore 
        |> getEventsObservable<string> "0" 0L 9L 
        |> Observable.toArray 
        |> Observable.subscribe (fun msgs -> printfn "Msgs: %A" msgs)
        |> ignore

        eventSourcing <! PoisonPill
    ]  
  ]