#r "../bin/Avalanchain/Akka.dll"
#r "../bin/Avalanchain/Hyperion.dll"
#r "../bin/Avalanchain/Newtonsoft.Json.dll"
#r "../bin/Avalanchain/FSharp.PowerPack.dll"
#r "../bin/Avalanchain/FSharp.PowerPack.Linq.dll"
#r "../bin/Avalanchain/Akkling.dll"
#r "../bin/Avalanchain/System.Collections.Immutable.dll"
#r "../bin/Avalanchain/Google.ProtocolBuffers.dll"
#r "../bin/Avalanchain/Akka.Persistence.dll"
#r "../bin/Avalanchain/Akkling.Persistence.dll"

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
