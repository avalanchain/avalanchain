#load "../.paket/load/net461/main.group.fsx"

#load "lib/ws.fs"

open System
open System.IO
#if INTERACTIVE
let cd = Path.Combine(__SOURCE_DIRECTORY__, "")
System.IO.Directory.SetCurrentDirectory(cd)
#endif


open Suave
open Suave.Sockets
open Suave.Sockets.Control
open Suave.WebSocket

open Akka.IO
open Akka.Streams
open Akkling
open Akkling.Streams
open Akkling.Persistence

open Avalanchain.WebSockets

let system = System.create "streams-sys" <| Configuration.defaultConfig()
let mat = system.Materializer()

let wsPaths =
    [   "/ws1", fun (conn: IncomingConnection) -> 
                    printfn "Connected ws1" 
                    printfn "from: '%A' to: '%A'" conn.RemoteAddress conn.LocalAddress
                    let echo = Flow.id
                    // let echo = Framing.delimiter true 256 (ByteString.FromString("\n"))
                    //             |> Flow.map string
                    //             |> Flow.map (fun s -> s + " ws1")
                    //             |> Flow.iter (printfn "Server: %s")
                    //             |> Flow.map ByteString.FromString
                    conn.Flow |> Flow.join echo |> Graph.run mat |> ignore
        "/ws2", fun (conn: IncomingConnection) -> 
                    printfn "Connected ws2" 
                    printfn "from: '%A' to: '%A'" conn.RemoteAddress conn.LocalAddress
                    let echo =  Flow.id
                                |> Flow.map string
                                |> Flow.map (fun s -> s + " ws2")
                                |> Flow.iter (printfn "Server: %s")
                                |> Flow.map ByteString.FromString
                    conn.Flow |> Flow.join echo |> Graph.run mat |> ignore
    ] |> Map.ofList
webSocketServer { defaultConfig with bindings = [ HttpBinding.createSimple HTTP "127.0.0.1" 8082 ]} wsPaths mat
