package com.avalanchain.jwt.jwt.chat

import java.security.KeyPair
import java.time.OffsetDateTime
import java.util.UUID

import scala.concurrent.duration._
import akka.actor.{Actor, ActorLogging, ActorRef, Props}
import akka.cluster.Cluster
import akka.cluster.ddata._
import com.avalanchain.jwt.KeysDto.PubKey
import com.avalanchain.jwt.basicChain.{Id, Position}
import com.avalanchain.jwt.jwt.chat.ChatMsg.ChatMsgToken
import com.avalanchain.jwt.jwt.chat.ChatMsg
import com.avalanchain.jwt.utils._


/**
  * Created by yuriyhabarov on 08/02/2016.
  */

object ChatClient extends CirceCodecs {
  import akka.cluster.ddata.Replicator._

  def props(keyPair: () => KeyPair, nodeName: String): Props = Props(new ChatClient(keyPair, nodeName))

  final case class ChatParticipant(id: Id, name: String)

  final case class ChatChannel (id: Id, name: String)
//  final case class ChatChannelState (channel: ChatChannel, val participants: Set[ChatParticipant])

  sealed trait ChatAdminMsg //{ val channelId: Id }
  object ChatAdminMsg {
    final case class CreateParticipant(name: String) extends ChatAdminMsg
    final case class CreateChatChannel(name: String, participants: Set[ChatParticipant]) extends ChatAdminMsg
    final case class RemoveChatChannel(id: Id) extends ChatAdminMsg
    final case class AddParticipants(groupChannelId: Id, cps: Set[ChatParticipant]) extends ChatAdminMsg
    final case class RemoveParticipants(groupChannelId: Id, cps: Set[ChatParticipant]) extends ChatAdminMsg
  }

  final case class SendMsg(channel: ChatChannel, participant: ChatParticipant, msg: String)
  final case class GetMessages(channel: ChatChannel)

  final case class Participants(participants: Set[ChatParticipant])
  final case class ChatChannels(channels: Map[ChatChannel, Set[ChatParticipant]])
  final case class ChatMessages(channel: ChatChannel, msgs: Set[ChatMsg])

  sealed trait ChatQuery
  final case object GetParticipantsAll extends ChatQuery

  final case object GetChatChannelsAll extends ChatQuery
  final case class GroupChannelParticipants(groupChannelId: Id) extends ChatQuery

  final case class GroupChannelMessagesAll(groupChannelId: Id) extends ChatQuery
  final case class GroupChannelMessagesPage(groupChannelId: Id, from: Position, pageSize: Int) extends ChatQuery

  //#read-write-majority
  private val timeout = 3.seconds
  private val readMajority = ReadMajority(timeout)
  private val writeMajority = WriteMajority(timeout)
  //#read-write-majority

}

class ChatClient(keyPair: () => KeyPair, nodeName: String) extends Actor with ActorLogging {
  import ChatClient._
  import akka.cluster.ddata.Replicator._
  import com.avalanchain.jwt.jwt.chat.ChatClient.ChatAdminMsg._

  private final case class GetMessageCtx(channel: ChatChannel, replyTo: ActorRef)

  val replicator = DistributedData(context.system).replicator
  implicit val cluster = Cluster(context.system)

  val ChatParticipantsKey = ORSetKey[ChatParticipant]("ChatParticipants")
  val ChatChannelsKey = ORMultiMapKey[ChatChannel, ChatParticipant]("ChatChannels")
  //def ChatChannelsMessagesKey(id: Id) = GSetKey[ChatMsgToken](s"ChatMessages:$id")
  def ChatChannelsMessagesKey(id: Id) = GSetKey[ChatMsg](s"ChatMessages:$id")

  def newId() = randomId

  def receive = getParticipantsAll
    .orElse[Any, Unit](createParticipant)
    .orElse[Any, Unit](getChatChannelsAll)
    .orElse[Any, Unit](chatChannelAdmin)
    .orElse[Any, Unit](createMessage)
    .orElse[Any, Unit](getMessages)
    .orElse[Any, Unit](receiveOther)

  def createMessage: Receive = {
    //case msg @ ChatMsg(channelId: Id, senderId: Id, nodeName: String, message: String, dt: OffsetDateTime, pub: PubKey) =>
    case cmd @ SendMsg(channel: ChatChannel, participant: ChatParticipant, msg: String) =>
      val chatMsg = ChatMsg(channel.id, participant.id, nodeName, msg, OffsetDateTime.now, keyPair().getPublic)
      val channelKey = ChatChannelsMessagesKey(channel.id)
      log.info("Message sent: {}", msg)
      replicator ! Update(channelKey, GSet.empty[ChatMsg], writeMajority, Some(msg)) (_ + chatMsg)
  }

