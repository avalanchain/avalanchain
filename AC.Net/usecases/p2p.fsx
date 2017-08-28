#r "../bin/Avalanchain/Akka.dll"
#r "../bin/Avalanchain/Hyperion.dll"
#r "../bin/Avalanchain/Newtonsoft.Json.dll"
#r "../bin/Avalanchain/FSharp.PowerPack.dll"
#r "../bin/Avalanchain/FSharp.PowerPack.Linq.dll"
#r "../bin/Avalanchain/Akkling.dll"
#r "../bin/Avalanchain/Reactive.Streams.dll"
#r "../bin/Avalanchain/Akka.Streams.dll"
#r "../bin/Avalanchain/Akkling.Streams.dll"
#r "../bin/Avalanchain/System.Collections.Immutable.dll"


open System
open System.Text
open System.Threading.Tasks
open Akka
open Akka.Actor
open Akka.IO
open Akka.Streams
open Akka.Streams.Dsl
open Akkling
open Akkling.Streams

// open Tcp = Akka.Streams.Dsl.Tcp
// open TcpExt = Akka.Streams.Dsl.TcpExt

let systemServer = System.create "ac" <| Configuration.defaultConfig()
let matServer = systemServer.Materializer()

// Source.ofArray (text.Split())
// |> Source.map (fun x -> x.ToUpper())
// |> Source.filter (String.IsNullOrWhiteSpace >> not)
// |> Source.runForEach mat (printfn "%s")
// |> Async.RunSynchronously

let echo = 
    // Flow.map(fun bs -> printfn "ser: %A" bs; bs) 
    // |> Flow.map(fun bs -> )
    Framing.Delimiter(ByteString.FromString("\n"), 256, true)
    |> Flow.map(fun bytes -> Encoding.UTF8.GetString(bytes.ToArray()))
    |> Flow.map(fun s -> printfn "Server received: '%s'" s; s)
    |> Flow.map(fun s -> s + "\n" |> ByteString.FromString)
    
    
let startServer addr port = async {
    let! sb = systemServer.TcpStream().BindAndHandle(echo, matServer, addr, port) |> Async.AwaitTask
    printfn "Server bound to: %A" sb.LocalAddress
    //return sb
}        


// let start addr port = 
//     (connections addr port)
//     |> Source.runWith matServer (Sink.forEach (fun (c: Tcp.IncomingConnection) -> 
//                                                 printfn "Connected: %A" c.RemoteAddress
//                                                 c.HandleWith(Flow.empty |> echo, matServer)))

let address, port = "127.0.0.1", 3000

startServer address port
|> Async.Start



let systemClient = System.create "ac" <| Configuration.defaultConfig()
let matClient = systemClient.Materializer()

let connect addr port = systemClient.TcpStream().OutgoingConnection(addr, port)


let sink() =
    Framing.Delimiter(ByteString.FromString("\n"), 256, true)
    |> Flow.map(fun bytes -> Encoding.UTF8.GetString(bytes.ToArray()))
    |> Flow.toMat (Sink.forEach (printfn "%A")) Keep.none

let source() = 
    ["Hello"; "World"] 
    |> Source.ofList
    |> Source.map(fun s -> s + "\n" |> ByteString.FromString)

let flowClient() = Flow.FromSinkAndSource(sink(), source())
// let flowClient() = Flow.empty

async {
    try
        let! conn = (connect "localhost" port).Join(flowClient()).Run(matClient) |> Async.AwaitTask
        printfn "Connected to %A from %A" conn.RemoteAddress conn.LocalAddress
    with 
    | :? AggregateException as e -> printfn "Inner: %A" (((e.InnerException :?> AggregateException).InnerException :?> Akka.Streams.StreamTcpException))
}
|> Async.RunSynchronously

