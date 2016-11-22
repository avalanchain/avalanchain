package com.avalanchain.jwt.api

import java.security.{KeyPair, PrivateKey, PublicKey}
import java.util.UUID
import java.util.concurrent.ThreadLocalRandom

import akka.actor.ActorSystem
import akka.event.{Logging, LoggingAdapter}
import akka.http.scaladsl.Http
import com.avalanchain.jwt.basicChain.ChainDef
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
import com.avalanchain.jwt.jwt.actors.{ChainNode, ChainNodeFacade}
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
import io.circe.generic.auto._

import com.avalanchain.jwt.basicChain.KeysDto._

//trait ACJsonSupport extends DefaultJsonProtocol with SprayJsonSupport {
//  implicit val userInfoFormats = jsonFormat4(UserInfo)
//  implicit val userDataFormats = jsonFormat5(UserData)
//  implicit val pubKeysFormats = jsonFormat2(PubKey)
//  implicit val privKeysFormats = jsonFormat1(PrivKey)
//  implicit val keysFormats = jsonFormat2(Keys)
//}

class Main() extends Config with CorsSupport with CirceSupport {

  import com.avalanchain.jwt.basicChain.ChainDefCodecs._

  private implicit val system = ActorSystem()
  protected implicit val executor: ExecutionContext = system.dispatcher
  protected val log: LoggingAdapter = Logging(system, getClass)
  protected implicit val materializer: ActorMaterializer = ActorMaterializer()

//  import io.circe._, io.circe.generic.semiauto._
//
//  implicit val encoder: Encoder[ChainDef] = deriveEncoder
//  implicit val decoder: Decoder[ChainDef] = deriveDecoder

  def chainNode: ChainNode = new ChainNode(CurveContext.currentKeys, Set.empty)
  val demoNode: DemoNode = new DemoNode(chainNode)

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
    } //~
//      path("yahoo") {
//        val src = YahooFinSource().map(i => TextMessage(i.toString))
//
//        extractUpgradeToWebSocket { upgrade =>
//          complete(upgrade.handleMessagesWithSinkSource(Sink.ignore, src))
//        }
//      } ~
//      path("cluster") {
//        val system = ClusterService.firstNode
//        val monitor = ClusterService.startMonitor(system)
//        val src = Source.fromPublisher[String](ActorPublisher(monitor)).map(i => TextMessage(i))
//
//        extractUpgradeToWebSocket { upgrade =>
//          complete(upgrade.handleMessagesWithSinkSource(Sink.ignore, src))
//        }
//      } ~
//      path("newnode") {
//        get {
//          ClusterService.deployNode(0)
//          complete("Node added")
//        }
//      }
  }

  val routes =
    pathPrefix("swagger") {
      getFromResourceDirectory("swagger") ~ pathSingleSlash(get(redirect("index.html", StatusCodes.PermanentRedirect)))
    } ~
    pathPrefix("v1") {
      path("")(getFromResource("public/index.html")) ~
      corsHandler(new ChainService(chainNode).route) ~
//      corsHandler(new AdminService().route) ~
      corsHandler(new UsersService(userInfos, u => userInfos.exists(_ == u), u => addUserInfo(u)).route)
    } ~
    pathPrefix("ws") {
      wsRoute
    } ~
    corsHandler(pathSingleSlash(getFromResource("html/build/index.html"))) ~
    corsHandler(getFromResourceDirectory("html/build")) ~
    corsHandler(new SwaggerDocService(system, httpInterface, httpPort).routes)

  val bindingFuture = Http().bindAndHandle(
    handler = DebuggingDirectives.logRequestResult("log")(routes), interface = httpInterface, port = httpPort)

  println(s"Server online at $httpInterface:$httpPort/\nPress RETURN to stop...")
  StdIn.readLine() // let it run until user presses return
  bindingFuture
    .flatMap(_.unbind()) // trigger unbinding from the port
    .onComplete(_ => system.terminate()) // and shutdown when done
}

object Main extends Main with App with CirceSupport
