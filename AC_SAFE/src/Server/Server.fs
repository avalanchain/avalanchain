open System
open System.IO
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

open Shared
open avalanchain.Common.MatchingEngine
open System

let clientPath = Path.Combine("..","Client") |> Path.GetFullPath
let port = 8085us

let wsConnectionManager = ConnectionManager()

let docAddendums =
    fun (route:Analyzer.RouteInfos) (path:string,verb:HttpVerb,pathDef:PathDefinition) ->
    
        // routef params are automatically added to swagger, but you can customize their names like this 
        let changeParamName oldName newName (parameters:ParamDefinition list) =
            parameters |> Seq.find (fun p -> p.Name = oldName) |> fun p -> { p with Name = newName }
    
        let tagPartitioner (pathPrefix: string) tag (path: string, verb, pathDef: PathDefinition) = 
            let pathDef = if path.ToLowerInvariant().StartsWith (pathPrefix.ToLowerInvariant()) then { pathDef with Tags=[ tag ] } else pathDef
            path, verb, pathDef

        let adjustOperationId (path: string, verb, pathDef: PathDefinition) = 
            path, verb, { pathDef with OperationId = path.Substring(1).Replace("/", "_").ToLowerInvariant() }

        let addQueryParam (subPaths: string list) name typ required (path: string, verb, pathDef: PathDefinition) = 
            let pathDef =   if subPaths |> List.exists (fun spath -> path.ToLowerInvariant().Contains (spath.ToLowerInvariant())) 
                            then 
                                let param = { ParamDefinition.Name = name
                                              Type = Primitive (typ, "") |> Some
                                              In = "query"
                                              Required = required }
                                { pathDef with Parameters = param :: pathDef.Parameters } 
                            else pathDef
            path, verb, pathDef

        let (path,verb,pathDef) = 
            (path,verb,pathDef) 
            |> adjustOperationId
            |> tagPartitioner "/api/exchange/" "Exchange"
            |> addQueryParam ["OrderStack"; "OrderStackView"] "symbol" "string" false
            |> addQueryParam ["OrderStackView"] "maxDepth" "integer" false
            |> addQueryParam ["GetOrder"; "GetOrder2"] "orderID" "string" true

        match path, verb, pathDef with
        | _, _, def when def.OperationId = "say_hello_in_french" ->
            let firstname = def.Parameters |> changeParamName "arg0" "Firstname"
            let lastname = def.Parameters |> changeParamName "arg1" "Lastname"
            "/hello/{Firstname}/{Lastname}", verb, { def with Parameters = [ firstname; lastname ] }

        | "/", HttpVerb.Get, def ->
            // This is another solution to add operation id or other infos
            path, verb, { def with OperationId = "Home"; Tags=["Home Page"] }
       
        | _ -> path, verb, pathDef


let docsConfig c = 
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

let matchingService = Facade.MatchingService(5M<price>, 100UL, false)

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

