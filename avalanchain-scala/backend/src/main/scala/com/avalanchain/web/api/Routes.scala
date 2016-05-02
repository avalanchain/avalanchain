package com.avalanchain.web.api

import akka.http.javadsl.model.ContentTypes
import akka.http.scaladsl.model
import akka.http.scaladsl.model.{HttpEntity, StatusCodes}
import akka.http.scaladsl.model.ws.{Message, TextMessage}
import akka.http.scaladsl.server.Directives._
import akka.http.scaladsl.server.{ExceptionHandler, RejectionHandler, Route}
import akka.http.scaladsl.server.directives.WebSocketDirectives
import akka.stream.scaladsl.{Flow, Sink, Source}
import com.avalanchain.yahoo.YahooFinSource
import com.typesafe.scalalogging.StrictLogging

import scala.concurrent.forkjoin.ThreadLocalRandom

trait Routes extends CacheSupport with StrictLogging {

  private val exceptionHandler = ExceptionHandler {
    case e: Exception =>
      logger.error(s"Exception during client request processing: ${e.getMessage}", e)
      _.complete(StatusCodes.InternalServerError, "Internal server error")
  }

  private val rejectionHandler = RejectionHandler.default
  private val logDuration = extractRequestContext.flatMap { ctx =>
    val start = System.currentTimeMillis()
    // handling rejections here so that we get proper status codes
    mapResponse { resp =>
      val d = System.currentTimeMillis() - start
      logger.info(s"[${resp.status.intValue()}] ${ctx.request.method.name} ${ctx.request.uri} took: ${d}ms")
      resp
    } & handleRejections(rejectionHandler)
  }

  val greeterWebSocketService =
    Flow[Message].collect {
      case tm: TextMessage =>
        println(s"Received: $tm")
        TextMessage(Source.single("Hello ") ++ tm.textStream)
      // ignore binary messages
    }

  //#websocket-routing
  val wsRoute: Route = {
    path("greeter") {
      get {
        handleWebSocketMessages(greeterWebSocketService)
        //complete(HttpEntity(model.ContentTypes.`text/html(UTF-8)`, "<h1>Say hello to akka-http</h1>"))
      }
    } ~
    path("randomNums") {
      val src =
        Source.fromIterator(() => Iterator.continually(ThreadLocalRandom.current.nextInt()))
          .filter(i => i > 0 && i % 2 == 0).map(i => TextMessage(i.toString))

      extractUpgradeToWebSocket { upgrade =>
        complete(upgrade.handleMessagesWithSinkSource(Sink.ignore, src))
      }
    } ~
    path("yahoo") {
      val src = YahooFinSource().map(i => TextMessage(i.toString))

      extractUpgradeToWebSocket { upgrade =>
        complete(upgrade.handleMessagesWithSinkSource(Sink.ignore, src))
      }
    }
  }

  val routes =
    logDuration {
      handleExceptions(exceptionHandler) {
        cacheImages {
          encodeResponse {
//            pathPrefix("api") {
//              passwordResetRoutes ~
//                usersRoutes
//            } ~
            pathPrefix("ws") {
              wsRoute
            } ~
            getFromResourceDirectory("webapp") ~
            path("") {
              getFromResource("webapp/index.html")
            }
          }
        }
      }
    }
}

