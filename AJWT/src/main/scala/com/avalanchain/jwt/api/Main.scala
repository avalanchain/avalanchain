package com.avalanchain.jwt.api

import java.security.{KeyPair, PrivateKey, PublicKey}
import java.util.UUID
import java.util.concurrent.{ThreadLocalRandom}

import akka.actor.{ActorSelection, ActorSystem}
import akka.event.{Logging, LoggingAdapter}
import akka.http.scaladsl.Http
import akka.stream.actor.ActorPublisher
import com.avalanchain.jwt.basicChain.ChainDef
import com.avalanchain.jwt.jwt.actors.network.NodeStatus
import com.avalanchain.jwt.jwt.demo.{StockTick, YahooFinSource}
import com.avalanchain.jwt.utils.CirceEncoders
import com.typesafe.config.ConfigFactory

import scala.concurrent.{Awaitable, Future}
//import akka.http.scaladsl.marshallers.sprayjson.SprayJsonSupport
import akka.http.scaladsl.model.ws.{Message, TextMessage}
import akka.http.scaladsl.model.{StatusCodes, _}
import akka.http.scaladsl.server.{Directive0, Route}
import akka.stream.ActorMaterializer
import akka.stream.scaladsl.{Flow, Sink, Source}
import akka.util.Timeout
import com.avalanchain.jwt.basicChain.ChainDefToken
import com.avalanchain.jwt.jwt.CurveContext
import com.avalanchain.jwt.jwt.account.principals.{User, UserData}
import com.avalanchain.jwt.jwt.actors.ChainNode
import com.avalanchain.jwt.jwt.demo.DemoNode
import com.avalanchain.jwt.utils.Config
import com.github.swagger.akka._
import com.github.swagger.akka.model.Info
import de.heikoseeberger.akkahttpcirce._
import io.swagger.annotations._
import kantan.csv.ops._
import kantan.csv.{RowDecoder, RowEncoder}

import scala.concurrent.ExecutionContext
import scala.io.StdIn
//import io.swagger.models.Path
import javax.ws.rs._

import akka.http.scaladsl.model.HttpResponse
import akka.http.scaladsl.model.headers._
import akka.http.scaladsl.server.Directives
import akka.http.scaladsl.server.Directives._
import akka.http.scaladsl.server.directives.DebuggingDirectives
import io.circe.{Decoder, Encoder}
import io.circe.syntax._
import io.circe.generic.auto._

import com.avalanchain.jwt.KeysDto._

class Main(port: Int) extends Config with CorsSupport with CirceSupport with CirceEncoders {

  import com.avalanchain.jwt.basicChain.ChainDefCodecs._
  import StockTick._

  val chainNode = new ChainNode(port, CurveContext.currentKeys, Set.empty)
//  val chainNodeMonitor = ActorSelection(chainNode.node, "monitor")
  val demoNode: DemoNode = new DemoNode(chainNode)

  private implicit val system = ActorSystem("webapi", ConfigFactory.load("application.conf"))
  protected implicit val executor: ExecutionContext = system.dispatcher
  protected val log: LoggingAdapter = Logging(system, getClass)
  protected implicit val materializer = ActorMaterializer()

  var childNodes: List[Awaitable[Main]] = List.empty
  def startChild(): Unit = {
    childNodes = Future { new Main(0)}(ExecutionContext.global) :: childNodes
  }

  def userInfos() = {
    val userDatas = getClass.getResourceAsStream("/public/mock_users.csv")

    implicit val userDecoder: RowDecoder[UserData] = RowDecoder.decoder(0, 1, 2, 3, 4)(UserData.apply)
    val reader = userDatas.asCsvReader[UserData](',', true)

    reader.map(_.get).map(User(UUID.randomUUID(), _)).toList
  }

  def addUserInfo(user: UserData): User = {
    val infos = userInfos()
    implicit val userEncoder: RowEncoder[UserData] = RowEncoder.caseEncoder(0, 1, 2, 3, 4)(UserData.unapply)
//    new File(rawDataFile.toAbsolutePath().toString() + "2").delete()
//    val out = new java.io.File(rawDataFile.toAbsolutePath().toString())
//    val writer = out.asCsvWriter[UserInfo](',', List("username", "firstname", "lastname", "email", "ip_address"))
//    writer.write(user :: infos).close()
    User(UUID.randomUUID(), user)
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
    path("nodes") {
      val src = chainNode.monitorSource().map(i => TextMessage(i.asJson.noSpaces))

      extractUpgradeToWebSocket { upgrade =>
        complete(upgrade.handleMessagesWithSinkSource(Sink.ignore, src))
      }
    } ~
    path("yahoo") {
      val src = YahooFinSource().map(i => TextMessage(i.asJson.noSpaces))

      extractUpgradeToWebSocket { upgrade =>
        complete(upgrade.handleMessagesWithSinkSource(Sink.ignore, src))
      }
    } //~
//    path("tick") {
//      val src = demoNode.tickerSource(0, 2000).right.toOption.get.map(i => TextMessage(i.toString))
//
//      extractUpgradeToWebSocket { upgrade =>
//        complete(upgrade.handleMessagesWithSinkSource(Sink.ignore, src))
//      }
//    } //~
  }

  val httpPort = port + 1000

  val localhost = "localhost"

  val routes =
    pathPrefix("swagger") {
      getFromResourceDirectory("swagger") ~ pathSingleSlash(get(redirect("index.html", StatusCodes.PermanentRedirect)))
    } ~
    pathPrefix("v1") {
      path("")(getFromResource("public/index.html")) ~
      corsHandler(new NodeService(chainNode, startChild).route) ~
      corsHandler(new ChainService(chainNode).route) ~
      corsHandler(new AdminService().route) ~
      corsHandler(new UsersService(userInfos, u => userInfos.exists(_ == u), u => addUserInfo(u)).route)
    } ~
    pathPrefix("ws") {
      wsRoute
    } ~
    corsHandler(pathSingleSlash(getFromResource("html/index.html"))) ~
    corsHandler(getFromResourceDirectory("html/")) ~
    corsHandler(new SwaggerDocService(system, localhost, httpPort).routes)

  val bindingFuture = Http().bindAndHandle(
    handler = DebuggingDirectives.logRequestResult("log")(routes), interface = localhost, port = httpPort)

  println(s"Server online at ${localhost}:$httpPort/\nPress RETURN to stop...")
  StdIn.readLine() // let it run until user presses return
  bindingFuture
    .flatMap(_.unbind()) // trigger unbinding from the port
    .onComplete(_ => system.terminate()) // and shutdown when done
}

object Main extends Main(2551) with App

object Main2 extends Main(2552) with App

object Main3 extends Main(2553) with App

object MainRnd extends Main(0) with App