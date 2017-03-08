import java.util.UUID

import akka.actor.{ActorSystem, Props}
import akka.stream.{ActorMaterializer, Materializer}
import com.avalanchain.jwt.jwt.CurveContext
import com.avalanchain.jwt.jwt.chat.ChatClient
import com.avalanchain.jwt.utils.AkkaConfigs
import com.typesafe.config.ConfigFactory
import akka.pattern.{ask, pipe}
import com.avalanchain.jwt.jwt.chat.ChatClient.ChatAdminMsg.{CreateChatChannel, CreateParticipant}
import com.avalanchain.jwt.jwt.chat.ChatClient.{ChatChannel, ChatChannels, ChatMessages, ChatParticipant, GetChatChannelsAll, GetMessages, GetParticipantsAll, Participants, SendMsg}

import scala.concurrent.duration._
import akka.util.Timeout

import scala.concurrent.Await

implicit val timeout = Timeout(5 seconds)

def start(port: Int, user: String) = {
  //implicit val system: ActorSystem = ActorSystem("avalanchain", ConfigFactory.parseString(AkkaConfigs.PersInmem2))
  implicit val system: ActorSystem = ActorSystem("avalanchain", ConfigFactory.parseString(s"akka.remote.netty.tcp.host = 127.0.0.1")
    .withFallback(ConfigFactory.parseString(s"akka.remote.netty.tcp.port = $port"))
    .withFallback(ConfigFactory.load("node.conf")))
  implicit val mat: Materializer = ActorMaterializer()


  val chatClient = system.actorOf(Props(new ChatClient(() => CurveContext.savedKeys(), user + " Node")), "chatClient")

  chatClient ! GetParticipantsAll

  chatClient ! CreateParticipant(user)

  chatClient
}

val chatClient1 = start(2551, "John")

val chatClient2 = start(2552, "Smith")

val participants = Await.result(chatClient1 ? GetParticipantsAll, 5 seconds).asInstanceOf[Participants]

chatClient1 ! CreateChatChannel("priv room", participants.participants)
chatClient2 ! CreateChatChannel("pub room", participants.participants)

//chatClient2 ? GetChatChannelsAll

val chatChannels = Await.result(chatClient1 ? GetChatChannelsAll, 5 seconds).asInstanceOf[ChatChannels]
val chatChannel = chatChannels.channels.head

chatClient1 ! SendMsg(chatChannel._1, participants.participants.head, "Hello")

chatClient2 ! SendMsg(chatChannel._1, participants.participants.head, "Yes")

for (i <- 1 to 5000) {
  chatClient1 ! SendMsg(chatChannel._1, participants.participants.head, "Hello-" + i)
  chatClient2 ! SendMsg(chatChannel._1, participants.participants.head, "Yes-" + i)
}

val messages = Await.result(chatClient1 ? GetMessages(chatChannel._1), 5 seconds).asInstanceOf[ChatMessages]

messages.msgs.size