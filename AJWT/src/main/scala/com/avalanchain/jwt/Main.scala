package com.avalanchain.jwt

import java.io.File
import java.security.{KeyPair, PrivateKey, PublicKey}
import java.util.UUID
import java.util.concurrent.ThreadLocalRandom

import akka.actor.{ActorRef, ActorSystem}
import akka.event.{Logging, LoggingAdapter}
import akka.http.scaladsl.Http
import akka.http.scaladsl.marshallers.sprayjson.SprayJsonSupport
import akka.http.scaladsl.model.{StatusCodes, _}
import akka.http.scaladsl.server.{Directive0, Route}
import akka.stream.ActorMaterializer
import akka.util.Timeout
import com.avalanchain.jwt.jwt.{CurveContext, UserInfo}
import CurveContext._
import akka.http.scaladsl.model.ws.{Message, TextMessage}
import akka.stream.scaladsl.{Flow, Sink, Source}
import com.avalanchain.jwt.basicChain.{ChainDefToken, ChainRef}
import com.avalanchain.jwt.jwt.account.principals.{User, UserData}
import com.avalanchain.jwt.jwt.actors.{ChainNode, ChainNodeFacade}
import com.avalanchain.jwt.jwt.demo.DemoNode
import com.avalanchain.jwt.utils.Config

import scala.concurrent.ExecutionContext
import scala.io.StdIn
import com.github.swagger.akka._
import com.github.swagger.akka.model.Info
import de.heikoseeberger.akkahttpcirce._
import kantan.csv.{RowDecoder, RowEncoder}
import kantan.csv.ops._

import cats.implicits._

import scala.collection.Map
import spray.json.DefaultJsonProtocol
import io.swagger.annotations._
//import io.swagger.models.Path
import javax.ws.rs._

import io.circe.{Decoder, DecodingFailure, Encoder, Json}
import io.circe.syntax._
import io.circe.parser._
import io.circe.generic.auto._

import scala.reflect.runtime.{universe=>ru}

import akka.http.scaladsl.server.Directives
import akka.http.scaladsl.server.Directives._
import akka.http.scaladsl.server.directives.DebuggingDirectives
import akka.http.scaladsl.model.headers._
import akka.http.scaladsl.model.HttpMethods._
import akka.http.scaladsl.model.HttpResponse

object KeysDto {
  case class PubKey(X: String, Y: String)
  case class PrivKey(S: String)
  case class Keys(priv: PrivKey, pub: PubKey)

  implicit def toPubKeyDto(key: PublicKey) = {
    val pkstr = new String(key.toString.toCharArray.map(_.toByte).filter(b => b != 10 && b != 13).map(_.toChar))
    val pattern = """.*X: ([0-9a-f]+) +Y: ([0-9a-f]+).*""".r
    val pattern(x, y) = pkstr
    PubKey(x, y)
  }

  def toPrivKeyDto(key: PrivateKey) = {
    val s = key.toString.substring(31).trim
    PrivKey(s)
  }

  def toKeysDto(keys: KeyPair) = {
    Keys(toPrivKeyDto(keys.getPrivate), toPubKeyDto(keys.getPublic))
  }
}

import KeysDto._

trait ACJsonSupport extends DefaultJsonProtocol with SprayJsonSupport {
  implicit val userInfoFormats = jsonFormat4(UserInfo)
  implicit val userDataFormats = jsonFormat5(UserData)
  implicit val pubKeysFormats = jsonFormat2(PubKey)
  implicit val privKeysFormats = jsonFormat1(PrivKey)
  implicit val keysFormats = jsonFormat2(Keys)
}

//see https://groups.google.com/forum/#!topic/akka-user/5RCZIJt7jHo
trait CorsSupport {

  //this directive adds access control headers to normal responses
  private def addAccessControlHeaders: Directive0 = {
    mapResponseHeaders { headers =>
      `Access-Control-Allow-Origin`.* +:
        `Access-Control-Allow-Credentials`(true) +:
        `Access-Control-Allow-Headers`("Authorization", "Content-Type", "X-Requested-With") +:
        headers
    }
  }

  //this handles preflight OPTIONS requests. TODO: see if can be done with rejection handler,
  //otherwise has to be under addAccessControlHeaders
  private def preflightRequestHandler: Route = options {
    complete(HttpResponse(200).withHeaders(
      `Access-Control-Allow-Methods`(HttpMethods.OPTIONS, HttpMethods.POST, HttpMethods.PUT, HttpMethods.GET, HttpMethods.DELETE)
    )
    )
  }

  def corsHandler(r: Route) = addAccessControlHeaders {
    preflightRequestHandler ~ r
  }
}

@Path("/users")
@Api(value = "/users", produces = "application/json")
class UsersService(getUsers: () => List[User], userExists: UserData => Boolean, addUser: UserData => User)(implicit executionContext: ExecutionContext)
  extends Directives with CorsSupport /*with ACJsonSupport*/ with CirceSupport {

  import scala.concurrent.duration._

  implicit val timeout = Timeout(2.seconds)

  val route = pathPrefix("users") {
    getall ~ add
  }

  @ApiOperation(value = "Add User", notes = "", nickname = "addUser", httpMethod = "POST")
  @ApiImplicitParams(Array(
    new ApiImplicitParam(name = "body", value = "\"User\" to add", required = true,
      dataType = "com.avalanchain.jwt.jwt.account.principals.UserData", paramType = "body")
  ))
  @ApiResponses(Array(
    new ApiResponse(code = 201, message = "User added", response = classOf[User]),
    new ApiResponse(code = 409, message = "Internal server error")
  ))
  def add =
    post {
      entity(as[UserData]) { user =>
        val exists = userExists(user)
        if (exists) complete(StatusCodes.Conflict, None)
        else {
          val newUser = addUser(user)
          complete(StatusCodes.Created, Some(newUser))
        }
      }
    }

  @ApiOperation(httpMethod = "GET", response = classOf[List[User]], value = "Returns the list of registered Users")
  @ApiResponses(Array(
    new ApiResponse(code = 200, message = "Users registered", response = classOf[List[User]])
  ))
  def getall =
    get {
      completeWith(instanceOf[List[User]])(_(getUsers()))
    }
}


