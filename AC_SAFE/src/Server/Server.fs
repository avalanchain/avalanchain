namespace Avalanchain.Server

open Avalanchain.Core
module Server =

    open System
    open System.Collections.Concurrent
    open System.Collections.Generic
    open System.IO
    open System.Reactive
    open System.Reactive.Subjects
    open System.Reactive.Linq
    open System.Threading
    open System.Threading.Tasks
    open Microsoft.AspNetCore
    open Microsoft.AspNetCore.Builder
    open Microsoft.AspNetCore.Hosting
    open Microsoft.Extensions.DependencyInjection
    open Microsoft.Extensions.Logging
    open Microsoft.AspNetCore.Http

    open Newtonsoft.Json

    open Giraffe
    open Giraffe.Swagger
    open Giraffe.Swagger.Common
    open Giraffe.Swagger.Analyzer
    open Giraffe.Swagger.Generator
    open Giraffe.Swagger.Dsl
    open SwaggerUi
    open Giraffe.WebSocket
    open Giraffe.Serialization.Json
    open Giraffe.ModelBinding

    open Fable.Remoting.Giraffe
    open FSharp.Control.Reactive
    open FSharp.Control.Tasks
    open FSharp.Control.Tasks.ContextInsensitive

    open Shared
    open Avalanchain.Exchange.MatchingEngine
    open Avalanchain.Core
    open Avalanchain.Core.Crypto
    open Avalanchain.Core.Chains
    // open Avalanchain.Core.Node
    open Avalanchain.Core.Observable
    open Avalanchain.Server.WebSocketActor
    // open Network

    // open Akka
    // open Akka.Actor
    // open Akka.Streams
    // open Akka.Streams.Dsl
    // open Akkling.Streams


    module ParamHelper =
        let tagPartitioner (pathPrefix: string) tag (path: string, verb, pathDef: PathDefinition) = 
            let pathDef = if path.ToLowerInvariant().StartsWith (pathPrefix.ToLowerInvariant()) then { pathDef with Tags=[ tag ] } else pathDef
            path, verb, pathDef

        let adjustOperationId (path: string, verb, pathDef: PathDefinition) = 
            path, verb, { pathDef with OperationId = path.Substring(1).Replace("/", "_").ToLowerInvariant() }

        let addParam (paramIn: ParamContainer) paramType name required (pathPostfixes: string list) (path: string, verb, pathDef: PathDefinition) = 
            let lpath = path.Split "/" |> Array.last
            let pathDef =   if pathPostfixes |> List.exists (fun spath -> lpath.ToLowerInvariant() = spath.ToLowerInvariant()) 
                            then 
                                let param = {   ParamDefinition.Name = name
                                                Type = paramType |> Some
                                                In = paramIn.ToString()
                                                Required = required }
                                { pathDef with Parameters = param :: pathDef.Parameters } 
                            else pathDef
            path, verb, pathDef

        let addQueryParam typ = addParam ParamContainer.Query (Primitive (typ, ""))
        let addBodyParam<'T> = addParam ParamContainer.Body (Ref (typeof<'T>.Describes()))
        let addBodyParam2<'T> name (pathPostfixes: string list) (path: string, verb, pathDef: PathDefinition) = 
            let pathDef =   if pathPostfixes |> List.exists (fun spath -> path.ToLowerInvariant().EndsWith (spath.ToLowerInvariant())) 
                            then 
                                pathDef.AddConsume name "application/json" ParamContainer.Body typeof<'T>
                            else pathDef
            path, verb, pathDef

    open ParamHelper
    open Avalanchain.Core.Chains.PagedLog
    open Avalanchain.Exchange
    let docAddendums =
        fun (route:Analyzer.RouteInfos) (path:string,verb:HttpVerb,pathDef:PathDefinition) ->
        
            // routef params are automatically added to swagger, but you can customize their names like this 
            let changeParamName oldName newName (parameters:ParamDefinition list) =
                parameters |> Seq.find (fun p -> p.Name = oldName) |> fun p -> { p with Name = newName }
        
            let (path,verb,pathDef) = 
                (path,verb,pathDef) 
                |> adjustOperationId
                |> tagPartitioner "/api/exchange/" "Exchange"
                |> tagPartitioner "/api/exchange/token/" "Token"
                |> tagPartitioner "/api/exchange/jwt/" "Jwt"
                |> addQueryParam "string" "symbol" false   ["OrderStack"; "OrderStackView";
                                                            "SymbolOrderCommands"; "SymbolOrderEvents"; "SymbolFullOrders";
                                                            "SymbolLastOrderCommands"; "SymbolLastOrderEvents"; "SymbolLastFullOrders";
                                                            "SymbolOrderCommandsCount"; "SymbolOrderEventsCount"; "SymbolFullOrdersCount"] 
                |> addQueryParam "integer" "maxDepth" false["OrderStackView"]
                |> addQueryParam "string" "orderID" true   ["GetOrder"; "GetOrder2"] 
                |> addQueryParam "long" "startIndex" false ["GetOrders"; "OrderCommands"; "OrderEvents"; "FullOrders";
                                                            "SymbolOrderCommands"; "SymbolOrderEvents"; "SymbolFullOrders"]
                |> addQueryParam "integer" "pageSize" false["GetOrders"; "OrderCommands"; "OrderEvents"; "FullOrders"; 
                                                            "LastOrderCommands"; "LastOrderEvents"; "LastFullOrders"; 
                                                            "SymbolOrderCommands"; "SymbolOrderEvents"; "SymbolFullOrders";
                                                            "SymbolLastOrderCommands"; "SymbolLastOrderEvents"; "SymbolLastFullOrders"]
                |> addBodyParam2<OrderCommand> "orderCommand" ["SubmitOrder"]                                                        

            match path, verb, pathDef with
            | _, _, def when def.OperationId = "say_hello_in_french" ->
                let firstname = def.Parameters |> changeParamName "arg0" "Firstname"
                let lastname = def.Parameters |> changeParamName "arg1" "Lastname"
                "/hello/{Firstname}/{Lastname}", verb, { def with Parameters = [ firstname; lastname ] }

            | "/", HttpVerb.Get, def ->
                // This is another solution to add operation id or other infos
                path, verb, { def with OperationId = "Home"; Tags=["Home Page"] }
           
            | _ -> path, verb, pathDef


    let docsConfig port c = 
        let describeWith desc  = 
            { desc
                with
                    Title="Avalanchain Node"
                    Description="Avalanchain Standard Node"
                    TermsOfService="/termsOfService"
            } 
        
        { c with 
            Description = describeWith
            Host = sprintf "localhost:%d" port
            DocumentationAddendums = docAddendums
            MethodCallRules = 
                    (fun rules -> 
                        // You can extend quotation expression analysis
                        rules.Add ({ ModuleName="App"; FunctionName="httpFailWith" }, 
                           (fun ctx -> 
                               ctx.AddResponse 500 "text/plain" (typeof<string>)
                    )))
        }


    let getInitCounter () : Task<Counter> = task { return 42 }

    let parsingError (err : string) = RequestErrors.BAD_REQUEST err

    type [<CLIMutable>] SymbolQuery = { symbol: string option }
    type [<CLIMutable>] SymbolMaxDepthQuery = { symbol: string option; maxDepth: int option }
    type [<CLIMutable>] OrderIDQuery = { orderID: string option }
    type [<CLIMutable>] PageStartQuery = { startIndex: uint64 option; pageSize: uint32 option }
    type [<CLIMutable>] PageQuery = { pageSize: uint32 option }
    type [<CLIMutable>] PageSymbolStartQuery = { symbol: string option; startIndex: uint64 option; pageSize: uint32 option }
    type [<CLIMutable>] PageSymbolPageQuery = { symbol: string option; pageSize: uint32 option }

    // let primitive value : HttpHandler = text (value.ToString())
    let textAsync (str : unit -> Task<string>) : HttpHandler =
        fun (next : HttpFunc) (ctx : HttpContext) -> task { 
            let! s = str() 
            return! ctx.WriteTextAsync s
        }

    let primitive (value: unit -> Task<_>): HttpHandler = textAsync (fun () -> task {   let! v = value()
                                                                                        return v.ToString() }) |> Successful.ok

    let jsonAsync (value: Task<'T>) : HttpHandler =
        fun (next : HttpFunc) (ctx : HttpContext) -> task { 
            let! v = value 
            return! ctx.WriteJsonAsync v
        }

    let bindSymbolQuery successHandler = 
        bindQuery<SymbolQuery> None (fun qs -> qs.symbol |> Option.defaultValue "" |> Symbol |> successHandler |> json |> Successful.ok)

    let bindSymbolUInt64Query successHandler = 
        bindQuery<SymbolQuery> None (fun qs -> qs.symbol |> Option.defaultValue "" |> Symbol |> successHandler |> primitive)

    let bindSymbolMaxDepthQuery successHandler = 
        bindQuery<SymbolMaxDepthQuery> None (fun qs -> (successHandler (qs.symbol |> Option.defaultValue "" |> Symbol) (qs.maxDepth |> Option.defaultValue 10)) |> json |> Successful.ok)

    let bindOrderIDQuery successHandler = 
        bindQuery<OrderIDQuery> None (fun qs -> qs.orderID |> Option.defaultValue "" |> successHandler |> jsonAsync |> Successful.ok)

    let bindPageStartQuery successHandler = 
        bindQuery<PageStartQuery> None (fun pq -> successHandler (pq.startIndex |> Option.defaultValue 0UL) (pq.pageSize |> Option.defaultValue 0u) |> jsonAsync |> Successful.ok)

    let bindPageQuery successHandler = 
        bindQuery<PageQuery> None (fun qs -> qs.pageSize |> Option.defaultValue 0u |> successHandler |> jsonAsync |> Successful.ok)

    let bindSymbolPageStartQuery successHandler = 
        bindQuery<PageSymbolStartQuery> None (fun pq -> successHandler (pq.symbol |> Option.defaultValue "" |> Symbol) (pq.startIndex |> Option.defaultValue 0UL) (pq.pageSize |> Option.defaultValue 0u) |> jsonAsync |> Successful.ok)

    let bindSymbolPageQuery successHandler = 
        bindQuery<PageSymbolPageQuery> None (fun pq -> successHandler (pq.symbol |> Option.defaultValue "" |> Symbol) (pq.pageSize |> Option.defaultValue 0u) |> jsonAsync |> Successful.ok)                          

    let logger str (msg: WebSocketMessage) = 
        match msg with | WebSocketMessage msg -> printfn "%s:%s" str msg 
        msg

    type InternalError<'T> = InternalError of 'T * string 
    let toInternalError<'T> (obs: IObservable<Result<LogOfferReceipt<'T>, 'T * EventLogError>>) = 
        obs 
        |> Observable.map(  Result.map(fun r -> r.Payload) 
                            >> Result.mapError(fun (t, e) -> InternalError(t, e.ToString())))

    type ObservableSource<'T> = IObservable<Result<'T, InternalError<'T>>>

    type MatchingServiceSources = {
        OrderCommandsSource: ObservableSource<OrderCommand>
        OrderEventsSource: ObservableSource<OrderEvent>
        FullOrdersSource: ObservableSource<Order>
    }

    type MatchingServiceQueues = {
        OrderCommandsQueue: IObserver<OrderCommand>
        OrderEventsQueue: IObserver<OrderEvent>
        FullOrdersQueue: IObserver<Order>
    }

    let toWebSocketMessage o = o |> toJson |> WebSocketMessage 
    let fromWebSocketMessage (msg: WebSocketMessage) = msg.Value 

    let wsStreamsBinding prefix (streams: MatchingServiceSources) cancellationToken =
        let webSocketRouteCi route connection = routeCi route >=> webSocket route ignore connection cancellationToken
        let webSocketBroadcastRouteCi route connection = routeCi route >=> webSocketBroadcast route ignore connection cancellationToken

        let toSourceHandler (dispatcher: WebSocketDispatcher) (source: IObservable<_>) = 
            let obs = source
                        |> Observable.bind(fun o -> Observable.FromAsync(fun () -> o |> toWebSocketMessage |> dispatcher))
                        |> Observable.subscribe ignore
            (fun m ->
                let _ = obs // Just not to loose the ref 
                m |> dispatcher)

        choose [
            webSocketBroadcastRouteCi (prefix + "/OrderRequests") (fun d _ _ -> toSourceHandler d streams.OrderCommandsSource)
            webSocketBroadcastRouteCi (prefix + "/OrderEvents") (fun d _ _ -> toSourceHandler d streams.OrderEventsSource)
            webSocketBroadcastRouteCi (prefix + "/FullOrders") (fun d _ _ -> toSourceHandler d streams.FullOrdersSource)
        ]

    let wsSymbolStreamsBinding prefix (symbol: Symbol) (streams: MatchingServiceSources) cancellationToken =
        wsStreamsBinding (prefix + "/" + symbol.Value) streams cancellationToken

    let wsSymbolStreamsBindings prefix (streams: Map<Symbol, MatchingServiceSources>) cancellationToken =
        choose [ for kv in streams -> wsSymbolStreamsBinding (prefix + "/symbol") kv.Key kv.Value cancellationToken ]

    //let wsSymbolStreamsBinding materializer (streams: Map<Symbol, MatchingServiceSources>) cancellationToken =
    //  subRouteCi "/symbols" (choose [ for kv in streams -> wsSymbolStreamsBinding materializer kv.Key kv.Value cancellationToken ])
    //
    //let wsSymbolStreamsBinding materializer (streams: MatchingServiceSources) (symbolStreams: Map<Symbol, MatchingServiceSources>) cancellationToken =
    //  subRouteCi "/ws" (
    //    choose [ 
    //      wsStreamsBinding materializer streams cancellationToken
    //      wsSymbolStreamsBinding materializer symbolStreams cancellationToken
    //    ]
    //  )

    //let prepareStreams (mat: IMaterializer) = 
    //    let ocq, ocs = Source.queue OverflowStrategy.DropTail 10000 |> Source.toMat (Sink.broadcastHub 1000) Keep.both |> Graph.run mat
    //    let oeq, oes = Source.queue OverflowStrategy.DropTail 10000 |> Source.toMat (Sink.broadcastHub 1000) Keep.both |> Graph.run mat
    //    let foq, fos = Source.queue OverflowStrategy.DropTail 10000 |> Source.toMat (Sink.broadcastHub 1000) Keep.both |> Graph.run mat
    //    let queues = {  OrderCommandsQueue = ocq
    //                    OrderEventsQueue = oeq
    //                    FullOrdersQueue = foq }
    //    let sources = { OrderCommandsSource = ocs
    //                    OrderEventsSource = oes
    //                    FullOrdersSource = fos }
    //    queues, sources    

    type MatchingServiceStreaming = {
        Streams: MatchingServiceLogs
        SymbolStreams: MatchingServiceSymbolLogs
    }
    let prepareStreams (streams: MatchingServiceLogs) = 
        let queues = {  OrderCommandsQueue = Subject.broadcast
                        OrderEventsQueue   = Subject.broadcast
                        FullOrdersQueue    = Subject.broadcast }
        let ocs = streams.OrderCommands.View.Subscribe()
        let oes = streams.OrderEvents.View.Subscribe()
        let fos = streams.FullOrders.View.Subscribe()
        let sources = { OrderCommandsSource = ocs |> toInternalError
                        OrderEventsSource   = oes |> toInternalError
                        FullOrdersSource    = fos |> toInternalError }
                        
        queues, sources               

    let prepareSymbolStreams mat (symbols: Symbol list) =
      symbols |> List.map (fun s -> s, prepareStreams mat) |> Map.ofList 
      
    // let prepareStreams (streams: Symbol list)

    // let prefix = "/aa"
    let eventLogViewSection<'T> prefix name (eventLog: EventLogView<'T>) = //(handler: HttpHandler) = 
        choose [
            routeCi  (prefix + "/" + name)       >=> bindPageStartQuery eventLog.GetPage
            routeCi  (prefix + "/token/" + name) >=> bindPageStartQuery eventLog.GetPageToken
            routeCi  (prefix + "/jwt/" + name)   >=> bindPageStartQuery eventLog.GetPageJwt

            routeCi  (prefix + "/Last" + name)       >=> bindPageQuery eventLog.GetLastPage
            routeCi  (prefix + "/token/Last" + name) >=> bindPageQuery eventLog.GetLastPageToken
            routeCi  (prefix + "/jwt/Last" + name)   >=> bindPageQuery eventLog.GetLastPageJwt

            routeCi  (prefix + "/" + name + "Count") >=> primitive eventLog.GetCount
        ]

    let webApp (ms: MatchingService) port symbols cancellationToken : HttpHandler =
      let wsConnectionManager = ConnectionManager()
      let counterProcotol = 
        { getInitCounter = getInitCounter >> Async.AwaitTask }

      // creates a HttpHandler for the given implementation
      choose [swaggerOf
                (choose [ //route  "/test"       >=> text "test" 
                          //route  "/test2"       >=> text "test12"
                          subRouteCi "/api" (
                            choose [
                                subRouteCi "/Exchange" (
                                  choose [
                                    POST >=> 
                                        routeCi  "/SubmitOrder" >=> //operationId "submit_order" ==> consumes typeof<OrderCommand> ==> produces typeof<OrderCommand> ==>
                                                bindJson<OrderCommand> (ms.SubmitOrder >> Successful.OK )
                                    GET >=>
                                      choose [
                                        routeCi  "/OrderStack"      >=> bindSymbolQuery ms.OrderStack
                                        routeCi  "/OrderStackView"  >=> bindSymbolMaxDepthQuery ms.OrderStackView
                                        routeCi  "/MainSymbol"      >=> json ms.MainSymbol |> Successful.ok
                                        routeCi  "/Symbols"         >=> json ms.Symbols |> Successful.ok
                                        routeCi  "/GetOrder"        >=> bindQuery<OrderIDQuery> None 
                                                                        (fun oq -> match oq.orderID with
                                                                                    | None -> parsingError "Missing order ID"
                                                                                    | Some guidStr -> guidStr |> Guid.Parse |> ms.OrderById |> json |> Successful.ok)
                                        // routeCi  "/GetOrder2"       >=> bindOrderIDQuery ms.OrderById2
                                        routeCi  "/GetOrders"       >=> bindPageStartQuery ms.Orders
                                        routeCi  "/GetOrdersCount"  >=> primitive ms.OrdersCount
                                        
                                        routeCi  "/OrderCommands"   >=> bindPageStartQuery ms.OrderCommands.GetPage
                                        routeCi  "/OrderEvents"     >=> bindPageStartQuery ms.OrderEvents.GetPage
                                        routeCi  "/FullOrders"      >=> bindPageStartQuery ms.FullOrders.GetPage
                                        routeCi  "/token/OrderCommands" >=> bindPageStartQuery ms.OrderCommands.GetPageToken
                                        routeCi  "/token/OrderEvents"   >=> bindPageStartQuery ms.OrderEvents.GetPageToken
                                        routeCi  "/token/FullOrders"    >=> bindPageStartQuery ms.FullOrders.GetPageToken
                                        routeCi  "/jwt/OrderCommands"   >=> bindPageStartQuery ms.OrderCommands.GetPageJwt
                                        routeCi  "/jwt/OrderEvents"     >=> bindPageStartQuery ms.OrderEvents.GetPageJwt
                                        routeCi  "/jwt/FullOrders"      >=> bindPageStartQuery ms.FullOrders.GetPageJwt
                                        
                                        routeCi  "/LastOrderCommands"  >=> bindPageQuery ms.OrderCommands.GetLastPage
                                        routeCi  "/LastOrderEvents"    >=> bindPageQuery ms.OrderEvents.GetLastPage
                                        routeCi  "/LastFullOrders"     >=> bindPageQuery ms.FullOrders.GetLastPage
                                        routeCi  "/token/LastOrderCommands"  >=> bindPageQuery ms.OrderCommands.GetLastPageToken
                                        routeCi  "/token/LastOrderEvents"    >=> bindPageQuery ms.OrderEvents.GetLastPageToken
                                        routeCi  "/token/LastFullOrders"     >=> bindPageQuery ms.FullOrders.GetLastPageToken
                                        routeCi  "/jwt/LastOrderCommands"  >=> bindPageQuery ms.OrderCommands.GetLastPageJwt
                                        routeCi  "/jwt/LastOrderEvents"    >=> bindPageQuery ms.OrderEvents.GetLastPageJwt
                                        routeCi  "/jwt/LastFullOrders"     >=> bindPageQuery ms.FullOrders.GetLastPageJwt

                                        routeCi  "/OrderCommandsCount"       >=> primitive ms.OrderCommands.GetCount
                                        routeCi  "/OrderEventsCount"         >=> primitive ms.OrderEvents.GetCount
                                        routeCi  "/FullOrdersCount"          >=> primitive ms.FullOrders.GetCount
                                       
                                        routeCi  "/SymbolOrderCommands"   >=> bindSymbolPageStartQuery (fun symbol -> (ms.SymbolOrderCommands symbol).GetPage)
                                        routeCi  "/SymbolOrderEvents"     >=> bindSymbolPageStartQuery (fun symbol -> (ms.SymbolOrderEvents symbol).GetPage)
                                        routeCi  "/SymbolFullOrders"      >=> bindSymbolPageStartQuery (fun symbol -> (ms.SymbolFullOrders symbol).GetPage)
                                        routeCi  "/token/SymbolOrderCommands"   >=> bindSymbolPageStartQuery (fun symbol -> (ms.SymbolOrderCommands symbol).GetPageToken)
                                        routeCi  "/token/SymbolOrderEvents"     >=> bindSymbolPageStartQuery (fun symbol -> (ms.SymbolOrderEvents symbol).GetPageToken)
                                        routeCi  "/token/SymbolFullOrders"      >=> bindSymbolPageStartQuery (fun symbol -> (ms.SymbolFullOrders symbol).GetPageToken)
                                        routeCi  "/jwt/SymbolOrderCommands"   >=> bindSymbolPageStartQuery (fun symbol -> (ms.SymbolOrderCommands symbol).GetPageJwt)
                                        routeCi  "/jwt/SymbolOrderEvents"     >=> bindSymbolPageStartQuery (fun symbol -> (ms.SymbolOrderEvents symbol).GetPageJwt)
                                        routeCi  "/jwt/SymbolFullOrders"      >=> bindSymbolPageStartQuery (fun symbol -> (ms.SymbolFullOrders symbol).GetPageJwt)
                                       
                                        routeCi  "/SymbolOrderCommandsCount" >=> bindSymbolUInt64Query (fun symbol -> (ms.SymbolOrderCommands symbol).GetCount)
                                        routeCi  "/SymbolOrderEventsCount"   >=> bindSymbolUInt64Query (fun symbol -> (ms.SymbolOrderEvents symbol).GetCount)
                                        routeCi  "/SymbolFullOrdersCount"    >=> bindSymbolUInt64Query (fun symbol -> (ms.SymbolFullOrders symbol).GetCount)
                                       
                                        routeCi  "/SymbolLastOrderCommands"  >=> bindSymbolPageQuery (fun symbol -> (ms.SymbolOrderCommands symbol).GetLastPage)
                                        routeCi  "/SymbolLastOrderEvents"    >=> bindSymbolPageQuery (fun symbol -> (ms.SymbolOrderEvents symbol).GetLastPage)
                                        routeCi  "/SymbolLastFullOrders"     >=> bindSymbolPageQuery (fun symbol -> (ms.SymbolFullOrders symbol).GetLastPage)
                                        routeCi  "/token/SymbolLastOrderCommands"  >=> bindSymbolPageQuery (fun symbol -> (ms.SymbolOrderCommands symbol).GetLastPageToken)
                                        routeCi  "/token/SymbolLastOrderEvents"    >=> bindSymbolPageQuery (fun symbol -> (ms.SymbolOrderEvents symbol).GetLastPageToken)
                                        routeCi  "/token/SymbolLastFullOrders"     >=> bindSymbolPageQuery (fun symbol -> (ms.SymbolFullOrders symbol).GetLastPageToken)
                                        routeCi  "/jwt/SymbolLastOrderCommands"  >=> bindSymbolPageQuery (fun symbol -> (ms.SymbolOrderCommands symbol).GetLastPageJwt)
                                        routeCi  "/jwt/SymbolLastOrderEvents"    >=> bindSymbolPageQuery (fun symbol -> (ms.SymbolOrderEvents symbol).GetLastPageJwt)
                                        routeCi  "/jwt/SymbolLastFullOrders"     >=> bindSymbolPageQuery (fun symbol -> (ms.SymbolFullOrders symbol).GetLastPageJwt)

                                        // eventLogViewSection "OrderCommands" ms.OrderCommands // TODO: Investigate possibility of not loosing swagger defs 
                                      ]
                                  ])
                                subRouteCi "/Currency" (
                                  choose [
                                    subRouteCi "/Node" (
                                      choose [
                                        POST >=> 
                                            routeCi "/SubmitOrder" >=> //operationId "submit_order" ==> consumes typeof<OrderCommand> ==> produces typeof<OrderCommand> ==>
                                                    bindJson<OrderCommand> (ms.SubmitOrder >> Successful.OK )
                                        GET >=>
                                          choose [
                                            route  "/OrderStack"      >=> bindSymbolQuery ms.OrderStack
                                            
                                          ]
                                      ])
                                    subRouteCi "/Wallet" (
                                      choose [
                                        POST >=> 
                                            routeCi "/SubmitOrder" >=> //operationId "submit_order" ==> consumes typeof<OrderCommand> ==> produces typeof<OrderCommand> ==>
                                                    bindJson<OrderCommand> (ms.SubmitOrder >> Successful.OK )
                                        GET >=>
                                          choose [
                                            route  "/OrderStack"      >=> bindSymbolQuery ms.OrderStack
                                            
                                          ]
                                      ])
                                    subRouteCi "/Account" (
                                      choose [
                                        POST >=> 
                                            routeCi "/SubmitOrder" >=> //operationId "submit_order" ==> consumes typeof<OrderCommand> ==> produces typeof<OrderCommand> ==>
                                                    bindJson<OrderCommand> (ms.SubmitOrder >> Successful.OK )
                                        GET >=>
                                          choose [
                                            route  "/OrderStack"      >=> bindSymbolQuery ms.OrderStack
                                            
                                          ]
                                      ])
                                  ]
                                )
                          ]) 
                          GET >=>
                             choose [
                                  route  "/"           >=> htmlFile "index.html"
                                  route  "/ping"       >=> text "pong"
                        ]
                ]) |> withConfig (docsConfig port)
              
              wsStreamsBinding "/wstreams" (ms.Streams |> prepareStreams |> snd) cancellationToken
              wsSymbolStreamsBindings "/wstreams" (symbols |> Seq.map (fun s -> s, ms.SymbolStreams s |> prepareStreams |> snd) |> Map.ofSeq) cancellationToken

              route "/wsecho" >=> (wsConnectionManager.CreateSocket(
                                    (fun ref -> task { return () }),
                                    (fun ref msg -> ref.SendTextAsync("Hi " + msg, cancellationToken)),
                                    cancellationToken = cancellationToken)) 
              route "/wsecho2" >=> webSocket "wsecho2" (printfn "%s") (fun d _ _ -> (fun m -> m |> d)) cancellationToken
              route "/wsecho3" >=> webSocketBroadcast "wsecho3" (printfn "%s") (fun d _ _ -> (fun m -> "Hi " + m.Value |> WebSocketMessage |> logger "wsecho3" |> d)) cancellationToken
              route "/wsecho4" >=> webSocket "wsecho4" (printfn "%s") 
                                    (fun dispatcher _ _ -> 
                                            let url = (sprintf "ws://localhost:%d/wsecho3" port) |> Uri
                                            let (clientDispatcher, _) = 
                                                webSocketClient url (printfn "%s") (fun d _ _ -> (logger "Rec2" >> dispatcher)) cancellationToken
                                            logger "ClientRec" >> clientDispatcher
                                        ) 
                                    cancellationToken
    //          route "/wsecho5" >=> webSocket "wsecho5" (printfn "%s") 
    //                                (fun dispatcher _ _ -> 
    //                                        let source, sink, handler = toAsyncSeqPair cancellationToken dispatcher
    //                                        let url = (sprintf "ws://localhost:%d/wsecho4" port) |> Uri
    //                                        webSocketClient url (printfn "%s") (fun d _ _ -> fromAsyncSeqPair source sink cancellationToken dispatcher) cancellationToken |> ignore
    //                                        handler
    //                                    ) 
    //                                cancellationToken
              route  "/termsOfService"       >=> text "TODO: Add Terms of Service" 
              FableGiraffeAdapter.httpHandlerWithBuilderFor counterProcotol Route.builder 
              ]

    let matchingServiceLogs (config: StreamingConfig) pidPrefix = 
        task {
            let! commands = eventLog<OrderCommand> config pidPrefix
            let! events = eventLog<OrderEvent> config pidPrefix
            let! fullOrders = eventLog<Order> config pidPrefix
            return {OrderCommands = commands
                    OrderEvents = events
                    FullOrders = fullOrders }
        }
        |> Async.AwaitTask 
        |> Async.RunSynchronously

    let symbolMatchingServiceLogs (config: StreamingConfig) pidPrefix (symbol: Symbol) = matchingServiceLogs config (pidPrefix + "__" + symbol.Value)

    let symbolsMatchingServiceLogs (config: StreamingConfig) pidPrefix (symbols: Symbol seq) =
        symbols 
        |> Seq.map (fun symbol -> symbol, symbolMatchingServiceLogs config pidPrefix symbol)
        |> Map.ofSeq                            

    let matchingServiceStreaming (config: StreamingConfig) pidPrefix (symbols: Symbol seq) = // TODO: Apply this 
        let symbolStreamsMap = symbolsMatchingServiceLogs config pidPrefix symbols  
        {   Streams = matchingServiceLogs config pidPrefix
            SymbolStreams = fun symbol -> symbolStreamsMap.[symbol] }

    let matchingService symbols =
        // let endpoint1 = { IP = "127.0.0.1"; Port = 5000us }

        // let acNode = setupNode "ac1" endpoint1 [endpoint1] (OverflowStrategy.DropNew) 1000 // None
        // Threading.Thread.Sleep 1000 
        
        let keyVault = KeyVault([KeyVaultEntry.generate()]) // TODO: Add persistence
         
        let streamingConfig = {  //Node = acNode
                                 SnapshotInterval = 1000L
                                //  OverflowStrategy = OverflowStrategy.Backpressure
                                 QueueMaxBuffer = 10000
                                 Verify = false
                                 KeyVault = keyVault }
        let streaming: MatchingServiceStreaming = matchingServiceStreaming streamingConfig "matchingService" symbols
        let ms = MatchingService (streaming.Streams, symbols, streaming.SymbolStreams, 1M<price>, 100UL) 
        ms

    let startSimulation ms symbols = async { do! TradingBot.tradingBot(ms, symbols) |> Async.AwaitTask } |> Async.Start
    let startSimulation2 ms symbols = async { do! TradingBot.tradingBot(ms, symbols) |> Async.AwaitTask } |> Async.Start
    let startSimulation3 ms symbols = async { do! TradingBot.tradingBot(ms, symbols) |> Async.AwaitTask } |> Async.Start
    let startSimulation4 ms symbols = async { do! TradingBot.tradingBot(ms, symbols) |> Async.AwaitTask } |> Async.Start
    let startSimulation5 ms symbols = async { do! TradingBot.tradingBot(ms, symbols) |> Async.AwaitTask } |> Async.Start
    let startSimulation6 ms symbols = async { do! TradingBot.tradingBot(ms, symbols) |> Async.AwaitTask } |> Async.Start
    let startSimulation7 ms symbols = async { do! TradingBot.tradingBot(ms, symbols) |> Async.AwaitTask } |> Async.Start
    let startSimulation8 ms symbols = async { do! TradingBot.tradingBot(ms, symbols) |> Async.AwaitTask } |> Async.Start
    let startSimulation9 ms symbols = async { do! TradingBot.tradingBot(ms, symbols) |> Async.AwaitTask } |> Async.Start
    let startSimulation10 ms symbols = async { do! TradingBot.tradingBot(ms, symbols) |> Async.AwaitTask } |> Async.Start
    let startSimulation11 ms symbols = async { do! TradingBot.tradingBot(ms, symbols) |> Async.AwaitTask } |> Async.Start
    let startSimulation12 ms symbols = async { do! TradingBot.tradingBot(ms, symbols) |> Async.AwaitTask } |> Async.Start

    let configureApp port (app : IApplicationBuilder) =
      let symbols = ["AVC"; "BTC"; "XRP"; "ETH"; "AIM"; "LTC"; "ADA"; "XLM"; "NEO"; "EOS"; "MIOTA"; "XMR"; "DASH"; "XEM"; "TRX"; "USDT"; "BTS"; "ETC"; "NANO" ] |> List.map Symbol
      let ms = matchingService symbols
      startSimulation ms symbols

      app
        .UseStaticFiles()
        .UseWebSockets()
        .UseGiraffe (webApp ms port symbols CancellationToken.None)

    let configureServices (services : IServiceCollection) =
        services.AddGiraffe() |> ignore
        // Configure JsonSerializer to use Fable.JsonConverter
        let fableJsonSettings = JsonSerializerSettings()
        fableJsonSettings.Converters.Add(Fable.JsonConverter())

        services.AddSingleton<IJsonSerializer>(
            NewtonsoftJsonSerializer(fableJsonSettings)) |> ignore
        
    let configureLogging (loggerBuilder : ILoggingBuilder) =
        loggerBuilder.AddFilter(fun lvl -> lvl.Equals LogLevel.Debug)
                     .AddConsole()
                     .AddDebug() |> ignore

    let getPortsOrDefault defaultVal = defaultVal

    [<EntryPoint>]
    let main args =
        try
            let port = getPortsOrDefault 8085us

            let args = Array.toList args
            let clientPath =
                match args with
                | clientPath:: _  when Directory.Exists clientPath -> clientPath
                | _ ->
                    // did we start from server folder?
                    let devPath = Path.Combine("..","Client")
                    if Directory.Exists devPath then devPath
                    else
                        // maybe we are in root of project?
                        let devPath = Path.Combine("src","Client")
                        if Directory.Exists devPath then devPath
                        else @"./Client"
                |> Path.GetFullPath        

            WebHost
              .CreateDefaultBuilder()
              .UseWebRoot(clientPath)
              .UseContentRoot(clientPath)
              .ConfigureLogging(configureLogging)
              .Configure(Action<IApplicationBuilder> (configureApp port))
              .ConfigureServices(configureServices)
              .UseUrls("http://0.0.0.0:" + port.ToString() + "/")
              .Build()
              .Run()
                
            0
        with
        | exn ->
            let color = Console.ForegroundColor
            Console.ForegroundColor <- System.ConsoleColor.Red
            Console.WriteLine(exn.Message)
            Console.ForegroundColor <- color
            1
      