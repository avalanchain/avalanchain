namespace Avalanchain.Web.Controllers

open System.Net
open System.Net.Http
open System.Web.Http

open Avalanchain.Web.Models

/// Retrieves values.
[<RoutePrefix("api")>]
type NodeController() =
    inherit ApiController()

    //let node = Avalanchain.Node.defaultNode
    

    /// Gets all values.
    [<Route("node")>]
    member x.Get() = {
            Path = "aaa" //node.Path
            StreamsCount = 1u //uint32 (node.Streams.StreamMap.Count)
        }



//    /// Gets a single value at the specified index.
//    [<Route("cars/{id}")>]
//    member x.Get(request: HttpRequestMessage, id: int) =
//        if id >= 0 && values.Length > id then
//            request.CreateResponse(values.[id])
//        else 
//            request.CreateResponse(HttpStatusCode.NotFound)
//
