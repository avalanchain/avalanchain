#load "../.paket/load/net461/main.group.fsx"

// #load "lib/ws.fs"

open System
open System.IO
#if INTERACTIVE
let cd = Path.Combine(__SOURCE_DIRECTORY__, "bin/Debug/net461")
System.IO.Directory.SetCurrentDirectory(cd)
#I "bin/Debug/net461"
#endif


open Akka.IO
open Akka.Streams
open Akka.Streams.Dsl
open Akkling
open Akkling.Streams

let system = System.create "streams-sys" <| Configuration.defaultConfig()
let mat = system.Materializer()


// server
let echo remoteAddress = 
    Framing.delimiter true 256 (ByteString.FromString("\n"))
    |> Flow.map string
    |> Flow.iter (printfn "Client %A: %s" remoteAddress)
    |> Flow.merge (Source.singleton "Hello from Server")
    |> Flow.takeWhile ((<>) "BYE")
    |> Flow.concat (Source.singleton "Hello from Server")
    |> Flow.map (fun x -> ByteString.FromString(x + "\n"))


let startServer = async {
    let handler = Sink.forEach (fun (conn: Tcp.IncomingConnection) ->
        printfn "New client connected (local: %A, remote: %A)" conn.LocalAddress conn.RemoteAddress
        conn.HandleWith(echo conn.RemoteAddress, mat)
        // printfn "Client disconnected (local: %A, remote: %A)" conn.LocalAddress conn.RemoteAddress
        )

    let! server =
        system.TcpStream()
        |> Tcp.bind "127.0.0.1" 5000
        |> Source.toMat handler Keep.left
        |> Graph.run mat

    printfn "TCP server listetning on %A" server.LocalAddress
    Console.ReadLine() |> ignore

    do! server.AsyncUnbind()
} 

// client

let parser: Flow<string, ByteString, Akka.NotUsed> = 
    Flow.id
    |> Flow.takeWhile ((<>) "q")
    |> Flow.concat (Source.singleton "BYE" |> Source.mapMaterializedValue(fun _ -> Akka.NotUsed.Instance))
    |> Flow.map (fun x -> ByteString.FromString(x + "\n"))


let repl: Flow<ByteString, ByteString, Akka.NotUsed> = 
    Framing.delimiter true 256 (ByteString.FromString("\n"))
    |> Flow.map string
    |> Flow.iter (printfn "Server: %s")
    |> Flow.map (fun _ -> Console.ReadLine())
    |> Flow.viaMat parser Keep.right
    
let startClient = async {
    let! client = 
        system.TcpStream()
        |> Tcp.outgoing "127.0.0.1" 5000 
        |> Flow.join repl
        |> Graph.run mat

    printfn "Client connected (local: %A, remote: %A)" client.LocalAddress client.RemoteAddress
} 


let server = startServer |> Async.RunSynchronously
let client = startClient |> Async.RunSynchronously
