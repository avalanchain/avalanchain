#r "../bin/Avalanchain/Akka.dll"
#r "../bin/Avalanchain/Hyperion.dll"
#r "../bin/Avalanchain/Newtonsoft.Json.dll"
#r "../bin/Avalanchain/FSharp.PowerPack.dll"
#r "../bin/Avalanchain/FSharp.PowerPack.Linq.dll"
#r "../bin/Avalanchain/Akkling.dll"
#r "../bin/Avalanchain/System.Collections.Immutable.dll"

open System
open Akkling
open Akka.Actor

let system = System.create "basic-sys" <| Configuration.defaultConfig()

let aref = spawn system "hello-actor" <| props(fun m ->
    let rec loop () = actor {
        let! (msg: obj) = m.Receive ()
        match msg with
        | LifecycleEvent e ->
            match e with
            | PreStart -> printfn "Actor %A has started" m.Self
            | PostStop -> printfn "Actor %A has stopped" m.Self
            | _ -> return Unhandled
        | x -> printfn "%A" x
        return! loop ()
    }
    loop ())

let sref = retype aref
sref <! "ok"
(retype aref) <! PoisonPill.Instance