package com.avalanchain.jwt

import java.io.File
import java.security.{KeyPair, PrivateKey, PublicKey}

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
import com.avalanchain.jwt.utils.Config

import scala.concurrent.ExecutionContext
import scala.io.StdIn
import com.github.swagger.akka._
import com.github.swagger.akka.model.Info
import kantan.csv.{RowDecoder, RowEncoder}
import kantan.csv.ops._
import spray.json.DefaultJsonProtocol
import io.swagger.annotations._
//import io.swagger.models.Path
import javax.ws.rs._


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
    val pk = key.toString
    val x = pk.substring(29, 93).trim
    val y = pk.substring(109).trim
    PubKey(x, y)
  }

  implicit def toPrivKeyDto(key: PrivateKey) = {
    val s = key.toString.substring(31).trim
    PrivKey(s)
  }

  implicit def toKeysDto(keys: KeyPair) = {
    Keys(toPrivKeyDto(keys.getPrivate), toPubKeyDto(keys.getPublic))
  }
}

import KeysDto._

trait UserInfoJsonSupport extends DefaultJsonProtocol with SprayJsonSupport {
  implicit val userInfoFormats = jsonFormat4(UserInfo)
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
class UsersService(getUsers: () => List[UserInfo], userExists: UserInfo => Boolean, addUser: UserInfo => Unit)(implicit executionContext: ExecutionContext)
  extends Directives with CorsSupport with UserInfoJsonSupport {

  import scala.concurrent.duration._

  implicit val timeout = Timeout(2.seconds)

  val route = pathPrefix("users") {
    getall ~ add
  }

  @ApiOperation(value = "Add User", notes = "", nickname = "addUser", httpMethod = "POST")
  @ApiImplicitParams(Array(
    new ApiImplicitParam(name = "body", value = "\"User\" to add", required = true,
      dataType = "com.deloitte.id.jwtexport.jwt.UserInfo", paramType = "body")
  ))
  @ApiResponses(Array(
    new ApiResponse(code = 201, message = "User added", response = classOf[UserInfo]),
    new ApiResponse(code = 409, message = "Internal server error")
  ))
  def add =
    post {
      entity(as[UserInfo]) { user =>
        val exists = userExists(user)
        if (exists) complete(StatusCodes.Conflict, None)
        else {

          val encodedUser = encodeUser(CurveContext.savedKeys().getPrivate, user)

          val post =
            scalaj.http.Http("https://e-money.setl.io/deloitteuser").
              postForm(Seq("data" -> encodedUser._2,
                "cardkey" -> "042a92b740aa59f4f91c2160a956cb786239e4d12840f6ca8ff0261e75e3f9ca72fcab1d06f0901238712db53258ea3f9b1b9ef9e8908d71528fd1a1227aea26bb")).asString

          println(s"SETL post result: ${post.body}")

          if (!post.body.contains("""{"Status": "Fail",""")) {
            addUser(user)
            complete(StatusCodes.Created, Some(user))
          } else complete(StatusCodes.BadRequest, None)
        }
      }
    }

  @ApiOperation(httpMethod = "GET", response = classOf[List[UserInfo]], value = "Returns the list of registered Users")
  @ApiResponses(Array(
    new ApiResponse(code = 200, message = "Users registered", response = classOf[UserInfo])
  ))
  def getall =
    get {
      completeWith(instanceOf[List[UserInfo]])(_(getUsers()))
    }
}


@Path("admin")
@Api(value = "/admin", produces = "application/json")
class AdminService()
  extends Directives with CorsSupport with UserInfoJsonSupport {

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

object Main extends App with Config with CorsSupport with UserInfoJsonSupport {
  private implicit val system = ActorSystem()
  protected implicit val executor: ExecutionContext = system.dispatcher
  protected val log: LoggingAdapter = Logging(system, getClass)
  protected implicit val materializer: ActorMaterializer = ActorMaterializer()

  import java.nio.file.{Paths, Files}
  val rawDataFile = Paths.get(sys.env("HOME") + "/Documents/users.csv")
  if (!Files.exists(rawDataFile)) {
    Files.copy(getClass.getResourceAsStream("/public/users.csv"), rawDataFile)
  }

  def userInfos() = {
    val rawData: java.net.URL = rawDataFile.toUri.toURL

    implicit val userDecoder: RowDecoder[UserInfo] = RowDecoder.decoder(0, 1, 2, 3)(UserInfo.apply)
    val reader = rawData.asCsvReader[UserInfo](',', true)

    reader.map(_.get).toList
  }

  def addUserInfo(user: UserInfo): Unit = {
    val infos = userInfos()
    implicit val userEncoder: RowEncoder[UserInfo] = RowEncoder.caseEncoder(0, 1, 2, 3)(UserInfo.unapply)
    new File(rawDataFile.toAbsolutePath().toString() + "2").delete()
    val out = new java.io.File(rawDataFile.toAbsolutePath().toString())
    val writer = out.asCsvWriter[UserInfo](',', List("first_name", "last_name", "email", "phone"))
    writer.write(user :: infos).close()
  }

  class SwaggerDocService(system: ActorSystem) extends SwaggerHttpService with HasActorSystem {
    override implicit val actorSystem: ActorSystem = system
    override implicit val materializer: ActorMaterializer = ActorMaterializer()
    override val apiTypes = Seq(ru.typeOf[UsersService], ru.typeOf[AdminService])
    override val host = s"$httpInterface:$httpPort" //the url of your api, not swagger's json endpoint
    override val basePath = "/v1"    //the basePath for the API you are exposing
    override val apiDocsPath = "api-docs" //where you want the swagger-json endpoint exposed
    override val info = Info(version = "1.0") //provides license and other description details
  }

  val routes =
    pathPrefix("swagger") {
      getFromResourceDirectory("swagger") ~ pathSingleSlash(get(redirect("index.html", StatusCodes.PermanentRedirect)))
    } ~
    pathPrefix("v1") {
      path("")(getFromResource("public/index.html")) ~
      corsHandler(new UsersService(userInfos, u => userInfos.exists(_ == u), addUserInfo(_)).route) ~
      corsHandler(new AdminService().route)
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
