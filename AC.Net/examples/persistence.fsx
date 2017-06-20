#r "../src/Akkling/bin/Release/Akka.dll"
#r "../src/Akkling/bin/Release/Hyperion.dll"
#r "../src/Akkling/bin/Release/Newtonsoft.Json.dll"
#r "../src/Akkling/bin/Release/FSharp.PowerPack.dll"
#r "../src/Akkling/bin/Release/FSharp.PowerPack.Linq.dll"
#r "../src/Akkling/bin/Release/Akkling.dll"
#r "../src/Akkling/bin/Release/System.Collections.Immutable.dll"
#r "../src/Akkling.Persistence/bin/Release/Google.ProtocolBuffers.dll"
#r "../src/Akkling.Persistence/bin/Release/Akka.Persistence.dll"
#r "../src/Akkling.Persistence/bin/Release/Akkling.Persistence.dll"

open System
open Akkling
open Akkling.Persistence

let system = System.create "persisting-sys" <| Configuration.defaultConfig()

type CounterChanged =
    { Delta : int }

type CounterCommand =
    | Inc
    | Dec
    | GetState

type CounterMessage =
    | Command of CounterCommand
    | Event of CounterChanged

let counter =
    spawn system "counter-1" <| propsPersist(fun mailbox ->
        let rec loop state =
            actor {
                let! msg = mailbox.Receive()
                match msg with
                | Event(changed) -> return! loop (state + changed.Delta)
                | Command(cmd) ->
                    match cmd with
                    | GetState ->
                        mailbox.Sender() <! state
                        return! loop state
                    | Inc -> return Persist (Event { Delta = 1 })
                    | Dec -> return Persist (Event { Delta = -1 })
            }
        loop 0)

counter <! Command Inc
counter <! Command Inc
counter <! Command Dec
async { let! reply = counter <? Command GetState
        printfn "Current state of %A: %i" counter reply.Value } |> Async.RunSynchronously
