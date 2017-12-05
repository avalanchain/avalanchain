open Akka.Streams.Dsl

// server
let echo = Flow.id
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