let webApp(wsConnectionManager: ConnectionManager) cancellationToken : HttpHandler =
  let counterProcotol = 
    { getInitCounter = getInitCounter >> Async.AwaitTask }
  // creates a HttpHandler for the given implementation
  choose [swaggerOf
            (choose [ route  "/test"       >=> text "test" 
                      route  "/test2"       >=> text "test12"
                      subRouteCi "/api" (
                        subRouteCi "/Exchange" (
                          choose [
                            POST >=> 
                                routeCi  "/SubmitOrder" >=> //operationId "submit_order" ==> consumes typeof<OrderCommand> ==> produces typeof<OrderCommand> ==>
                                    bindJson<OrderCommand> (matchingService.SubmitOrder >> Successful.OK )
                            GET >=>
                              choose [
                                route  "/OrderStack"      >=> bindSymbolQuery matchingService.OrderStack
                                route  "/OrderStackView"  >=> bindSymbolMaxDepthQuery matchingService.OrderStackView
                                route  "/MainSymbol"      >=> json matchingService.MainSymbol |> Successful.ok
                                route  "/Symbols"         >=> json matchingService.Symbols |> Successful.ok
                                route  "/GetOrder"        >=> bindQuery<OrderIDQuery> None 
                                                                (fun oq -> match oq.orderID with
                                                                            | None -> parsingError "Missing order ID"
                                                                            | Some guidStr -> guidStr |> Guid.Parse |> matchingService.OrderById |> json |> Successful.ok)
                                route  "/GetOrder2"       >=> bindOrderIDQuery matchingService.OrderById2
                                route  "/GetOrders"       >=> json matchingService.Orders |> Successful.ok
                                route  "/OrderCommands"   >=> bindPageStartQuery matchingService.OrderCommands
                                route  "/OrderEvents"     >=> bindPageStartQuery matchingService.OrderEvents
                                route  "/FullOrders"      >=> bindPageStartQuery matchingService.FullOrders
                                route  "/OrderCommandsCount" >=> primitive matchingService.OrderCommandsCount |> Successful.ok
                                route  "/OrderEventsCount"   >=> primitive matchingService.OrderEventsCount |> Successful.ok
                                route  "/FullOrdersCount"    >=> primitive matchingService.FullOrdersCount |> Successful.ok
                                route  "/LastOrderCommands"  >=> bindPageQuery matchingService.LastOrderCommands
                                route  "/LastOrderEvents"    >=> bindPageQuery matchingService.LastOrderEvents
                                route  "/LastFullOrders"     >=> bindPageQuery matchingService.LastFullOrders
                                route  "/SymbolOrderCommands"   >=> bindSymbolPageStartQuery matchingService.SymbolOrderCommands
                                route  "/SymbolOrderEvents"     >=> bindSymbolPageStartQuery matchingService.SymbolOrderEvents
                                route  "/SymbolFullOrders"      >=> bindSymbolPageStartQuery matchingService.SymbolFullOrders
                                route  "/SymbolOrderCommandsCount" >=> bindSymbolUInt64Query matchingService.SymbolOrderCommandsCount
                                route  "/SymbolOrderEventsCount"   >=> bindSymbolUInt64Query matchingService.SymbolOrderEventsCount
                                route  "/SymbolFullOrdersCount"    >=> bindSymbolUInt64Query matchingService.SymbolFullOrdersCount
                                route  "/SymbolLastOrderCommands"  >=> bindSymbolPageQuery matchingService.SymbolLastOrderCommands
                                route  "/SymbolLastOrderEvents"    >=> bindSymbolPageQuery matchingService.SymbolLastOrderEvents
                                route  "/SymbolLastFullOrders"     >=> bindSymbolPageQuery matchingService.SymbolLastFullOrders
                              ]
                          ])
                      ) 
                      GET >=>
                         choose [
                              route  "/"           >=> text "index" 
                              route  "/ping"       >=> text "pong"
                    ]
            ]) |> withConfig docsConfig
          route "/wsecho" >=> (wsConnectionManager.CreateSocket(
                                (fun _ref -> task { return () }),
                                (fun ref msg -> ref.SendTextAsync("Hi " + msg, cancellationToken)),
                                cancellationToken = cancellationToken)) 
          route  "/termsOfService"       >=> text "TODO: Add Terms of Service" 
          FableGiraffeAdapter.httpHandlerWithBuilderFor counterProcotol Route.builder ]

                      

let configureApp  (app : IApplicationBuilder) =
  app
    .UseStaticFiles()
    .UseWebSockets()
    .UseGiraffe (webApp wsConnectionManager CancellationToken.None)

let configureServices (services : IServiceCollection) =
    services.AddGiraffe() |> ignore
    // Configure JsonSerializer to use Fable.JsonConverter
    let fableJsonSettings = JsonSerializerSettings()
    fableJsonSettings.Converters.Add(Fable.JsonConverter())

    services.AddSingleton<IJsonSerializer>(
        NewtonsoftJsonSerializer(fableJsonSettings)) |> ignore
    
let configureLogging (loggerBuilder : ILoggingBuilder) =
    loggerBuilder.AddFilter(fun lvl -> lvl.Equals LogLevel.Error)
                 .AddConsole()
                 .AddDebug() |> ignore

let getPortsOrDefault defaultVal = defaultVal

[<EntryPoint>]
let main args =
    try
        let port = getPortsOrDefault 8085us

        WebHost
          .CreateDefaultBuilder()
          .UseWebRoot(clientPath)
          .UseContentRoot(clientPath)
          .ConfigureLogging(configureLogging)
          .Configure(Action<IApplicationBuilder> configureApp)
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
  