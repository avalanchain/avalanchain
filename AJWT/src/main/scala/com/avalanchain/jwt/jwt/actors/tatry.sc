import java.security.KeyPair
import java.util.concurrent.ConcurrentHashMap

import akka.NotUsed
import akka.stream.scaladsl.Source
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
import scala.util.{Success, Try}
import scala.collection.immutable.Map
import scala.collection._
import scala.collection.convert.decorateAsScala._
import java.util.concurrent.ConcurrentHashMap

import com.avalanchain.jwt.KeysDto.PubKey
import com.avalanchain.jwt.basicChain.ChainStatus._
implicit val timeout = Timeout(2 seconds)

////// Chain Registry

trait FrameTokenStorage2 {
  def add(frameToken: FrameToken): Either[String, FrameRef]
  def get(frameRef: FrameRef): Option[FrameToken]
  //def get(chainRef: ChainRef, pos: Position): Option[FrameToken]
  def getPage(fromPos: Position, size: Short): Vector[FrameToken]
}

class MapFrameTokenStorage extends FrameTokenStorage2 {
  private var tokens = new ConcurrentHashMap[FrameRef, FrameToken].asScala
  private val buffer = new mutable.ArrayBuffer[FrameToken](1024)
  def frameTokens = tokens

  override def add(frameToken: FrameToken): Either[String, FrameRef] = {
    tokens += (FrameRef(frameToken) -> frameToken)
    buffer += frameToken
    Right(FrameRef(frameToken))
  }

  override def get(frameRef: FrameRef): Option[FrameToken] = tokens.get(frameRef)

  def getPage(fromPos: Position, size: Short): Vector[FrameToken] = {
    if (fromPos > buffer.size || size < 0) Vector.empty[FrameToken]
    else {
      val (s, l) =
        if (fromPos >= 0) {
          val start = fromPos
          val len = Math.max(Math.min(fromPos + size, buffer.size) - fromPos, 0)
          (start, len)
        }
        else {
          val start = 0
          val adjustedSize = fromPos + size // as fromPos is negative adjustedSize will be always positive
          val len = Math.min(adjustedSize, buffer.size)
          (start, len)
        }
      val array = new Array[FrameToken](l)
      buffer.copyToArray(array, s, l)
      array.toVector
    }
  }
}

class Chain2(val keyPair: KeyPair, val chainDefToken: ChainDefToken, initialState: Option[ChainState] = None, val tokenStorage: FrameTokenStorage2 = new MapFrameTokenStorage()) {
  if (chainDefToken.payload.isEmpty) throw new RuntimeException(s"Inconsistent ChainDefToken: '$chainDefToken'")
  val chainRef = ChainRef(chainDefToken)
  def status = ChainStatus.Active

  private var currentState = initialState.getOrElse(ChainState(chainDefToken, status, None, new FrameRef(chainRef.sig), -1, parse("{}").getOrElse(Json.Null)))
  def state = currentState

  def add(v: Json) = {
    val newPos = currentState.pos + 1
    val frameToken: TypedJwtToken[Frame] = chainDefToken.payload.get.algo match {
      case HS512 => TypedJwtToken[FSym](FSym(chainRef, newPos, state.lastRef, v), state.lastRef.sig).asInstanceOf[TypedJwtToken[Frame]]
      case ES512 => TypedJwtToken[FAsym](FAsym(chainRef, newPos, state.lastRef, v, keyPair.getPublic), keyPair.getPrivate).asInstanceOf[TypedJwtToken[Frame]]
    }
    tokenStorage.add(frameToken).map(fr => {
      currentState = ChainState(chainDefToken, status, Some(frameToken), fr, newPos, v)
      fr
    })
  }

  def suspend(reason: String): Unit = { currentState = currentState.copy(status = Suspended(reason)) }
  def resume(): Unit = { currentState = currentState.copy(status = Active) }
  def delete(reason: String): Unit = { currentState = currentState.copy(status = Deleted(reason)) }
}


