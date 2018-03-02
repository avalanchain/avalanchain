open System
open System.IO
open System.Threading.Tasks
open Microsoft.AspNetCore
open Microsoft.AspNetCore.Builder
open Microsoft.AspNetCore.Hosting
open Microsoft.Extensions.DependencyInjection

open Giraffe
open Giraffe.Swagger
open Giraffe.Swagger.Common
open Giraffe.Swagger.Analyzer
open Giraffe.Swagger.Generator
open Giraffe.Swagger.Dsl
open Analyzer
open SwaggerUi

open Fable.Remoting.Giraffe

open Shared

let clientPath = Path.Combine("..","Client") |> Path.GetFullPath
let port = 8085us

let docAddendums =
    fun (route:Analyzer.RouteInfos) (path:string,verb:HttpVerb,pathDef:PathDefinition) ->
    
        // routef params are automatically added to swagger, but you can customize their names like this 
        let changeParamName oldName newName (parameters:ParamDefinition list) =
            parameters |> Seq.find (fun p -> p.Name = oldName) |> fun p -> { p with Name = newName }
    
        match path,verb,pathDef with
        | _,_, def when def.OperationId = "say_hello_in_french" ->
            let firstname = def.Parameters |> changeParamName "arg0" "Firstname"
            let lastname = def.Parameters |> changeParamName "arg1" "Lastname"
            "/hello/{Firstname}/{Lastname}", verb, { def with Parameters = [firstname; lastname] }
        | "/", HttpVerb.Get,def ->
            // This is another solution to add operation id or other infos
            path, verb, { def with OperationId = "Home"; Tags=["home page"] }
        
        | _ -> path,verb,pathDef


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


let webApp : HttpHandler =
  let counterProcotol = 
    { getInitCounter = getInitCounter >> Async.AwaitTask }
  // creates a HttpHandler for the given implementation
  choose [swaggerOf
            (choose [ route  "/test"       >=> text "test" 
                      route  "/test2"       >=> text "test12" 
                      GET >=>
                         choose [
                              route  "/"           >=> text "index" 
                              route  "/ping"       >=> text "pong"]
            ]) |> withConfig docsConfig
          route  "/termsOfService"       >=> text "TODO: Add Terms of Service" 
          FableGiraffeAdapter.httpHandlerWithBuilderFor counterProcotol Route.builder ]

                      

let configureApp  (app : IApplicationBuilder) =
  app.UseStaticFiles()
     .UseGiraffe webApp

let configureServices (services : IServiceCollection) =
    services.AddGiraffe() |> ignore

WebHost
  .CreateDefaultBuilder()
  .UseWebRoot(clientPath)
  .UseContentRoot(clientPath)
  .Configure(Action<IApplicationBuilder> configureApp)
  .ConfigureServices(configureServices)
  .UseUrls("http://0.0.0.0:" + port.ToString() + "/")
  .Build()
  .Run()