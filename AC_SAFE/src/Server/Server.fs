open System
open System.IO
open System.Threading
open System.Threading.Tasks
open System.Reactive.Subjects
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

open Shared
open Avalanchain.Common.MatchingEngine
open Avalanchain.Core
open Avalanchain.Server.WebSocketActor

let wsConnectionManager = ConnectionManager()

module ParamHelper =
    let tagPartitioner (pathPrefix: string) tag (path: string, verb, pathDef: PathDefinition) = 
        let pathDef = if path.ToLowerInvariant().StartsWith (pathPrefix.ToLowerInvariant()) then { pathDef with Tags=[ tag ] } else pathDef
        path, verb, pathDef

    let adjustOperationId (path: string, verb, pathDef: PathDefinition) = 
        path, verb, { pathDef with OperationId = path.Substring(1).Replace("/", "_").ToLowerInvariant() }

    let addParam (paramIn: ParamContainer) paramType name required (pathPostfixes: string list) (path: string, verb, pathDef: PathDefinition) = 
        let pathDef =   if pathPostfixes |> List.exists (fun spath -> path.ToLowerInvariant().EndsWith (spath.ToLowerInvariant())) 
                        then 
                            let param = { ParamDefinition.Name = name
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
open System.Collections.Concurrent

let docAddendums =
    fun (route:Analyzer.RouteInfos) (path:string,verb:HttpVerb,pathDef:PathDefinition) ->
    
        // routef params are automatically added to swagger, but you can customize their names like this 
        let changeParamName oldName newName (parameters:ParamDefinition list) =
            parameters |> Seq.find (fun p -> p.Name = oldName) |> fun p -> { p with Name = newName }
    
        let (path,verb,pathDef) = 
            (path,verb,pathDef) 
            |> adjustOperationId
            |> tagPartitioner "/api/exchange/" "Exchange"
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

let primitive value : HttpHandler = text (value.ToString())

let bindSymbolQuery successHandler = 
    bindQuery<SymbolQuery> None (fun qs -> qs.symbol |> Option.defaultValue "" |> Symbol |> successHandler |> json |> Successful.ok)

let bindSymbolUInt64Query successHandler = 
    bindQuery<SymbolQuery> None (fun qs -> qs.symbol |> Option.defaultValue "" |> Symbol |> successHandler |> primitive |> Successful.ok)

let bindSymbolMaxDepthQuery successHandler = 
    bindQuery<SymbolMaxDepthQuery> None (fun qs -> (successHandler (qs.symbol |> Option.defaultValue "" |> Symbol) (qs.maxDepth |> Option.defaultValue 10)) |> json |> Successful.ok)

let bindOrderIDQuery successHandler = 
    bindQuery<OrderIDQuery> None (fun qs -> qs.orderID |> Option.defaultValue "" |> successHandler |> json |> Successful.ok)

let bindPageStartQuery successHandler = 
    bindQuery<PageStartQuery> None (fun pq -> successHandler (pq.startIndex |> Option.defaultValue 0UL) (pq.pageSize |> Option.defaultValue 0u) |> json |> Successful.ok)

let bindPageQuery successHandler = 
    bindQuery<PageQuery> None (fun qs -> qs.pageSize |> Option.defaultValue 0u |> successHandler |> json |> Successful.ok)

let bindSymbolPageStartQuery successHandler = 
    bindQuery<PageSymbolStartQuery> None (fun pq -> successHandler (pq.symbol |> Option.defaultValue "" |> Symbol) (pq.startIndex |> Option.defaultValue 0UL) (pq.pageSize |> Option.defaultValue 0u) |> json |> Successful.ok)

let bindSymbolPageQuery successHandler = 
    bindQuery<PageSymbolPageQuery> None (fun pq -> successHandler (pq.symbol |> Option.defaultValue "" |> Symbol) (pq.pageSize |> Option.defaultValue 0u) |> json |> Successful.ok)                          

let logger str (msg: WebSocketMessage) = 
    match msg with | WebSocketMessage msg -> printfn "%s:%s" str msg 
    msg

let webApp (wsConnectionManager: ConnectionManager) (ms: Facade.MatchingService) port cancellationToken : HttpHandler =
  let counterProcotol = 
    { getInitCounter = getInitCounter >> Async.AwaitTask }
  // creates a HttpHandler for the given implementation
  choose [swaggerOf
            (choose [ route  "/test"       >=> text "test" 
                      route  "/test2"       >=> text "test12"
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
                                    routeCi  "/GetOrder2"       >=> bindOrderIDQuery ms.OrderById2
                                    routeCi  "/GetOrders"       >=> bindPageStartQuery ms.Orders
                                    routeCi  "/OrderCommands"   >=> bindPageStartQuery ms.OrderCommands
                                    routeCi  "/OrderEvents"     >=> bindPageStartQuery ms.OrderEvents
                                    routeCi  "/FullOrders"      >=> bindPageStartQuery ms.FullOrders
                                    routeCi  "/OrderCommandsCount" >=> primitive ms.OrderCommandsCount |> Successful.ok
                                    routeCi  "/OrderEventsCount"   >=> primitive ms.OrderEventsCount |> Successful.ok
                                    routeCi  "/FullOrdersCount"    >=> primitive ms.FullOrdersCount |> Successful.ok
                                    routeCi  "/LastOrderCommands"  >=> bindPageQuery ms.LastOrderCommands
                                    routeCi  "/LastOrderEvents"    >=> bindPageQuery ms.LastOrderEvents
                                    routeCi  "/LastFullOrders"     >=> bindPageQuery ms.LastFullOrders
                                    routeCi  "/SymbolOrderCommands"   >=> bindSymbolPageStartQuery ms.SymbolOrderCommands
                                    routeCi  "/SymbolOrderEvents"     >=> bindSymbolPageStartQuery ms.SymbolOrderEvents
                                    routeCi  "/SymbolFullOrders"      >=> bindSymbolPageStartQuery ms.SymbolFullOrders
                                    routeCi  "/SymbolOrderCommandsCount" >=> bindSymbolUInt64Query ms.SymbolOrderCommandsCount
                                    routeCi  "/SymbolOrderEventsCount"   >=> bindSymbolUInt64Query ms.SymbolOrderEventsCount
                                    routeCi  "/SymbolFullOrdersCount"    >=> bindSymbolUInt64Query ms.SymbolFullOrdersCount
                                    routeCi  "/SymbolLastOrderCommands"  >=> bindSymbolPageQuery ms.SymbolLastOrderCommands
                                    routeCi  "/SymbolLastOrderEvents"    >=> bindSymbolPageQuery ms.SymbolLastOrderEvents
                                    routeCi  "/SymbolLastFullOrders"     >=> bindSymbolPageQuery ms.SymbolLastFullOrders
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
                                            webSocketClient url (printfn "%s") (fun d _ _ -> (fun m -> m |> logger "Rec2" |> dispatcher)) cancellationToken
                                        fun m -> m |> logger "ClientRec" |> clientDispatcher
                                    ) 
                                cancellationToken
          route  "/termsOfService"       >=> text "TODO: Add Terms of Service" 
          FableGiraffeAdapter.httpHandlerWithBuilderFor counterProcotol Route.builder ]

let matchingService = Facade.MatchingService.Instance

let configureApp port (app : IApplicationBuilder) =
  app
    .UseStaticFiles()
    .UseWebSockets()
    .UseGiraffe (webApp wsConnectionManager matchingService port CancellationToken.None)

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
          .UseUrls("http://localhost:" + port.ToString() + "/")
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
  