object ChainRegistry {

  sealed trait Command
  sealed trait ChainError
  object ChainError {
    final case object AccessDenied extends ChainError
    final case class ChainNotDefined(chainRef: ChainRef) extends ChainError
    final case object ChainDeleted extends ChainError
    final case class InternalError(details: String) extends ChainError
  }

  sealed trait DataCommand extends Command
  object DataCommand {
    import DataReply._
    final case class Post(j: Json, replyTo: ActorRef[Either[ChainError, PostResult]]) extends DataCommand
    final case class GetPage(fromPos: Position, size: Short, replyTo: ActorRef[Either[ChainError, Vector[FrameToken]]]) extends DataCommand
    final case class GetBySig(sig: FrameRef, replyTo: ActorRef[Either[ChainError, Option[FrameToken]]]) extends DataCommand
  }

  sealed trait DataReply
  object DataReply {
    sealed trait PostResult extends DataReply
    object PostResult {
      final case class Acked(fr: FrameRef) extends PostResult
      final case class Rejected(j: Json, reason: String) extends PostResult
    }
  }

  final case class NodeInfo(pubKeys: Set[PubKey], host: String, port: Int)

  sealed trait AdminCommandResult
  object AdminCommandResult {
    sealed trait ChainCreationResult extends AdminCommandResult { val chainDefToken: ChainDefToken }
    object ChainCreationResult {
      sealed trait ChainCreationSuccess extends ChainCreationResult { val chainDefToken: ChainDefToken }
      final case class ChainCreated(chainDefToken: ChainDefToken, replyTo: ActorRef[DataCommand]) extends ChainCreationSuccess
      final case class ChainAlreadyExists(chainDefToken: ChainDefToken, replyTo: ActorRef[DataCommand]) extends ChainCreationSuccess

      sealed trait ChainCreationError extends ChainCreationResult
      final case class GeneralChainError(chainDefToken: ChainDefToken, error: ChainError) extends ChainCreationError
      final case class CannotWriteIntoDerivedChain(chainDefToken: ChainDefToken) extends ChainCreationError
      final case class InvalidChainDefToken(chainDefToken: ChainDefToken) extends ChainCreationError
    }

    final case class Succeeded() extends AdminCommandResult
    //final case class ChainProxy(error: ChainError) extends AdminCommandResult

    final case class KnownNodes(nodes: Set[NodeInfo]) extends AdminCommandResult
    final case class KnownPubKeys(nodes: Set[PubKey]) extends AdminCommandResult
    final case class GetChainsResult(chains: Vector[(ChainDefToken, ChainState)]) extends AdminCommandResult

  }


  sealed trait AdminCommand extends Command
  object AdminCommand {
    import AdminCommandResult._
    final case class CreateChain(chainDefToken: ChainDefToken, replyTo: ActorRef[ChainCreationResult]) extends AdminCommand
    final case class PauseChain(chainRef: ChainRef, reason: String, replyTo: ActorRef[Either[ChainError, Succeeded]]) extends AdminCommand
    final case class ResumeChain(chainRef: ChainRef, replyTo: ActorRef[Either[ChainError, Succeeded]]) extends AdminCommand
    final case class DeleteChain(chainRef: ChainRef, reason: String, replyTo: ActorRef[Either[ChainError, Succeeded]]) extends AdminCommand

    sealed trait NodeCommand extends AdminCommand
    object NodeCommand {
      final case class GetKnownNodes(replyTo: ActorRef[KnownNodes]) extends AdminCommand
      final case class GetKnownPubKeys(replyTo: ActorRef[KnownPubKeys]) extends AdminCommand
      final case class AddNode(replyTo: ActorRef[KnownPubKeys]) extends AdminCommand
    }

