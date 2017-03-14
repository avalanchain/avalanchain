import java.security.KeyPair
import java.util.concurrent.ConcurrentHashMap

import akka.typed.AskPattern._
import akka.typed.ScalaDSL._
import akka.typed._
import akka.util.Timeout
import cats.implicits._
import io.circe._
import io.circe.generic.auto._
import io.circe.parser._

import scala.concurrent.{Await, Future}
import scala.concurrent.duration._


////////////////////////////////////// ChatRoom

object ChatRoom {
  sealed trait Command
  final case class GetSession(screenName: String, replyTo: ActorRef[SessionEvent]) extends Command

  sealed trait SessionEvent
  final case class SessionGranted(handle: ActorRef[PostMessage]) extends SessionEvent
  final case class SessionDenied(reason: String) extends SessionEvent
  final case class MessagePosted(screenName: String, message: String) extends SessionEvent

  final case class PostMessage(message: String)


  private final case class PostSessionMessage(screenName: String, message: String) extends Command

  val chatRoom: Behavior[GetSession] =
    ContextAware[Command] { ctx =>
      var sessions = List.empty[ActorRef[SessionEvent]]

      Static {
        case GetSession(screenName, client) =>
          sessions ::= client
          val wrapper = ctx.spawnAdapter {
            p: PostMessage => PostSessionMessage(screenName, p.message)
          }
          client ! SessionGranted(wrapper)
        case PostSessionMessage(screenName, message) =>
          val mp = MessagePosted(screenName, message)
          sessions foreach (_ ! mp)
      }
    }.narrow // only expose GetSession to the outside

  def participant(name: String): Behavior[SessionEvent] =
    Total {
      case SessionDenied(reason) =>
        println(s"cannot start chat room session: $reason")
        Stopped
      case SessionGranted(handle) =>
        handle ! PostMessage("Hello World!")
        Same
      case MessagePosted(screenName, message) =>
        println(s"'$name': message has been posted by '$screenName': $message")
        Stopped
        //Same
    }
}

import ChatRoom._

val main: Behavior[akka.NotUsed] =
  Full {
    case Sig(ctx, PreStart) =>
      val chatRoomRef = ctx.spawn(chatRoom, "chatroom")
      def addParticipant(name: String) = {
        val partRef = ctx.spawn(participant(name), name)
        ctx.watch(partRef)
        chatRoomRef ! ChatRoom.GetSession(name, partRef)
      }
      addParticipant("User 1")
      addParticipant("User 2")
      addParticipant("User 3")
      addParticipant("User 4")
      addParticipant("User 5")
      addParticipant("User 6")
      Same
    case Sig(_, Terminated(ref)) =>
      Stopped
  }

val system2 = ActorSystem("ChatRoomDemo", main)
Await.result(system2.whenTerminated, 100.second)



//////////// Hello World

object HelloWorld {
  final case class Greet(whom: String, replyTo: ActorRef[Greeted])
  final case class Greeted(whom: String)

  val greeter = Static[Greet] { msg =>
    println(s"Hello ${msg.whom}!")
    msg.replyTo ! Greeted(msg.whom)
  }
}

import HelloWorld._
// using global pool since we want to run tasks after system.terminate
import scala.concurrent.ExecutionContext.Implicits.global

val system: ActorSystem[Greet] = ActorSystem("hello", greeter)
implicit def scheduler = system.scheduler

val future: Future[Greeted] = system ? (Greet("world", _))

for {
  greeting <- future.recover { case ex => ex.getMessage }
  done <- { println(s"result: $greeting"); system.terminate() }
} println("system terminated")