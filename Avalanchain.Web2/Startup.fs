namespace Avalanchain.Web2

open Owin
open Microsoft.Owin
open System
open System.Net.Http
open System.Web
open System.Web.Http
open System.Web.Http.Owin
open Swashbuckle.Application

type HttpRoute = {
    controller : string
    action : string
    id : RouteParameter 
}

type ApiHttpRoute = {
    controller : string
    id : RouteParameter 
}

[<Sealed>]
type Startup() =

    static member RegisterWebApi(config: HttpConfiguration) =
        let a = { controller = "{controller}"; id = RouteParameter.Optional }
        // Configure routing
        config.MapHttpAttributeRoutes()
        config.Routes.MapHttpRoute(
            "DefaultApi", // Route name
            "api/{controller}/{id}", // URL with parameters
            { controller = "{controller}"; id = RouteParameter.Optional } // Parameter defaults
        ) |> ignore
        // Swagger configuration

        // Configure serialization
        config.Formatters.XmlFormatter.UseXmlSerializer <- true
        config.Formatters.JsonFormatter.SerializerSettings.ContractResolver <- Newtonsoft.Json.Serialization.CamelCasePropertyNamesContractResolver()

        // Additional Web API settings

        config.
            EnableSwagger(fun c -> c.SingleApiVersion("v1", "Avalanchain Node API") |> ignore).
            EnableSwaggerUi()

    member __.Configuration(builder: IAppBuilder) =
        let config = new HttpConfiguration()
        Startup.RegisterWebApi(config)
        builder.UseWebApi(config) |> ignore

