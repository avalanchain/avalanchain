﻿open System
open System.IO
open System.Threading.Tasks
open Microsoft.AspNetCore
open Microsoft.AspNetCore.Builder
open Microsoft.AspNetCore.Hosting
open Microsoft.Extensions.DependencyInjection

open Giraffe

open Fable.Remoting.Giraffe

open Shared

let clientPath = Path.Combine("..","Client") |> Path.GetFullPath
let port = 8085us

let getInitCounter () : Task<Counter> = task { return 42 }

let webApp : HttpHandler =
  let counterProcotol = 
    { getInitCounter = getInitCounter >> Async.AwaitTask }
  // creates a HttpHandler for the given implementation
  FableGiraffeAdapter.httpHandlerWithBuilderFor counterProcotol Route.builder

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