  def getMessages: Receive = {
    case GetMessages(channel: ChatChannel) =>
      replicator ! Get(ChatChannelsMessagesKey(channel.id), readMajority, Some(GetMessageCtx(channel, sender())))

    case g @ GetSuccess(key: GSetKey[ChatMsg], Some(GetMessageCtx(channel, replyTo))) =>
      val data = g.get(key)
      replyTo ! ChatMessages(channel, data.elements)

    case NotFound(key: GSetKey[ChatMsg], Some(GetMessageCtx(channel, replyTo))) =>
      replyTo ! ChatMessages(channel, Set.empty)

    case GetFailure(key: GSetKey[ChatMsg], Some(GetMessageCtx(channel, replyTo))) =>
      // ReadMajority failure, try again with local read
      replicator ! Get(key, ReadLocal, Some(GetMessageCtx(channel, replyTo)))
  }

  def getParticipantsAll: Receive = {
    case GetParticipantsAll =>
      replicator ! Get(ChatParticipantsKey, readMajority, Some(sender()))

    case g @ GetSuccess(ChatParticipantsKey, Some(replyTo: ActorRef)) =>
      val data = g.get(ChatParticipantsKey)
      replyTo ! Participants(data.elements)

    case NotFound(ChatParticipantsKey, Some(replyTo: ActorRef)) =>
      replyTo ! Participants(Set.empty)

    case GetFailure(ChatParticipantsKey, Some(replyTo: ActorRef)) =>
      // ReadMajority failure, try again with local read
      replicator ! Get(ChatParticipantsKey, ReadLocal, Some(replyTo))
  }

  def createParticipant: Receive = {
    case cmd @ CreateParticipant(name: String) =>
      val participant = ChatParticipant(newId, name)
      log.info("Adding Chat Participant: {}", participant)
      replicator ! Update(ChatParticipantsKey, ORSet.empty[ChatParticipant], writeMajority, Some(cmd)) (_ + participant)
      sender() ! participant // TODO: Add awaiting for the update to finish
  }

  def getChatChannelsAll: Receive = {
    case GetChatChannelsAll =>
      replicator ! Get(ChatChannelsKey, readMajority, Some(sender()))

    case g @ GetSuccess(ChatChannelsKey, Some(replyTo: ActorRef)) =>
      val data = g.get(ChatChannelsKey)
      replyTo ! ChatChannels(data.entries)

    case NotFound(ChatChannelsKey, Some(replyTo: ActorRef)) =>
      replyTo ! ChatChannels(Map.empty)

    case GetFailure(ChatChannelsKey, Some(replyTo: ActorRef)) =>
      // ReadMajority failure, try again with local read
      replicator ! Get(ChatChannelsKey, ReadLocal, Some(replyTo))
  }

  def chatChannelAdmin: Receive = {
    case cmd @ CreateChatChannel(name: String, participants: Set[ChatParticipant]) =>
      val channel = ChatChannel(newId, name)
      log.info("Adding Chat Channel: {}", cmd)
      replicator ! Update(ChatChannelsKey, ORMultiMap.empty[ChatChannel, ChatParticipant], writeMajority, Some(cmd)) (_. + (channel -> participants))
      sender() ! channel // TODO: Add awaiting for the update to finish

    case cmd @ RemoveChatChannel(id: Id) =>
      log.info("Removing Chat Channel: {}", id)
      replicator ! Update(ChatChannelsKey, ORMultiMap.empty[ChatChannel, ChatParticipant], writeMajority, Some(cmd)) (orm => {
        val keysToRemove = orm.entries.filterKeys(_.id == id).keys
        var ret = orm
        keysToRemove.foreach(k => ret = ret - k)
        ret
      })

    case cmd @ AddParticipants(groupChannelId: Id, cps: Set[ChatParticipant]) =>
      log.info("Adding Participants: {}", cmd)
      replicator ! Update(ChatChannelsKey, ORMultiMap.empty[ChatChannel, ChatParticipant], writeMajority, Some(cmd)) (orm => {
        val groupChannels = orm.entries.filterKeys(_.id == groupChannelId).keys
        var ret = orm
        groupChannels.foreach(gc => cps.foreach(cp => ret = ret.addBinding(gc, cp)))
        ret
      })

    case cmd @ RemoveParticipants(groupChannelId: Id, cps: Set[ChatParticipant]) =>
      log.info("Adding Participants: {}", cmd)
      replicator ! Update(ChatChannelsKey, ORMultiMap.empty[ChatChannel, ChatParticipant], writeMajority, Some(cmd)) (orm => {
        val groupChannels = orm.entries.filterKeys(_.id == groupChannelId).keys
        var ret = orm
        groupChannels.foreach(gc => cps.foreach(cp => ret = ret.removeBinding(gc, cp)))
        ret
      })
  }

  ////////////////

  def receiveOther: Receive = {
    case _: UpdateSuccess[_] | _: UpdateTimeout[_] =>
    // UpdateTimeout, will eventually be replicated
    case e: UpdateFailure[_]                       => throw new IllegalStateException("Unexpected failure: " + e)
    case m => log.info("Unhandled message: {}", m)
  }

}