@Path("admin")
@Api(value = "/admin", produces = "application/json")
class AdminService()
  extends Directives with CorsSupport /*with ACJsonSupport*/ with CirceSupport {

  val route = pathPrefix("admin") {
    path("newKeys") {
      newKeys
    } ~
      path("getKeys") {
        getKeys
      } ~
      path("key") {
        key
      }
  }

  @Path("newKeys")
  @ApiOperation(notes = "Copy key from screen", httpMethod = "GET", value = "Returns a newly generated key pair")
  @ApiResponses(Array(
    new ApiResponse(code = 200, message = "New keys generated", response = classOf[Keys])
  ))
  def newKeys =
    get {
      complete {
        val keys = CurveContext.newKeys()
        toKeysDto(keys)
      }
    }

  @Path("getKeys")
  @ApiOperation(notes = "Copy key from screen", httpMethod = "GET", value = "Returns key pair currently used")
  @ApiResponses(Array(
    new ApiResponse(code = 200, message = "Current Key Pair", response = classOf[Keys])
  ))
  def getKeys =
    get {
      complete {
        val keys = CurveContext.savedKeys()
        toKeysDto(keys)
      }
    }

  @Path("key")
  @ApiOperation(notes = "Copy key from screen", httpMethod = "GET", value = "Returns current Public Key in use")
  @ApiResponses(Array(
    new ApiResponse(code = 200, message = "Current Public Key", response = classOf[PubKey])
  ))
  def key =
    get {
      complete {
        val keys: KeyPair = CurveContext.savedKeys()
        toPubKeyDto(keys.getPublic)
      }
    }
}

//@Path("chains")
//@Api(value = "/chains", produces = "application/json")
//class ChainService(chainNode: ChainNode)
//  extends Directives with CorsSupport /*with ACJsonSupport*/ with CirceSupport {
//  import scala.concurrent.duration._
//
//  implicit val timeout = Timeout(2.seconds)
//  val cnf = new ChainNodeFacade(chainNode)
//
//  //val a = List[ChainDefToken]().asJson
//
//  val route = pathPrefix("chains") {
//    allchains ~ newchain
//  }
//
//  @ApiOperation(value = "Create New Chain", notes = "", nickname = "newchain", httpMethod = "POST")
//  @ApiResponses(Array(
//    new ApiResponse(code = 201, message = "Chain created", response = classOf[ChainDefToken]),
//    new ApiResponse(code = 409, message = "Internal server error")
//  ))
//  def newchain =
//    post {
//      complete(StatusCodes.Created, cnf.newChain().chainDefToken)
//    }
//
//  @ApiOperation(httpMethod = "GET", response = classOf[List[ChainDefToken]], value = "Returns the list of active chains")
//  @ApiResponses(Array(
//    new ApiResponse(code = 200, message = "Active Chains", response = classOf[List[ChainDefToken]])
//  ))
//  def allchains =
//    get {
//      completeWith(instanceOf[List[ChainDefToken]])(_(cnf.chains().values.toList))
//    }
//}

object Main extends App with Config with CorsSupport with CirceSupport {
  private implicit val system = ActorSystem()
  protected implicit val executor: ExecutionContext = system.dispatcher
  protected val log: LoggingAdapter = Logging(system, getClass)
  protected implicit val materializer: ActorMaterializer = ActorMaterializer()

  import java.nio.file.{Paths, Files}

//  def chainNode: ChainNode = new ChainNode(CurveContext.currentKeys, Set.empty)
//  val demoNode: DemoNode = new DemoNode(chainNode)

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

  class SwaggerDocService(system: ActorSystem) extends SwaggerHttpService with HasActorSystem {
    override implicit val actorSystem: ActorSystem = system
    override implicit val materializer: ActorMaterializer = ActorMaterializer()
    override val apiTypes = Seq(/*ru.typeOf[ChainService], */ru.typeOf[AdminService], ru.typeOf[UsersService])
    override val host = s"$httpInterface:$httpPort" //the url of your api, not swagger's json endpoint
    override val basePath = "/v1"    //the basePath for the API you are exposing
    override val apiDocsPath = "api-docs" //where you want the swagger-json endpoint exposed
    override val info = Info(version = "1.0") //provides license and other description details
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
//      corsHandler(new ChainService(chainNode).route) ~
      corsHandler(new AdminService().route) ~
      corsHandler(new UsersService(userInfos, u => userInfos.exists(_ == u), addUserInfo(_)).route)
    } ~
    pathPrefix("ws") {
      wsRoute
    } ~
    corsHandler(pathSingleSlash(getFromResource("html/build/index.html"))) ~
    corsHandler(getFromResourceDirectory("html/build")) ~
    corsHandler(new SwaggerDocService(system).routes)

  val bindingFuture = Http().bindAndHandle(handler = DebuggingDirectives.logRequestResult("log")(routes), interface = httpInterface, port = httpPort)

  println(s"Server online at $httpInterface:$httpPort/\nPress RETURN to stop...")
  StdIn.readLine() // let it run until user presses return
  bindingFuture
    .flatMap(_.unbind()) // trigger unbinding from the port
    .onComplete(_ => system.terminate()) // and shutdown when done
}
