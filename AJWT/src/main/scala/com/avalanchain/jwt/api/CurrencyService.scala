package com.avalanchain.jwt.api

import java.time.OffsetDateTime
import java.util.UUID
import javax.ws.rs.Path

import akka.actor.ActorSystem
import akka.http.scaladsl.model.StatusCodes
import akka.http.scaladsl.server.Directives
import akka.stream.Materializer
import akka.stream.scaladsl.Source
import akka.util.Timeout
import com.avalanchain.jwt.basicChain.{Cmd, FrameToken}
import com.avalanchain.jwt.jwt.CurveContext
import com.avalanchain.jwt.jwt.actors.ChainNode
import com.avalanchain.jwt.jwt.demo.Demo.{ChatMsg, ChatMsgToken}
import com.avalanchain.jwt.jwt.demo.account.AccountCommand.Add
import com.avalanchain.jwt.jwt.demo.account.{AccountCommand, AccountEvent, AccountStates}
import com.avalanchain.jwt.utils.CirceCodecs
import de.heikoseeberger.akkahttpcirce.CirceSupport
import io.circe.Json
import io.circe.generic.auto._
import io.circe.syntax._
import io.circe.parser._
import io.swagger.annotations.{ApiImplicitParam, _}

import scala.util.{Failure, Success}


/**
  * Created by Yuriy on 22/11/2016.
  */
@Path("currency")
@Api(value = "/currency", produces = "application/json")
class CurrencyService(chainNode: ChainNode)(implicit actorSystem: ActorSystem, materializer: Materializer)
  extends Directives with CorsSupport with CirceSupport with CirceCodecs {
  import scala.concurrent.duration._

  implicit val timeout = Timeout(2 seconds)

  //val a = List[ChainDefToken]().asJson

  val route = pathPrefix("currency") {
    path("accounts") {
      accounts
    } ~
    path("accountEvents") {
      accountEvents
    } ~
    path("accountEventsTokens") {
      accountEventsTokens
    } ~
    path("accountEventsJson") {
      accountEventsJson
    } ~
//    path("newAccount") {
//      newAccount
//    } ~
    path("newAccount1000") {
      newAccount1000
    }
  }

//  @Path("newAccount")
//  @ApiOperation(value = "Create a new Account", notes = "", nickname = "newAccount", httpMethod = "POST")
//  @ApiImplicitParams(Array(
//    new ApiImplicitParam(name = "body", value = "\"Account\" to create", required = true,
//      dataType = "com.avalanchain.jwt.api.ChatTweet", paramType = "body")
//  ))
//  @ApiResponses(Array(
//    new ApiResponse(code = 201, message = "AccountCreated", response = classOf[ChatTweet]),
//    new ApiResponse(code = 409, message = "Internal server error")
//  ))
//  def newAccount =
//    post {
//      entity(as[ChatTweet]) { tweet => {
//          Source.single(Cmd(ChatMsg(chainNode.nodeName, tweet.msg, OffsetDateTime.now, chainNode.publicKey).asJson)).runWith(chainNode.chatNode.sink)
//          complete(StatusCodes.Created, tweet)
//        }
//      }
//    }

  @Path("newAccount1000")
  @ApiOperation(value = "Create a new Account with 1000 balance", notes = "", nickname = "newAccount1000", httpMethod = "POST")
  @ApiResponses(Array(
    //new ApiResponse(code = 201, message = "AccountCreated", response = classOf[Add]),
    new ApiResponse(code = 409, message = "Internal server error")
  ))
  def newAccount1000 =
    post {
      val accountCommand = Add(UUID.randomUUID(), 1000, OffsetDateTime.now().plusYears(1), CurveContext.newKeys().getPublic, chainNode.publicKey)
      Source.single(accountCommand).runWith(chainNode.currencyNode.accountSink)
      complete(StatusCodes.Created, accountCommand)
    }

  @Path("accounts")
  @ApiOperation(httpMethod = "GET", response = classOf[List[AccountStates]], value = "Gets all chat messages")
  @ApiResponses(Array(
    new ApiResponse(code = 200, message = "All accounts", response = classOf[List[ChatMsg]])
  ))
  def accounts =
    get {
      onSuccess(chainNode.currencyNode.accountsSource.takeWithin(10 milliseconds).runFold(List.empty[AccountStates])((acc, cm) => cm :: acc)) { msgs =>
        completeWith(instanceOf[List[AccountStates]])(_(msgs.reverse))
      }
    }

//  @Path("accountEvents")
//  @ApiOperation(httpMethod = "GET", response = classOf[List[AccountEvent]], value = "Gets all accounts events")
//  @ApiResponses(Array(
//    new ApiResponse(code = 200, message = "All accounts", response = classOf[List[AccountEvent]])
//  ))
//  def accountEvents =
//    get {
//      onSuccess(chainNode.currencyNode.accountSource.takeWithin(10 milliseconds).runFold(List.empty[AccountEvent])((acc, cm) => cm :: acc)) { msgs =>
//        completeWith(instanceOf[List[AccountEvent]])(_(msgs.reverse))
//      }
//    }

  @Path("accountEvents")
  @ApiOperation(httpMethod = "GET", response = classOf[List[AccountCommand]], value = "Gets all account related commands")
  @ApiResponses(Array(
    new ApiResponse(code = 200, message = "All accounts", response = classOf[List[AccountCommand]])
  ))
  def accountEvents =
    get {
      onSuccess(chainNode.currencyNode.accountSource.takeWithin(10 milliseconds).runFold(List.empty[AccountCommand])((acc, cm) => cm :: acc)) { msgs =>
        completeWith(instanceOf[List[AccountCommand]])(_(msgs.reverse))
      }
    }

  @Path("accountEventsTokens")
  @ApiOperation(httpMethod = "GET", response = classOf[List[FrameToken]], value = "Gets all accounts as JWT tokens")
  @ApiResponses(Array(
    new ApiResponse(code = 200, message = "All accounts as JWT tokens", response = classOf[List[FrameToken]])
  ))
  def accountEventsTokens =
    get {
      onSuccess(chainNode.currencyNode.accountSourceToken.takeWithin(10 milliseconds).runFold(List.empty[FrameToken])((acc, cm) => cm :: acc)) { msgs =>
        completeWith(instanceOf[List[FrameToken]])(_(msgs.reverse))
      }
    }

  @Path("accountEventsJson")
  @ApiOperation(httpMethod = "GET", response = classOf[List[ChatMsg]], value = "Gets all accounts as Json")
  @ApiResponses(Array(
    new ApiResponse(code = 200, message = "All accounts as Json", response = classOf[List[ChatMsg]])
  ))
  def accountEventsJson =
    get {
      onSuccess(chainNode.currencyNode.accountSourceJson.takeWithin(10 milliseconds).runFold(List.empty[String])((acc, cm) => cm :: acc)) { msgs =>
        completeWith(instanceOf[List[Json]])(_(msgs.reverse.map(parse(_).right.toOption.getOrElse(Json.Null))))
      }
    }
}
