namespace Avalanchain.Server
open System.Threading
open FSharp.Control
open Akka.Streams
open Akka.Streams.Dsl
open Akkling.Streams

module WebSocketActor = 
    open Proto
    open Proto.FSharp
    open Giraffe
    open Giraffe.Common
    open Giraffe.WebSocket

    open System
    open System.Net.WebSockets

    type WebSocketMessage = | WebSocketMessage of string
        with member __.Value = match __ with | WebSocketMessage msg -> msg
    type WebSocketDispatcher = WebSocketMessage -> Async<unit>
    type WebSocketDisposer = string -> Async<unit>
    type WebSocketMessageHandler = WebSocketMessage -> Async<unit>
    type private WebSocketConnectionMessage = 
        | Message of WebSocketReference * WebSocketMessage
        | NewConnection of WebSocketReference

    let webSocketBase (isBroadcast: bool) (route: string) (log: string -> unit) (connection: WebSocketDispatcher -> WebSocketDisposer -> WebSocketReference -> WebSocketMessageHandler) cancellationToken =
        let connectionManager = ConnectionManager()
        let spawnChild (parentCtx: Proto.IContext) (ref: WebSocketReference) =
            let name = ref.ID

            let dispatcher (msg: WebSocketMessage) = 
                if isBroadcast then async { match msg with | WebSocketMessage msg -> do! connectionManager.BroadcastTextAsync(msg, cancellationToken) }
                else async { match msg with | WebSocketMessage msg -> do! ref.SendTextAsync(msg, cancellationToken) }
            let mutable actorPid = None
            let disposer reason = async { 
                actorPid |> Option.iter (fun pid -> pid <! PoisonPill) 
                do! ref.CloseAsync(reason, cancellationToken) }
            
            let handler = connection dispatcher disposer ref 

            let props = Actor.createAsync handler |> Actor.initProps
            let pid = parentCtx.SpawnNamed(props, name)
            actorPid <- Some pid
            log (sprintf "Spawned WebSocket connection: '%s' with PID: '%A' Id: '%s' Address: '%s'" name pid pid.Id pid.Address)
            pid

        let handler (ctx: IContext) (msg: WebSocketConnectionMessage) = async {
            match msg with 
            | NewConnection ref -> spawnChild ctx ref |> ignore
            | Message (ref, msg) -> 
                match ctx.Children |> Seq.tryFind (fun c -> c.Id.EndsWith ref.ID) with
                | Some pid -> pid <! msg
                | None -> log (sprintf "ERROR: WebSocket connection not found: '%s' for route '%s'" ref.ID route)
        }

        let pid = Actor.create2Async handler |> Actor.initProps |> Actor.spawnNamed ("ws_" + route)

        connectionManager.CreateSocket( (fun ref -> task { pid <! NewConnection ref } ),
                                        (fun ref msg -> task { pid <! Message(ref, WebSocketMessage msg) }),
                                        cancellationToken = cancellationToken)

    let webSocket route log connection cancellationToken = 
        webSocketBase false route log connection cancellationToken
    let webSocketBroadcast route log connection cancellationToken = 
        webSocketBase true route log connection cancellationToken


    let webSocketClient url (log: string -> unit) (connection: WebSocketDispatcher -> WebSocketDisposer -> WebSocketReference -> WebSocketMessageHandler) cancellationToken =
        let socket = new ClientWebSocket()
        let ref = WebSocketReference.FromWebSocket socket
        let messageSize = DefaultWebSocketOptions.ReceiveBufferSize
        let receive (reference: WebSocketReference) (handler: string -> Async<unit>) (cancellationToken:CancellationToken) = async {
            let buffer = Array.zeroCreate messageSize |> ArraySegment<byte>
            use memoryStream = new IO.MemoryStream()
            let mutable endOfMessage = false
            let mutable keepRunning = Unchecked.defaultof<_>
            printfn "WS Rec started:"

            while not endOfMessage do
                let! received = reference.WebSocket.ReceiveAsync(buffer, cancellationToken)
                printfn "WS Mes: %A" received
                if received.CloseStatus.HasValue then
                    do! reference.WebSocket.CloseAsync(received.CloseStatus.Value, received.CloseStatusDescription, cancellationToken)
                    keepRunning <- false
                    endOfMessage <- true
                else
                    memoryStream.Write(buffer.Array,buffer.Offset,received.Count)
                    if received.EndOfMessage then
                        match received.MessageType with
                        | WebSocketMessageType.Binary ->
                            raise (NotImplementedException())
                        | WebSocketMessageType.Close ->
                            keepRunning <- false 
                            endOfMessage <- true
                        | WebSocketMessageType.Text ->
                            let! r = 
                                memoryStream.ToArray()
                                |> System.Text.Encoding.UTF8.GetString
                                |> fun s -> s.TrimEnd(char 0)
                                |> handler 

                            keepRunning <- true
                            endOfMessage <- true
                        | _ ->
                            raise (NotImplementedException())

            return keepRunning
        }        
        
        let logger str (msg: WebSocketMessage) = 
            match msg with | WebSocketMessage msg -> printfn "%s:%s" str msg 
            msg

        async { do! socket.ConnectAsync(url, cancellationToken) } |> Async.RunSynchronously
        printfn "Socket state: %A" (socket.State)    
        let dispatcher (msg: WebSocketMessage) = 
            logger "Disp" msg |> ignore; 
            async { match msg with | WebSocketMessage msg -> do! ref.SendTextAsync(msg, cancellationToken) }
        let mutable actorPid = None
        let disposer reason = async { 
            actorPid |> Option.iter (fun pid -> pid <! PoisonPill) 
            do! ref.CloseAsync(reason, cancellationToken) }
        let handler = logger "Rec" >> connection dispatcher disposer ref 
        let receiver msg = async {
            let mutable running = true
            while running && not cancellationToken.IsCancellationRequested do
                let! msg = receive ref (WebSocketMessage >> handler) cancellationToken
                running <- msg
        }
        
        let pid = Actor.createAsync receiver |> Actor.spawnPropsPrefix ("wsClient_" + url.ToString() + "_") 
        pid <! "start"
        actorPid <- Some pid
        log (sprintf "Spawned WebSocket client to: '%A' with PID: '%A' Id: '%s' Address: '%s'" url pid pid.Id pid.Address)

        dispatcher, disposer


    let toAsyncSeqPair cancellationToken (dispatcher: WebSocketDispatcher) =
        let sinkSeqSrc = AsyncSeqSrc.create()
        sinkSeqSrc
        |> AsyncSeqSrc.toAsyncSeq 
        |> AsyncSeq.iterAsync dispatcher 
        |> fun aseq -> Async.Start (aseq, cancellationToken)

        let sourceSeqSrc = AsyncSeqSrc.create() 

        let handler = fun m -> async { sourceSeqSrc |> AsyncSeqSrc.put m }
        (sourceSeqSrc |> AsyncSeqSrc.toAsyncSeq), sinkSeqSrc, handler

    let fromAsyncSeqPair sourceSeq sinkSeqSrc cancellationToken (dispatcher: WebSocketDispatcher) =
        sourceSeq
        |> AsyncSeq.iterAsync dispatcher 
        |> fun aseq -> Async.Start (aseq, cancellationToken)

        fun m -> async { sinkSeqSrc |> AsyncSeqSrc.put m }
        
    // let toAkkaStreams materializer cancellationToken (dispatcher: WebSocketDispatcher) =
    //     let source = Source.queue OverflowStrategy.Backpressure 10000
    //                     |> Source.via flow 
    //                     |> Source.asyncMap 1 dispatcher 
    //                     |> Source.toMat (Sink.ignore) Keep.left
    //                     |> Graph.run materializer

    //     let handler = async {   let! _ = source.AsyncOffer m
    //                             () } // TODO: Process res
    //     (sourceSeqSrc |> AsyncSeqSrc.toAsyncSeq), sinkSeqSrc, handler        

    let fromAkkaStreams materializer (flow: Flow<WebSocketMessage,WebSocketMessage,_>) cancellationToken (dispatcher: WebSocketDispatcher) =
        let source = Source.queue OverflowStrategy.Backpressure 10000
                        |> Source.via flow 
                        |> Source.asyncMap 1 dispatcher 
                        |> Source.toMat (Sink.ignore) Keep.left
                        |> Graph.run materializer

        fun m -> async {    let! _ = source.AsyncOffer m
                            () } // TODO: Process res

    module Flow =
        let fromAsyncSeqs sourceSeq sinkSeqSrc cancellationToken = 
            let source = 
                Source.queue OverflowStrategy.Backpressure 10000
                |> Source.mapMaterializedValue (fun q -> sourceSeq
                                                        |> AsyncSeq.iterAsync (fun m -> async { let! _ = q.AsyncOffer m
                                                                                                () }) // TODO: Process res) 
                                                        |> fun aseq -> Async.Start (aseq, cancellationToken)) 

            let sink = Flow.id 
                        |> Flow.toSink (Sink.forEach (fun m -> sinkSeqSrc |> AsyncSeqSrc.put m ))

            Flow.ofSinkAndSourceMat sink Keep.right source

        let toAsyncSeqs cancellationToken (flow: Flow<'TIn,'TOut,_>) =
            let sinkSeqSrcSource = 
                Source.queue OverflowStrategy.Backpressure 10000
                |> Source.mapMaterializedValue (fun q -> 
                                                    let sinkSeqSrc = AsyncSeqSrc.create()
                                                    sinkSeqSrc
                                                    |> AsyncSeqSrc.toAsyncSeq 
                                                    |> AsyncSeq.iterAsync (fun m -> async { let! _ = q.AsyncOffer m
                                                                                            () }) // TODO: Process res) 
                                                    |> fun aseq -> Async.Start (aseq, cancellationToken)
                                                    sinkSeqSrc)

            let sourceSeqSrc = AsyncSeqSrc.create()

            sinkSeqSrcSource                                 
            |> Source.via flow 
            |> Source.toMat (Sink.forEach (fun m -> sourceSeqSrc |> AsyncSeqSrc.put m )) (fun sink _ -> sink, sourceSeqSrc |> AsyncSeqSrc.toAsyncSeq)

        // let parentHandler (ctx: IContext) (msg: obj) =
        //     printfn "(Parent) Message: %A" msg
        //     match msg with
        //     | :? string as message when message = "kill" ->
        //         printfn "Will kill someone"
        //         let children = ctx.Children
        //         let childToKill = children |> Seq.head
        //         "die" >! childToKill
        //     | :? Proto.Started ->
        //         [ 1 .. 3 ] |> List.iter (spawnChild ctx)
        //     | _ -> printfn "Some other message: %A" msg

        // let wsStreams = new ConcurrentDictionary<WebSocketReference, WebSocketConnection>()

        // let addSubscription = 

        // wsConnectionManager.CreateSocket(
        //                             (fun ref -> task { 
        //                                 let sink = new Subject<_>()
        //                                 let source = //connection ref sink
        //                                 source |> Observable.subscribe (fun msg -> ref.SendTextAsync(msg, cancellationToken))
        //                                 () 
        //                             }),
        //                             (fun ref msg -> ref.SendTextAsync("Hi " + msg, cancellationToken)),
        //                             cancellationToken = cancellationToken)

    // type WebSocketConnection = {
    //     Ref: WebSocketReference
    //     Sink: string -> Task<unit>
    //     Source: Subject<string>
    // }

// let webSocket (wsConnectionManager: ConnectionManager) (connection: WebSocketReference -> IObservable<string> -> IObservable<string>) cancellationToken =
//     let wsStreams = new ConcurrentDictionary<WebSocketReference, WebSocketConnection>()

//     wsConnectionManager.CreateSocket(
//                                 (fun ref -> 
//                                             task { 
//                                                 let sink = new Subject<_>()
//                                                 let source = connection sink
//                                                 source |> Observable.subscribe (fun msg -> ref.SendTextAsync(msg, cancellationToken).Wait())
//                                                 () 
//                                             }),
//                                 (fun ref msg -> ref.SendTextAsync("Hi " + msg, cancellationToken)),
//                                 cancellationToken = cancellationToken)

                