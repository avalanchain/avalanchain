package com.avalanchain.jwt.api

import java.security.{KeyPair, PrivateKey, PublicKey}
import java.util.UUID
import java.util.concurrent.ThreadLocalRandom

import akka.actor.{ActorSelection, ActorSystem}
import akka.event.{Logging, LoggingAdapter}
import akka.http.scaladsl.Http
import akka.http.scaladsl.Http.ServerBinding
import akka.stream.actor.ActorPublisher
import com.avalanchain.jwt.basicChain._
import com.avalanchain.jwt.jwt.actors.ActorNode
import com.avalanchain.jwt.jwt.actors.network.{DerivedChain, NewChain, NodeStatus}
import com.avalanchain.jwt.jwt.demo.{StockTick, YahooFinSource}
import com.avalanchain.jwt.utils.CirceCodecs
import com.rbmhtechnology.eventuate.adapter.stream.DurableEventProcessor._
import com.rbmhtechnology.eventuate.adapter.stream.DurableEventSource
import com.rbmhtechnology.eventuate.log.leveldb.LeveldbEventLog
import com.typesafe.config.ConfigFactory
import io.circe.Json
import io.circe.syntax._
import io.circe.parser._

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

class Main(port: Int) extends Config with CorsSupport with CirceSupport with CirceCodecs {

  import StockTick._

  val chainNode = new ChainNode("Main" + port, port, CurveContext.currentKeys, Set.empty)
  val demoNode: DemoNode = new DemoNode(chainNode)

  private val globalContext = ExecutionContext.global
  private implicit val system = ActorSystem("webapi", ConfigFactory.load("application.conf"))
  protected implicit val executor: ExecutionContext = system.dispatcher
  protected val log: LoggingAdapter = Logging(system, getClass)
  protected implicit val materializer = ActorMaterializer()

  var childNodes: List[Awaitable[Main]] = List.empty
  def startChild(): Unit = {
    childNodes = Future { new Main(0)}(globalContext) :: childNodes
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
    path("chat") {
      val src = chainNode.chatNode.source.map(i => TextMessage(i.asJson.noSpaces))

      extractUpgradeToWebSocket { upgrade =>
        complete(upgrade.handleMessagesWithSinkSource(Sink.ignore, src))
      }
    } ~
    path("accountEvents") {
      val src = chainNode.currencyNode.accountSource.map(i => TextMessage(i.asJson.noSpaces))

      extractUpgradeToWebSocket { upgrade =>
        complete(upgrade.handleMessagesWithSinkSource(Sink.ignore, src))
      }
    } ~
//    path("accounts") {
      //      val src = chainNode.currencyNode.accountsSource.map(i => TextMessage(i.asJson.noSpaces))
      //
      //      extractUpgradeToWebSocket { upgrade =>
      //        complete(upgrade.handleMessagesWithSinkSource(Sink.ignore, src))
      //      }
      //    } ~
    path("transactions") {
      val src = chainNode.currencyNode.transactionSource.map(i => TextMessage(i.asJson.noSpaces))

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

  def startHttp(localport: Int): Future[ServerBinding] = {
    val httpPort = localport + 1000

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
            corsHandler(new ChatService(chainNode).route) ~
            corsHandler(new CurrencyService(chainNode).route) ~
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
    bindingFuture
  }

  val httpServer = chainNode.localport.flatMap(port => startHttp(port))
  StdIn.readLine() // let it run until user presses return
  httpServer
    .flatMap(_.unbind()) // trigger unbinding from the port
    .onComplete(_ => system.terminate()) // and shutdown when done
}

object Main extends Main(2551) with App

object Main2 extends Main(2552) with App

object Main3 extends Main(2553) with App

object MainRnd extends Main(0) with App


object MainCmd extends App with CirceCodecs {
  val keyPair = CurveContext.currentKeys

  def newChain(jwtAlgo: JwtAlgo = JwtAlgo.HS512, initValue: Option[Json] = Some(Json.fromString("{}"))) = {
    val chainDef: ChainDef = ChainDef.New(jwtAlgo, UUID.randomUUID().toString, keyPair.getPublic, ResourceGroup.ALL, initValue.map(_.asString.getOrElse("{}")))
    val chainDefToken = TypedJwtToken[ChainDef](chainDef, keyPair.getPrivate)
    chainDefToken
  }

  def derivedChain(parentRef: ChainRef, jwtAlgo: JwtAlgo = JwtAlgo.HS512): (ChainDefToken, ChainDef.Derived) = {
    val chainDef = ChainDef.Derived(jwtAlgo, UUID.randomUUID().toString, keyPair.getPublic, ResourceGroup.ALL, parentRef,
      ChainDerivationFunction.Map("function(a) { return { b: a.e + ' Hello!' }; }"))
    val chainDefToken = TypedJwtToken[ChainDef](chainDef, keyPair.getPrivate)
    (chainDefToken, chainDef)
  }

  def toJson(j: String) = Json.fromString(j)

  val newChainDefToken = newChain()
  val derivedChainTuple = derivedChain(ChainRef(newChainDefToken))

  object ActorNode extends ActorNode {
    override val port: Int = 2222
    override val keyPair: KeyPair = CurveContext.currentKeys
  }
  import ActorNode._

  private val nodeIdToken: NodeIdToken = NodeIdToken("AC", "localhost", port, keyPair.getPublic, keyPair.getPrivate)
  val nc = new NewChain(nodeIdToken, newChainDefToken, keyPair)
  val dc = new DerivedChain(nodeIdToken, derivedChainTuple._1, keyPair, derivedChainTuple._2, nc.eventLog)

//  val printNCDES = nc.sourceFrame.runForeach(frm => println(s"NewChain DES: $frm"))
//  val printDCDES = dc.sourceFrame.runForeach(frm => println(s"DerivedChain DES: $frm"))

//  val printNCF = nc.sourceFrame.runForeach(frm => println(s"NewChain F: $frm"))
//  val printDCF = dc.sourceFrame.runForeach(frm => println(s"DerivedChain F: $frm"))
//
  val printNC = nc.sourceJson.runForeach(frm => println(s"NewChain: $frm"))
  val printDC = dc.sourceJson.runForeach(frm => println(s"DerivedChain: $frm"))

  nc.process()
  dc.process()

  Source.fromGraph(DurableEventSource(nc.eventLog))
    .runWith(Sink.foreach(e => println(s"aaa ${e.payload}")))

  Source(List("A", "B", "C")).map(e => Cmd(toJson(s"""{ "e": "${e}" }"""))).runWith(nc.sink)
}