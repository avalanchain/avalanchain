package com.avalanchain.jwt.api

import java.time.OffsetDateTime
import javax.ws.rs.Path

import akka.actor.ActorSystem
import akka.http.scaladsl.model.StatusCodes
import akka.http.scaladsl.server.Directives
import akka.stream.Materializer
import akka.stream.scaladsl.Source
import akka.util.Timeout
import com.avalanchain.jwt.basicChain.Cmd
import com.avalanchain.jwt.jwt.actors.ChainNode
import com.avalanchain.jwt.jwt.demo.Demo.{ChatMsg, ChatMsgToken}
import com.avalanchain.jwt.utils.{CirceDecoders, CirceEncoders}
import de.heikoseeberger.akkahttpcirce.CirceSupport
import io.circe.generic.auto._
import io.circe.syntax._
import io.swagger.annotations.{ApiImplicitParam, _}

import scala.util.{Failure, Success}

case class ChatTweet(msg: String)

/**
  * Created by Yuriy on 22/11/2016.
  */
@Path("chat")
@Api(value = "/chat", produces = "application/json")
class ChatService(chainNode: ChainNode)(implicit actorSystem: ActorSystem, materializer: Materializer)
  extends Directives with CorsSupport with CirceSupport with CirceEncoders with CirceDecoders {
  import scala.concurrent.duration._

  implicit val timeout = Timeout(2 seconds)

  //val a = List[ChainDefToken]().asJson

  val route = pathPrefix("chat") {
    path("allMessages") {
      allMessages
    } ~
    path("allMessagesTokens") {
      allMessagesTokens
    } ~
    path("allMessagesJson") {
      allMessagesJson
    } ~
    path("newMessage") {
      newMessage
    }
  }

  @Path("newMessage")
  @ApiOperation(value = "Post a new message", notes = "", nickname = "newMessage", httpMethod = "POST")
  @ApiImplicitParams(Array(
    new ApiImplicitParam(name = "body", value = "\"Message\" to post", required = true,
      dataType = "com.avalanchain.jwt.api.ChatTweet", paramType = "body")
  ))
  @ApiResponses(Array(
    new ApiResponse(code = 201, message = "Message posted", response = classOf[ChatTweet]),
    new ApiResponse(code = 409, message = "Internal server error")
  ))
  def newMessage =
    post {
      entity(as[ChatTweet]) { tweet => {
          Source.single(Cmd(ChatMsg(chainNode.nodeName, tweet.msg, OffsetDateTime.now, chainNode.publicKey).asJson)).runWith(chainNode.chatNode.sink)
          complete(StatusCodes.Created, tweet)
        }
      }
    }

  @Path("allMessages")
  @ApiOperation(httpMethod = "GET", response = classOf[List[ChatMsg]], value = "Gets all chat messages")
  @ApiResponses(Array(
    new ApiResponse(code = 200, message = "All chat messages", response = classOf[List[ChatMsg]])
  ))
  def allMessages =
    get {
      onComplete(chainNode.chatNode.source.takeWithin(10 milliseconds).runFold(List.empty[ChatMsgToken])((acc, cm) => cm :: acc)(chainNode.materializer)) {
        case Success(msgs) => completeWith(instanceOf[List[ChatMsg]])(_(msgs.reverse.map(_.payload.get)))
        case Failure(e) => {
          println(s"Error: ${e.getMessage}")
          failWith(e)
        }
      }
    }

  @Path("allMessagesTokens")
  @ApiOperation(httpMethod = "GET", response = classOf[List[ChatMsg]], value = "Gets all chat messages as JWT tokens")
  @ApiResponses(Array(
    new ApiResponse(code = 200, message = "All chat messages as JWT tokens", response = classOf[List[ChatMsg]])
  ))
  def allMessagesTokens =
    get {
      onSuccess(chainNode.chatNode.source.takeWithin(10 milliseconds).runFold(List.empty[ChatMsgToken])((acc, cm) => cm :: acc)) { msgs =>
        completeWith(instanceOf[List[ChatMsgToken]])(_(msgs.reverse))
      }
    }

  @Path("allMessagesJson")
  @ApiOperation(httpMethod = "GET", response = classOf[List[ChatMsg]], value = "Gets all chat messages")
  @ApiResponses(Array(
    new ApiResponse(code = 200, message = "All chat messages", response = classOf[List[ChatMsg]])
  ))
  def allMessagesJson =
    get {
      onSuccess(chainNode.chatNode.sourceJson.takeWithin(10 milliseconds).runFold(List.empty[String])((acc, cm) => cm :: acc)) { msgs =>
        completeWith(instanceOf[List[String]])(_(msgs.reverse))
      }
    }
}
