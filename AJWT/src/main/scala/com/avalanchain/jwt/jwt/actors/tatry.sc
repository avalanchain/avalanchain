import java.security.KeyPair

import akka.typed._
import akka.typed.ScalaDSL._
import akka.typed.AskPattern._
import akka.util.Timeout
import com.avalanchain.jwt.basicChain.JwtAlgo.{ES512, HS512}
import com.avalanchain.jwt.basicChain._
import cats.implicits._
import io.circe._
import io.circe.syntax._
import io.circe.parser._
import io.circe.generic.JsonCodec
import io.circe.generic.auto._
import pdi.jwt.{Jwt, JwtAlgorithm, JwtBase64}
import pdi.jwt.exceptions.JwtLengthException
import com.avalanchain.jwt.basicChain.JwtAlgo.{ES512, HS512}

import scala.collection.mutable
import scala.concurrent.Future
import scala.concurrent.duration._
import scala.concurrent.Await
implicit val timeout = Timeout(2 seconds)

////// Chain Registry

object ChainRegistry {
  class Chain2(val chainDefToken: ChainDefToken, val keyPair: KeyPair,
               tokenStorage: FrameTokenStorage, currentState: Option[ChainState] = None) {
    if (chainDefToken.payload.isEmpty) throw new RuntimeException(s"Inconsistent ChainDefToken: '$chainDefToken'")
    val chainRef = ChainRef(chainDefToken)
    def status = ChainStatus.Active

    //    def pos: Position = state.pos
    //    def current: Option[FrameToken] = state.frame

    private var state = currentState.getOrElse(ChainState(chainDefToken, status, None, new FrameRef(chainRef.sig), -1, parse("{}").getOrElse(Json.Null)))

    def add(v: Json) = {
      val newPos = state.pos + 1
      val frameToken: TypedJwtToken[Frame] = chainDefToken.payload.get.algo match {
        case HS512 => TypedJwtToken[FSym](FSym(chainRef, newPos, state.lastRef, v), state.lastRef.sig).asInstanceOf[TypedJwtToken[Frame]]
        case ES512 => TypedJwtToken[FAsym](FAsym(chainRef, newPos, state.lastRef, v, keyPair.getPublic), keyPair.getPrivate).asInstanceOf[TypedJwtToken[Frame]]
      }
      tokenStorage.add(frameToken).map(_ => {
        state = ChainState(chainDefToken, status, Some(frameToken), FrameRef(frameToken), newPos, v)
      })
    }
  }


  sealed trait Command

  sealed trait AdminCommand extends Command
  object AdminCommand {
    final case class CreateChain(chainDefToken: ChainDefToken, replyTo: ActorRef[ChainCreationResult]) extends AdminCommand
    final case class PauseChain(chainDefToken: ChainDefToken, replyTo: ActorRef[ChainCreationResult]) extends AdminCommand
    final case class ResumeChain(chainDefToken: ChainDefToken, replyTo: ActorRef[ChainCreationResult]) extends AdminCommand
    final case class DeleteChain(chainDefToken: ChainDefToken, replyTo: ActorRef[ChainCreationResult]) extends AdminCommand
    final case class GetChainByRef(chainRef: ChainRef, replyTo: ActorRef[Option[ChainDefToken]]) extends AdminCommand
    final case class GetChainState(chainRef: ChainRef, replyTo: ActorRef[Option[ChainState]]) extends AdminCommand
    final case class GetChainStatus(chainRef: ChainRef, replyTo: ActorRef[Option[ChainStatus]]) extends AdminCommand
    final case class GetChains(replyTo: ActorRef[Vector[ChainDefToken]]) extends AdminCommand
  }

  val adminCommand: Behavior[AdminCommand] =
    ContextAware[AdminCommand] { ctx =>
      import AdminCommand._
      import ChainCreationResult._
      val chains = mutable.HashMap.empty[ChainRef, Chain2]

      Static {
        case CreateChain (chainDefToken, replyTo) =>
          val chainRef: ChainRef = chainDefToken
          val reply =
            if (chains contains chainRef) ChainAlreadyExists(chainDefToken)
            else {
              chains += (chainRef -> chainDefToken)
              ChainAlreadyExists(chainDefToken)
            }
          replyTo ! reply
        case GetChainByRef (chainRef, replyTo) =>
          val reply =
            if (chains(chainRef) ChainAlreadyExists(chainDefToken)
            else {
              chains += (chainRef -> chainDefToken)
              ChainAlreadyExists(chainDefToken)
            }
          replyTo ! reply

//        case GetSession(screenName, client) =>
//          sessions ::= client
//          val wrapper = ctx.spawnAdapter {
//            p: PostMessage => PostSessionMessage(screenName, p.message)
//          }
//          client ! SessionGranted(wrapper)
//        case PostSessionMessage(screenName, message) =>
//          val mp = MessagePosted(screenName, message)
//          sessions foreach (_ ! mp)
      }
    }

  sealed trait ChainCreationResult { val chainDefToken: ChainDefToken }
  object ChainCreationResult {
    sealed trait ChainCreationSuccess extends ChainCreationResult { val chainDefToken: ChainDefToken }
    final case class ChainCreated(chainDefToken: ChainDefToken) extends ChainCreationSuccess
    final case class ChainAlreadyExists(chainDefToken: ChainDefToken) extends ChainCreationSuccess

    sealed trait ChainCreationError extends ChainCreationResult
    final case class ChainNotDefined(chainDefToken: ChainDefToken) extends ChainCreationError
    final case class CannotWriteIntoDerivedChain(chainDefToken: ChainDefToken) extends ChainCreationError
    final case class InvalidChainDefToken(chainDefToken: ChainDefToken) extends ChainCreationError
  }

  sealed trait DataCommand extends Command
  object DataCommand {
    import DataReply._
    final case class Post(j: Json, replyTo: ActorRef[Either[ChainError, PostResult]]) extends DataCommand
    final case class GetPage(fromPos: Position, size: Short, replyTo: ActorRef[Either[ChainError, Vector[FrameToken]]]) extends DataCommand
    final case class GetBySig(sig: String, replyTo: ActorRef[Either[ChainError, Option[FrameToken]]]) extends DataCommand
  }

  sealed trait DataReply
  object DataReply {
    sealed trait PostResult extends DataReply
    object PostResult {
      final case class Acked(sig: String) extends PostResult
      final case class Rejected(j: Json, reason: String) extends PostResult
    }

    sealed trait ChainError extends DataReply
    object ChainError {
      final case object AccessDenied extends ChainError
      final case object ChainDeleted extends ChainError
    }
  }

  sealed trait ChainEvent
  object ChainEvent {
    trait ChainAdminEvent extends ChainEvent
    final case class ChainCreated(chainDefToken: ChainDefToken) extends ChainAdminEvent
    final case class ChainStateChanged(chainRef: ChainRef, chainState: ChainState) extends ChainAdminEvent

    trait ChainDataEvent extends ChainEvent
    final case class FT(frame: FrameToken) extends ChainDataEvent
  }
}

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