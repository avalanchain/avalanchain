#r "../bin/Avalanchain/Akka.dll"
#r "../bin/Avalanchain/Hyperion.dll"
#r "../bin/Avalanchain/Newtonsoft.Json.dll"
#r "../bin/Avalanchain/FSharp.PowerPack.dll"
#r "../bin/Avalanchain/FSharp.PowerPack.Linq.dll"
#r "../bin/Avalanchain/Akkling.dll"
#r "../bin/Avalanchain/System.Collections.Immutable.dll"

open System
open Akkling
open Akkling.IO
open Akkling.IO.Tcp
open System.Net

let system = System.create "telnet-sys" <| Configuration.defaultConfig()

let handler connection = fun (ctx: Actor<obj>) ->
    monitor ctx connection |> ignore
    let rec loop () = actor {
        let! msg = ctx.Receive ()
        match msg with
        | Received(data) ->
            printfn "%s" (data.DecodeString())
            return! loop ()
        | Terminated(_, _,_) | ConnectionClosed(_) -> return Stop
        | _ -> return Unhandled
    }
    loop ()

let endpoint = IPEndPoint(IPAddress.Loopback, 5000)
let listener = spawn system "listener" <| props(fun m ->
    IO.Tcp(m) <! TcpMessage.Bind(m.Self, endpoint, 100)
    let rec loop () = actor {
        let! (msg: obj) = m.Receive ()
        match msg with
        | Connected(remote, local) ->
            let conn = m.Sender ()
            conn <! TcpMessage.Register(spawn m null (props(handler conn)))
            return! loop ()
        | _ -> return Unhandled
    }
    loop ())
