package com.avalanchain.jwt.api

import java.time.OffsetDateTime
import javax.ws.rs.Path

import akka.actor.ActorSystem
import akka.http.scaladsl.model.{HttpRequest, StatusCodes}
import akka.http.scaladsl.server.Directives
import akka.stream.Materializer
import akka.stream.scaladsl.{Sink, Source}
import akka.util.Timeout
import akka.pattern.{ask, pipe}

import scala.concurrent.duration._
import com.avalanchain.jwt.basicChain.{Cmd, FrameToken, Id}
import com.avalanchain.jwt.jwt.actors.ChainNode
import com.avalanchain.jwt.jwt.chat.ChatClient.ChatAdminMsg.{CreateChatChannel, CreateParticipant, RemoveChatChannel}
import com.avalanchain.jwt.jwt.chat.ChatClient.{ChatChannel, ChatChannels, ChatParticipant, GetChatChannelsAll, GetParticipantsAll, Participants}
import com.avalanchain.jwt.jwt.chat.ChatMsg
import com.avalanchain.jwt.utils.CirceCodecs
import de.heikoseeberger.akkahttpcirce.CirceSupport
import io.circe.Json
import io.circe.generic.auto._
import io.circe.syntax._
import io.circe.parser._
import io.swagger.annotations.{ApiImplicitParam, _}

import scala.concurrent.Await
import scala.util.{Failure, Success}

case class ChatTweet(msg: String)

/**
  * Created by Yuriy on 22/11/2016.
  */
