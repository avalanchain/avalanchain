import akka.actor.ActorSystem
import akka.stream.ActorMaterializer
import akka.stream.scaladsl.{ Source, Flow }
import akka.http.scaladsl.Http
import akka.http.scaladsl.model.ws.{ TextMessage, Message }
import akka.http.scaladsl.server.Directives

implicit val system = ActorSystem()
implicit val materializer = ActorMaterializer()

import Directives._

// The Greeter WebSocket Service expects a "name" per message and
// returns a greeting message for that name
val greeterWebSocketService =
  Flow[Message].collect {
      case tm: TextMessage =>
        println(s"Received: $tm")
        TextMessage(Source.single("Hello ") ++ tm.textStream)
      // ignore binary messages
    }

//#websocket-routing
val route =
  path("greeter") {
    get {
      handleWebSocketMessages(greeterWebSocketService)
    }
  }
//#websocket-routing

val bindingFuture = Http().bindAndHandle(route, "localhost", 8100)

println(s"Server online at http://localhost:8100/\nPress RETURN to stop...")
Console.readLine()

import system.dispatcher // for the future transformations
bindingFuture
  .flatMap(_.unbind()) // trigger unbinding from the port
  .onComplete(_ => system.terminate()) // and shutdown when done