    final case class GetChain(chainRef: ChainRef, replyTo: ActorRef[Either[ChainError, ActorRef[DataCommand]]]) extends AdminCommand
    final case class GetChains(replyTo: ActorRef[GetChainsResult]) extends AdminCommand
    final case class GetChainDef(chainRef: ChainRef, replyTo: ActorRef[Option[ChainDefToken]]) extends AdminCommand
    final case class GetChainState(chainRef: ChainRef, replyTo: ActorRef[Option[ChainState]]) extends AdminCommand
    final case class GetChainStatus(chainRef: ChainRef, replyTo: ActorRef[Option[ChainStatus]]) extends AdminCommand
  }


  def adminCommand(keyPair: KeyPair, knownPubKeys: Set[PubKey]): Behavior[AdminCommand] =
    ContextAware[AdminCommand] { ctx =>
      import AdminCommand._
      import AdminCommandResult._
      import AdminCommandResult.ChainCreationResult._
      import ChainError._

      val chains = mutable.HashMap.empty[ChainRef, Chain2]
      //val subscribers = mutable.HashMap.empty[ChainRef, Chain2]
      val nodes = mutable.Set.empty[NodeInfo]

      Static {
        case CreateChain (chainDefToken, replyTo) =>
          val chainRef: ChainRef = chainDefToken
          val reply =
            (chains get chainRef) match {
              case Some(chain) => ChainAlreadyExists(chainDefToken, ctx.spawn(dataCommand(chain), chainRef.sig))
              case None =>
                val chain = new Chain2(keyPair, chainDefToken)
                chains += (chainRef -> chain)
                ChainAlreadyExists(chainDefToken, ctx.spawn(dataCommand(chain), chainRef.sig))
            }
          replyTo ! reply
        case PauseChain(chainRef, reason, replyTo) =>
          replyTo ! chains.get(chainRef).map(c => {c.suspend(reason); Succeeded()}).toRight(ChainNotDefined(chainRef: ChainRef))
        case ResumeChain(chainRef, replyTo) =>
          replyTo ! chains.get(chainRef).map(c => {c.resume(); Succeeded()}).toRight(ChainNotDefined(chainRef: ChainRef))
        case DeleteChain(chainRef, reason, replyTo) =>
          replyTo ! chains.get(chainRef).map(c => {c.delete(reason); Succeeded()}).toRight(ChainNotDefined(chainRef: ChainRef))
        case GetChain(chainRef, replyTo) =>
          replyTo ! ctx.child(chainRef.sig).map(_.asInstanceOf[ActorRef[DataCommand]]).toRight(ChainNotDefined(chainRef: ChainRef))

        case NodeCommand.GetKnownNodes(replyTo: ActorRef[KnownNodes]) => KnownNodes(nodes.toSet)
        case NodeCommand.GetKnownPubKeys(replyTo: ActorRef[KnownPubKeys]) => KnownPubKeys(knownPubKeys ++ nodes.flatMap(_.pubKeys)) // TODO: Add more keys lifecycle logic

        case GetChainDef (chainRef, replyTo) => replyTo ! chains.get(chainRef).map(_.chainDefToken)
        case GetChainState (chainRef, replyTo) => replyTo ! chains.get(chainRef).map(_.state)
        case GetChainStatus (chainRef, replyTo) => replyTo ! chains.get(chainRef).map(_.status)
        case GetChains (replyTo) => replyTo ! GetChainsResult(chains.values.map(c => (c.chainDefToken, c.state)).toVector)
      }
    }

  def dataCommand(chain: Chain2): Behavior[DataCommand] = {
    import DataCommand._
    import DataReply._
    Static {
      case Post(j, replyTo) => {
        val ret = chain.add(j).map(PostResult.Acked(_)).left.map(ChainError.InternalError(_))
        replyTo ! ret
      }
      case GetPage(fromPos, size, replyTo) => replyTo ! Right(chain.tokenStorage.getPage(fromPos, size))
      case GetBySig(sig, replyTo) => replyTo ! Right(chain.tokenStorage.get(sig))
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