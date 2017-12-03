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

let system = System.create "streams-sys" <| Configuration.defaultConfig()
let mat = system.Materializer()

// module Stages = 
let listFolder filter path =
    Source.ofSeq(Directory.EnumerateFiles(path, filter)) 
    |> Source.map(fun f -> Path.GetFileName f, f)

let watchFolder filter path = 
    Source.queue OverflowStrategy.DropNew 1000 
        |> Source.mapMaterializedValue(
            fun queue ->
                let watcher = new FileSystemWatcher()
                watcher.Path <- path
                watcher.NotifyFilter <- NotifyFilters.LastWrite
                watcher.Filter <- filter //"*.*"
                watcher.Changed.Add(fun e -> queue.AsyncOffer(e.Name, e.FullPath) |> Async.Ignore |> Async.Start)
                watcher.EnableRaisingEvents <- true
                ()
            )
        |> Source.groupedWithin 10 (TimeSpan.FromMilliseconds 50.)
        |> Source.collect(fun files -> files |> Seq.distinct)
        //|> Source.recover (fun _ -> Some ())

let fileTransferFlow chunkSize destination = 
    Flow.id
    |> Flow.map(fun (fileName, path) -> 
        let destinationFileName = Path.Combine(destination, fileName)
        fileName, destinationFileName, Source.ofStreamChunked chunkSize (fun () -> new FileStream(path, FileMode.Open, FileAccess.Read) :> Stream)
                                        |> Source.toMat (Sink.ofStreamFlushed(fun () -> new FileStream(destinationFileName, FileMode.OpenOrCreate, FileAccess.Write) :> Stream)) Keep.none)
                                                    

//folderWatcher OverflowStrategy.Backpressure 1000 "*.xml" "files"
listFolder "*.xml" "files"
|> Source.via (fileTransferFlow 1024 "files_out")
|> Source.map (fun (fileName, path, runnable) -> 
                    printfn "Transferring file: %s" fileName
                    //async { 
                    fileName, Graph.run mat runnable)
|> Source.runForEach mat (fun (fileName, runnable) -> ())

let listAndWatchFolder filter path = 
    listFolder filter path
    |> Source.concat(watchFolder filter path |> Source.mapMaterializedValue ignore)

listAndWatchFolder "*.xml" "files"
|> Source.via (fileTransferFlow 1024 "files_out")
|> Source.map (fun (fileName, path, runnable) -> 
                    printfn "Transferring file: %s" fileName
                    //async { 
                    fileName, Graph.run mat runnable)
|> Source.runForEach mat (fun (fileName, runnable) -> ())


let wsPaths =
    [   "/ws1", fun (conn: IncomingConnection) -> 
                    printfn "Connected ws1" 
                    printfn "from: '%A' to: '%A'" conn.RemoteAddress conn.LocalAddress
                    let echo = Flow.id
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
        "/ws3", fun (conn: IncomingConnection) -> 
                    printfn "Connected ws3" 
                    printfn "from: '%A' to: '%A'" conn.RemoteAddress conn.LocalAddress
                    let echo = Flow.id
                                |> Flow.map string
                                |> Flow.merge (listAndWatchFolder "*.xml" "files" |> Source.map(fun (fileName, path) -> fileName))
                                |> Flow.map ByteString.FromString
                    conn.Flow |> Flow.join echo |> Graph.run mat |> ignore
    ] |> Map.ofList
webSocketServer { defaultConfig with bindings = [ HttpBinding.createSimple HTTP "127.0.0.1" 8082 ]} wsPaths mat
