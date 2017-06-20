#r "../src/Akkling/bin/Release/Akka.dll"
#r "../src/Akkling/bin/Release/Hyperion.dll"
#r "../src/Akkling/bin/Release/Newtonsoft.Json.dll"
#r "../src/Akkling/bin/Release/FSharp.PowerPack.dll"
#r "../src/Akkling/bin/Release/FSharp.PowerPack.Linq.dll"
#r "../src/Akkling/bin/Release/Akkling.dll"
#r "../src/Akkling/bin/Release/System.Collections.Immutable.dll"

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