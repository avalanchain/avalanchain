#load "../.paket/load/net461/main.group.fsx"


open Suave
open Suave.Http
open Suave.Operators
open Suave.Filters
open Suave.Successful
open Suave.Files
open Suave.RequestErrors
open Suave.Logging
open Suave.Utils

open System
open System.Net
open System.Text

open Suave.Sockets
open Suave.Sockets.Control
open Suave.WebSocket

open Akka.IO
open Akka.Streams
open Akkling
open Akkling.Streams
open Akkling.Persistence

type IncomingConnection = Akka.Streams.Dsl.Tcp.IncomingConnection


// let handler = Sink.forEach (fun (conn: IncomingConnection) ->
//     printfn "New client connected (local: %A, remote: %A)" conn.LocalAddress conn.RemoteAddress
//     conn.HandleWith(echo, mat))

// let! server =
//     system.TcpStream()
//     |> Tcp.bind "localhost" 5000
//     |> Source.toMat handler Keep.left
//     |> Graph.run mat

let webSocketSink (webSocket : WebSocket) = //(errorQueue: ISourceQueue<Error>) = 
    //Sink.forEach(fun (m: ByteString) -> webSocket.send Text (m.ToArray() |> ByteSegment) true)
    Flow.id
    |> Flow.asyncMap 1 (fun (m: ByteString) -> webSocket.send Text (m.ToArray() |> ByteSegment) true)
    |> Flow.map(fun r -> match r with    
                            | Choice1Of2 () -> None
                            | Choice2Of2 (error: Error) -> Some error)
    |> Flow.iter(printfn "%A")
    |> Flow.toMat (Sink.forEach (printfn "Error enqueue result: %A")) Keep.left
    // |> Flow.choose id
    // |> Flow.asyncMap 1 (errorQueue.AsyncOffer)
    // |> Flow.toMat (Sink.forEach (printfn "Error enqueue result: %A")) Keep.left

let webSocketSource (webSocket : WebSocket) =
    let emptyResponse = [||] |> ByteSegment
    Source.queue OverflowStrategy.DropNew 1000 
    |> Source.mapMaterializedValue(
        fun queue -> socket {
            let mutable loop = true
            while loop do 
                let! msg = webSocket.read() 
                printfn "Received msg: %A" msg
                match msg with
                    | (Text, data, true) -> 
                        do! async { let! _ = data |> ByteString.FromBytes |> queue.AsyncOffer 
                                    return Choice1Of2 () }
                    | (Ping, _, _) -> 
                        do! webSocket.send Pong emptyResponse true
                    | (Close, _, _) ->
                        // after sending a Close message, stop the loop
                        do! webSocket.send Close emptyResponse true
                    | _ -> ()
       })


let webSocketServer config (wsPath: string) =
    Source.queue OverflowStrategy.DropNew 1000 
    |> Source.mapMaterializedValue(
        fun connQueue ->

            let app : WebPart = 
                choose [
                    path wsPath >=> handShake (fun webSocket (context: HttpContext) -> 
                                                socket {
                                                    do! async { let! qr = connQueue.AsyncOffer(webSocket, context)
                                                                qr |> ignore 
                                                                return Choice1Of2 () }
                                                })  
                    GET >=> choose [ path "/" >=> OK wsPath ]
                    NOT_FOUND "Found no handlers." 
                ]
            let server = async { startWebServer { config with logger = Targets.create Verbose [||] } app } |> Async.Start 
            server
        )
    |> Source.map(
        fun (webSocket, context) ->
                
            let localEP = IPEndPoint(context.connection.ipAddr, context.connection.port |> int)
            let remoteEP = IPEndPoint(context.clientIpTrustProxy, context.clientPortTrustProxy |> int)
            let ic = IncomingConnection(localEP, remoteEP, 
                        Flow.ofSinkAndSourceMat (webSocketSink webSocket) (fun _ _ -> Akka.NotUsed.Instance) (webSocketSource webSocket))
            ic
    )


let system = System.create "streams-sys" <| Configuration.defaultConfig()
let mat = system.Materializer()


// webSocketServer { defaultConfig with bindings = [ HttpBinding.createSimple HTTP "127.0.0.1" 8082 ]} "/ws1"
// |> Source.runForEach mat (fun conn -> printfn "Connected: %A" conn)

let server =
    webSocketServer { defaultConfig with bindings = [ HttpBinding.createSimple HTTP "127.0.0.1" 8082 ]} "/ws1"
    |> Source.runForEach mat (fun conn -> 
                                    printfn "Connected" 
                                    printfn "from: '%A' to: '%A'" conn.RemoteAddress conn.LocalAddress
                                    let echo = Framing.delimiter true 256 (ByteString.FromString("\n"))
                                                |> Flow.map string
                                                |> Flow.iter (printfn "Server: %s")
                                                |> Flow.map ByteString.FromString
                                    conn.Flow |> Flow.join echo |> Graph.run mat |> ignore
                                    printfn "Flow matted"
                                    )