@Path("chat")
@Api(value = "/Chat", produces = "application/json")
class ChatService(chainNode: ChainNode)(implicit actorSystem: ActorSystem, materializer: Materializer)
  extends Directives with CorsSupport with CirceSupport with CirceCodecs {
  import scala.concurrent.duration._

  implicit val timeout = Timeout(2 seconds)

  //val a = List[ChainDefToken]().asJson

  val route = pathPrefix("chat") {
    path("allParticipants") {
      allParticipants
    } ~
    path("newParticipant") {
      newParticipant
    } ~
    path("allChannels") {
      allChannels
    } ~
    path("allChannelParticipants") {
      allChannelParticipants
    } ~
    path("newChannel") {
      newChannel
    } ~
    path("removeChannel") {
      removeChannel
    } ~
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

  @Path("allParticipants")
  @ApiOperation(httpMethod = "GET", response = classOf[Set[ChatParticipant]], value = "Gets all registered Participants")
  @ApiResponses(Array(
    new ApiResponse(code = 200, message = "All registered Participants", response = classOf[Set[ChatParticipant]])
  ))
  def allParticipants =
    get {
      onSuccess((chainNode.chatClient ? GetParticipantsAll).mapTo[Participants]) { participants =>
        completeWith(instanceOf[Set[ChatParticipant]])(_(participants.participants))
      }
    }

  @Path("newParticipant")
  @ApiOperation(value = "Create a new Participant", notes = "", nickname = "newParticipant", httpMethod = "POST")
  @ApiImplicitParams(Array(
    new ApiImplicitParam(name = "body", value = "Participant's name", required = true,
      dataType = "String", paramType = "body")
  ))
  @ApiResponses(Array(
    new ApiResponse(code = 201, message = "Participant created"),
    new ApiResponse(code = 409, message = "Internal server error")
  ))
  def newParticipant =
    post {
      entity(as[HttpRequest]) { request => {
        val user = request.entity.dataBytes.map(_.utf8String).runWith(Sink.head)
        chainNode.chatClient ! CreateParticipant(Await.result(user, 1 second))
        complete(StatusCodes.Created)
      }
      }
    }


  @Path("allChannel")
  @ApiOperation(httpMethod = "GET", response = classOf[Set[ChatChannel]], value = "Gets all registered Participants")
  @ApiResponses(Array(
    new ApiResponse(code = 200, message = "All registered Channels", response = classOf[Set[ChatChannel]])
  ))
  def allChannels =
    get {
      onSuccess((chainNode.chatClient ? GetChatChannelsAll).mapTo[ChatChannels]) { channels =>
        completeWith(instanceOf[Set[ChatChannel]])(_(channels.channels.keys.toSet))
      }
    }

  @Path("allChannelParticipants")
  @ApiOperation(httpMethod = "GET", response = classOf[Map[Id, Set[ChatParticipant]]], value = "Gets all registered Participants")
  @ApiResponses(Array(
    new ApiResponse(code = 200, message = "All registered Channels", response = classOf[Map[Id, Set[ChatParticipant]]])
  ))
  def allChannelParticipants =
    get {
      onSuccess((chainNode.chatClient ? GetChatChannelsAll).mapTo[ChatChannels]) { channels =>
        completeWith(instanceOf[Map[Id, Set[ChatParticipant]]])(_(channels.channels.map(e => (e._1.id, e._2))))
      }
    }

  @Path("newChannel")
  @ApiOperation(value = "Create a new Chat Channel", notes = "", nickname = "newChannel", httpMethod = "POST")
  @ApiImplicitParams(Array(
    new ApiImplicitParam(name = "body", value = "Create Chat Channel request", required = true,
      dataType = "CreateChatChannel", paramType = "body")
  ))
  @ApiResponses(Array(
    new ApiResponse(code = 201, message = "Channel created"),
    new ApiResponse(code = 409, message = "Internal server error")
  ))
  def newChannel =
    post {
      entity(as[CreateChatChannel]) { ccc => {
        chainNode.chatClient ! ccc
        complete(StatusCodes.Created)
      }
      }
    }

  @Path("removeChannel")
  @ApiOperation(value = "Remove a Chat Channel", notes = "", nickname = "newChannel", httpMethod = "DELETE")
  @ApiImplicitParams(Array(
    new ApiImplicitParam(name = "body", value = "Channel Id", required = true,
      dataType = "String", paramType = "body")
  ))
  @ApiResponses(Array(
    new ApiResponse(code = 200, message = "Channel removed"),
    new ApiResponse(code = 409, message = "Internal server error")
  ))
  def removeChannel =
    delete {
      entity(as[HttpRequest]) { request => {
        val user = request.entity.dataBytes.map(_.utf8String).runWith(Sink.head)
        chainNode.chatClient ! RemoveChatChannel(Await.result(user, 1 second))
        complete(StatusCodes.OK)
      }
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
          Source.single(Cmd(ChatMsg(ChatMsg.ALL, ChatMsg.SYSBOT, chainNode.nodeName, tweet.msg, OffsetDateTime.now, chainNode.publicKey).asJson)).runWith(chainNode.chatNode.sink)
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
      onSuccess(chainNode.chatNode.source.takeWithin(10 milliseconds).runFold(List.empty[ChatMsg])((acc, cm) => cm :: acc)) { msgs =>
        completeWith(instanceOf[List[ChatMsg]])(_(msgs.reverse))
      }
    }

  @Path("allMessagesTokens")
  @ApiOperation(httpMethod = "GET", response = classOf[List[FrameToken]], value = "Gets all chat messages as JWT tokens")
  @ApiResponses(Array(
    new ApiResponse(code = 200, message = "All chat messages as JWT tokens", response = classOf[List[FrameToken]])
  ))
  def allMessagesTokens =
    get {
      onSuccess(chainNode.chatNode.sourceToken.takeWithin(10 milliseconds).runFold(List.empty[FrameToken])((acc, cm) => cm :: acc)) { msgs =>
        completeWith(instanceOf[List[FrameToken]])(_(msgs.reverse))
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
        completeWith(instanceOf[List[Json]])(_(msgs.reverse.map(parse(_).right.toOption.getOrElse(Json.Null))))
      }
    }
}
