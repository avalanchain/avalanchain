#load "../.paket/load/net461/main.group.fsx"

#load "lib/ws.fs"

open System
open System.IO
#if INTERACTIVE
let cd = Path.Combine(__SOURCE_DIRECTORY__, "")
System.IO.Directory.SetCurrentDirectory(cd)
#endif


open System.IO

open Suave
open Suave.Sockets
open Suave.Sockets.Control
open Suave.WebSocket

open Akka.IO
open Akka.Streams
open Akka.Streams.Dsl
open Akkling
open Akkling.Streams
open Akkling.Persistence

open Avalanchain.WebSockets

let system = System.create "ac" <| Configuration.defaultConfig()
let mat = system.Materializer()

// server

type Pos = uint64


type ReplayMessage =
    | Page of From: Pos * PageSize: uint32  
    | GetMaxPos
    | GetState

type ReplayOk = ByteString[]

type ReplayError =
    | NotAllowed
    | OutOfBounds

type ReplayMessageResponse = Result<ReplayOk, ReplayError>

let echo = 
    Flow.id
    // |> Flow.map()
async {
    //let! server = 
    //    system.TcpStream()
    //    |> Tcp.bindAndHandle mat "localhost" 5000 echo

    let handler = Sink.forEach (fun (conn: Tcp.IncomingConnection) ->
        printfn "New client connected (local: %A, remote: %A)" conn.LocalAddress conn.RemoteAddress
        conn.HandleWith(echo, mat))

    let! server =
        system.TcpStream()
        |> Tcp.bind "localhost" 5000
        |> Source.toMat handler Keep.left
        |> Graph.run mat

    printfn "TCP server listetning on %A" server.LocalAddress
    Console.ReadLine() |> ignore

    do! server.AsyncUnbind()
} |> Async.RunSynchronously

// client
open Akka.IO

let parser = 
    Flow.id
    |> Flow.takeWhile ((<>) "q")
    |> Flow.concat (Source.singleton "BYE")
    |> Flow.map (fun x -> ByteString.FromString(x + "\n"))


let repl = 
    Framing.delimiter true 256 (ByteString.FromString("\n"))
    |> Flow.map string
    |> Flow.iter (printfn "Server: %s")
    |> Flow.map (fun _ -> Console.ReadLine())
    |> Flow.via parser
    
async {
    let! client = 
        system.TcpStream()
        |> Tcp.outgoing "localhost" 5000 
        |> Flow.join repl
        |> Graph.run mat

    printfn "Client connected (local: %A, remote: %A)" client.LocalAddress client.RemoteAddress
} |> Async.RunSynchronously