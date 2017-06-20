import java.net.InetAddress
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

import scala.collection.mutable
import scala.concurrent.Future
import scala.concurrent.duration._
import scala.concurrent.Await
import scala.util.{Success, Try}
import scala.collection.immutable.Map
import scala.collection._
import scala.collection.convert.decorateAsScala._
import java.util.concurrent.ConcurrentHashMap

import akka.actor.PoisonPill
import com.avalanchain.jwt.jwt.CurveContext
import com.avalanchain.jwt.utils._
import com.rbmhtechnology.eventuate.EndpointFilters._
import com.rbmhtechnology.eventuate.adapter.stream.{DurableEventSource, DurableEventWriter}
//import com.rbmhtechnology.eventuate.crdt.{MVRegisterService, ORSetService}
import com.rbmhtechnology.eventuate.{DurableEvent, ReplicationConnection, ReplicationEndpoint}
import com.rbmhtechnology.eventuate.log.leveldb.LeveldbEventLog
import com.typesafe.config.ConfigFactory

import com.avalanchain.jwt.utils.Pipe._
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


class Chain2(val keyPair: KeyPair, val chainDefToken: ChainDefToken) {
  if (chainDefToken.payload.isEmpty) throw new RuntimeException(s"Inconsistent ChainDefToken: '$chainDefToken'")
  val chainRef = ChainRef(chainDefToken)
  def status = ChainStatus.Active

  private var currentState = ChainState(chainDefToken, status, None, new FrameRef(chainRef.sig), -1, parse("{}").getOrElse(Json.Null))
  def state = currentState

  private var tokens = new ConcurrentHashMap[FrameRef, FrameToken].asScala
  private val buffer = new mutable.ArrayBuffer[FrameToken](1024)

  def add(frameToken: FrameToken): Either[String, FrameRef] = {
    tokens += (FrameRef(frameToken) -> frameToken)
    buffer += frameToken
    Right(FrameRef(frameToken))
  }

  def get(frameRef: FrameRef): Option[FrameToken] = tokens.get(frameRef)

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

  def add(v: Json): FrameRef = {
    val newPos = currentState.pos + 1
    val frameToken: TypedJwtToken[Frame] = chainDefToken.payload.get.algo match {
      case HS512 => TypedJwtToken[FSym](FSym(chainRef, newPos, state.lastRef, v), state.lastRef.sig).asInstanceOf[TypedJwtToken[Frame]]
      case ES512 => TypedJwtToken[FAsym](FAsym(chainRef, newPos, state.lastRef, v, keyPair.getPublic), keyPair.getPrivate).asInstanceOf[TypedJwtToken[Frame]]
    }
    add(frameToken)
    val fr = FrameRef(frameToken)
    currentState = ChainState(chainDefToken, status, Some(frameToken), fr, newPos, v)
    fr
  }

  def suspend(reason: String): Unit = { currentState = currentState.copy(status = Suspended(reason)) }
  def resume(): Unit = { currentState = currentState.copy(status = Active) }
  def delete(reason: String): Unit = { currentState = currentState.copy(status = Deleted(reason)) }
}


object ChainRegistry {

  final case class Succeeded()

  sealed trait Command


  sealed trait ChainError
  object ChainError {
    final case object AccessDenied extends ChainError
    final case class ChainNotDefined(chainRef: ChainRef) extends ChainError
    final case object ChainDeleted extends ChainError
    final case class InternalError(details: String) extends ChainError
  }

  sealed trait ChainCommand extends Command
  object ChainCommand {
    sealed trait ChainDataCommand extends ChainCommand
    object ChainDataCommand {
      import DataReply._
      final case class Post(j: Json, replyTo: ActorRef[Either[ChainError, PostResult]]) extends ChainDataCommand
      final case class GetPage(fromPos: Position, size: Short, replyTo: ActorRef[Either[ChainError, Vector[FrameToken]]]) extends ChainDataCommand
      final case class GetBySig(sig: FrameRef, replyTo: ActorRef[Either[ChainError, Option[FrameToken]]]) extends ChainDataCommand
    }

    sealed trait ChainAdminCommand extends ChainCommand
    object ChainAdminCommand {
      final case class GetChainDef(replyTo: ActorRef[Either[ChainError, ChainDefToken]]) extends ChainAdminCommand
      final case class GetChainState(replyTo: ActorRef[Either[ChainError, ChainState]]) extends ChainAdminCommand
      final case class GetChainStatus(replyTo: ActorRef[Either[ChainError, ChainStatus]]) extends ChainAdminCommand
      final case class PauseChain(reason: String, replyTo: ActorRef[Either[ChainError, Succeeded]]) extends ChainAdminCommand
      final case class ResumeChain(replyTo: ActorRef[Either[ChainError, Succeeded]]) extends ChainAdminCommand
      final case class DeleteChain(reason: String, replyTo: ActorRef[Either[ChainError, Succeeded]]) extends ChainAdminCommand
    }
  }

  sealed trait DataReply
  object DataReply {
    sealed trait PostResult extends DataReply
    object PostResult {
      final case class Acked(fr: FrameRef) extends PostResult
      final case class Rejected(j: Json, reason: String) extends PostResult
    }
  }

  sealed trait AdminCommandResult
  object AdminCommandResult {
    import AdminCommand._
    sealed trait ChainCreationResult extends AdminCommandResult
    object ChainCreationResult {
      sealed trait ChainCreationSuccess extends ChainCreationResult { val replyTo: ActorRef[ChainCommand] }
      final case class ChainCreated(replyTo: ActorRef[ChainCommand]) extends ChainCreationSuccess
      final case class ChainAlreadyExists(replyTo: ActorRef[ChainCommand]) extends ChainCreationSuccess

      sealed trait ChainCreationError extends ChainCreationResult
      final case class GeneralChainError(chainDefToken: ChainDefToken, error: ChainError) extends ChainCreationError
      //final case class CannotWriteIntoDerivedChain(chainDefToken: ChainDefToken) extends ChainCreationError
      final case class InvalidChainDefToken(chainDefToken: ChainDefToken) extends ChainCreationError
    }

    sealed trait NodeResult extends AdminCommandResult
    object NodeResult {
      final case class KnownNodes(nodes: Set[ActorRef[NodeCommand]]) extends NodeResult
      final case class KnownPubKeys(nodes: Set[PubKey]) extends NodeResult
      //final case class GetChainsResult(chains: Vector[(ChainState, ActorRef[ChainCommand])]) extends AdminCommandResult
    }
  }


  sealed trait AdminCommand extends Command
  object AdminCommand {
    import AdminCommandResult._
    sealed trait ChainRegistryCommand extends AdminCommand
    object ChainRegistryCommand {
      final case class CreateChain(chainDefToken: ChainDefToken, replyTo: ActorRef[ChainCreationResult]) extends ChainRegistryCommand
      final case class GetChain(chainRef: ChainRef, replyTo: ActorRef[Either[ChainError, ActorRef[ChainCommand]]]) extends ChainRegistryCommand
      final case class GetChains(replyTo: ActorRef[Vector[ActorRef[ChainCommand]]]) extends ChainRegistryCommand
    }

    sealed trait NodeAdminCommand extends AdminCommand
    object NodeAdminCommand {
      import NodeResult._
      final case class AddNode(nodeId: NodeIdToken, replyTo: ActorRef[Either[ChainError, Succeeded]]) extends NodeAdminCommand
      final case class RemoveNode(nodeRef: NodeRef, replyTo: ActorRef[Either[ChainError, Succeeded]]) extends NodeAdminCommand
      final case class GetKnownNodes(replyTo: ActorRef[KnownNodes]) extends NodeAdminCommand
      final case class GetKnownPubKeys(replyTo: ActorRef[KnownPubKeys]) extends NodeAdminCommand
    }

    sealed trait NodeCommand extends AdminCommand
    object NodeCommand {
      final case class GetNodeIdToken(replyTo: ActorRef[NodeIdToken]) extends NodeCommand
      final case class ReplicateChain(chainRef: ChainRef, replyTo: ActorRef[Either[ChainError, Succeeded]]) extends NodeCommand
      final case class StopChainReplication(chainRef: ChainRef, replyTo: ActorRef[Either[ChainError, Succeeded]]) extends NodeCommand
      final case class Stop(replyTo: ActorRef[Either[ChainError, Succeeded]]) extends NodeCommand
    }
  }

  /// Behaviors

  import AdminCommand._
  def chainRegistryCommand(keyPair: KeyPair): Behavior[ChainRegistryCommand] =
    ContextAware[ChainRegistryCommand] { ctx =>
      import AdminCommand.ChainRegistryCommand._
      import AdminCommandResult._
      import AdminCommandResult.ChainCreationResult._
      import ChainError._

      def getChain(chainRef: ChainRef) = ctx.child(chainRef.sig).asInstanceOf[Option[ActorRef[ChainCommand]]]
      def getChainEither(chainRef: ChainRef) = getChain(chainRef).toRight(ChainNotDefined(chainRef: ChainRef))
      def getChains() = ctx.children.map(_.asInstanceOf[ActorRef[ChainCommand]])
      // TODO: Add persistence and recovery

      Static {
        case CreateChain (chainDefToken, replyTo) =>
          val chainRef: ChainRef = chainDefToken
          val reply =
            getChain(chainRef) match {
              case Some(actor) => ChainAlreadyExists(actor)
              case None => ChainCreated(ctx.spawn(chainCommand(keyPair, chainDefToken), chainRef.sig))
            }
          replyTo ! reply
        case GetChain(chainRef, replyTo) => replyTo ! getChainEither(chainRef)
        case GetChains (replyTo) => replyTo ! getChains().toVector //Future.collect()
      }
    }

  def nodeAdminCommand(knownPubKeys: Set[PubKey]): Behavior[NodeAdminCommand] =
    ContextAware[NodeAdminCommand] { ctx =>
      import AdminCommand.NodeAdminCommand._
      import AdminCommandResult.NodeResult._
      import ChainError._

      val pubKeys = mutable.Set.empty[PubKey] ++ knownPubKeys

      def getNode(nodeRef: NodeRef) = ctx.child(nodeRef.sig).asInstanceOf[Option[ActorRef[NodeCommand]]]
      def getNodes() = ctx.children.map(_.asInstanceOf[ActorRef[NodeCommand]])
      // TODO: Add persistence and recovery

      Static {
        case AddNode(nodeIdToken, replyTo) =>
          val nodeRef = NodeRef(nodeIdToken)
          val reply =
            getNode(nodeRef) match {
              case Some(actor) => Right(Succeeded())
              case None =>
                pubKeys += nodeIdToken.payload.get.pub
                ctx.spawn(nodeCommand(nodeIdToken), nodeRef.sig)
                Right(Succeeded())
            }
          replyTo ! reply
        case RemoveNode(nodeRef: NodeRef, replyTo: ActorRef[Either[ChainError, Succeeded]]) =>
          getNode(nodeRef) match {
            case Some(actor) => actor ! NodeCommand.Stop(replyTo)
            case None => replyTo ! Right(Succeeded())
          }
        case GetKnownNodes(replyTo: ActorRef[KnownNodes]) => replyTo ! KnownNodes(getNodes().toSet)
        case GetKnownPubKeys(replyTo: ActorRef[KnownPubKeys]) => replyTo ! KnownPubKeys(pubKeys.toSet)
      }
    }

  def adminCommand(keyPair: KeyPair, knownPubKeys: Set[PubKey], nodeIdToken: NodeIdToken): Behavior[AdminCommand] =
    ContextAware[AdminCommand] { ctx =>
      import ChainCommand._
      val chainRegistry = ctx.spawn(chainRegistryCommand(keyPair), "chainRegistryCommand")
      val nodeAdmin = ctx.spawn(nodeAdminCommand(knownPubKeys), "nodeAdminCommand")
      val node = ctx.spawn(nodeCommand(nodeIdToken), "nodeCommand")

      Static {
        case c: ChainRegistryCommand => chainRegistry ! c
        case c: NodeAdminCommand => nodeAdmin ! c
        case c: NodeCommand => node ! c
      }
    }

  import AdminCommandResult._
  import AdminCommandResult.ChainCreationResult._
  def chainCreationResult(chainCommandHandler: ActorRef[ChainCommand] => Unit, errorHandler: ChainCreationError => Unit): Behavior[ChainCreationResult] =
    ContextAware[ChainCreationResult] { ctx =>

      Static {
        case ChainCreated(replyTo: ActorRef[ChainCommand]) => chainCommandHandler(replyTo)
        case ChainAlreadyExists(replyTo: ActorRef[ChainCommand]) => chainCommandHandler(replyTo)

        case e: ChainCreationError => errorHandler(e)
      }
    }

  def chainDataCommand(chain: Chain2): Behavior[ChainCommand.ChainDataCommand] = {
    import ChainCommand.ChainDataCommand._
    import DataReply._
    Static {
      case Post(j, replyTo) => {
        val ret = Right(chain.add(j) |> (PostResult.Acked(_))).left.map(ChainError.InternalError(_))
        replyTo ! ret
      }
      case GetPage(fromPos, size, replyTo) => replyTo ! Right(chain.getPage(fromPos, size))
      case GetBySig(sig, replyTo) => replyTo ! Right(chain.get(sig))
    }
  }
  def chainAdminCommand(chain: Chain2): Behavior[ChainCommand.ChainAdminCommand] = {
    import ChainCommand._
    import ChainCommand.ChainAdminCommand._
    Static {
      case PauseChain(reason, replyTo) =>
        chain.suspend(reason)
        replyTo ! Right(Succeeded())
      case ResumeChain(replyTo) =>
        chain.resume()
        replyTo ! Right(Succeeded())
      case DeleteChain(reason, replyTo) =>
        chain.delete(reason)
        replyTo ! Right(Succeeded())

      case GetChainDef(replyTo: ActorRef[Either[ChainError, ChainDefToken]]) => replyTo ! Right(chain.chainDefToken)
      case GetChainState(replyTo: ActorRef[Either[ChainError, ChainState]]) => replyTo ! Right(chain.state)
      case GetChainStatus(replyTo: ActorRef[Either[ChainError, ChainStatus]]) => replyTo ! Right(chain.status)
    }
  }

  def chainCommand(keyPair: KeyPair, chainDefToken: ChainDefToken): Behavior[ChainCommand] =
    ContextAware[ChainCommand] { ctx =>
      import ChainCommand._
      val chain = new Chain2(keyPair, chainDefToken)
      val data = ctx.spawn(chainDataCommand(chain), "data")
      val admin = ctx.spawn(chainAdminCommand(chain), "admin")

      Static {
        case c: ChainDataCommand => data ! c
        case c: ChainAdminCommand => admin ! c
      }
    }

  def nodeCommand(nodeIdToken: NodeIdToken): Behavior[NodeCommand] = ContextAware[NodeCommand] { ctx =>
    import NodeCommand._
    Total {
      case GetNodeIdToken(replyTo: ActorRef[NodeIdToken]) => replyTo ! nodeIdToken
        Same
      case ReplicateChain(chainRef: ChainRef, replyTo: ActorRef[Either[ChainError, Succeeded]]) =>
        replyTo ! Right(Succeeded()) // TODO
        Same
      case StopChainReplication(chainRef: ChainRef, replyTo: ActorRef[Either[ChainError, Succeeded]]) =>
        replyTo ! Right(Succeeded()) // TODO
        Same
      case Stop(replyTo: ActorRef[Either[ChainError, Succeeded]]) =>
        replyTo ! Right(Succeeded())
        Stopped
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




////////////////////////////////////////

final case class TestMsg(msg: String)

import ChainRegistry._
import AdminCommand.ChainRegistryCommand._
import ChainCommand.ChainDataCommand._
import AdminCommandResult.ChainCreationResult._
def main(keyPair: KeyPair, i: Int, cycles: Int): Behavior[akka.NotUsed] =
  Full {
    case Sig(ctx, PreStart) =>
      def newChain(jwtAlgo: JwtAlgo = JwtAlgo.HS512, id: Id = randomId) = {
        val chainDef: ChainDef = ChainDef.New(jwtAlgo, id, keyPair.getPublic, ResourceGroup.ALL)
        val chainDefToken = TypedJwtToken[ChainDef](chainDef, keyPair.getPrivate)
        chainDefToken
      }
      def newNodeId(i: Int) = NodeIdToken("Node" + i, "127.0.0.1", 2550 + i, keyPair.getPublic, keyPair.getPrivate)
      val knownPubKeys = Set.empty[PubKey]
      val chains = ctx.spawn(adminCommand(keyPair, knownPubKeys, newNodeId(1)), "chains")
      val chainCommandRefs = mutable.Set.empty[ActorRef[ChainCommand]]

      def msgReplyPrint() = {
        import DataReply._
        val eitherSucceeded: Behavior[Either[ChainError, PostResult]] = Static {
          case Right(s) => println(s"Message sending: $s")
          case Left(e) => println(s"Message sending failed with error: $e")
        }
        ctx.spawnAnonymous(eitherSucceeded)
      }
      def msgReply100k(cycles: Int, chainCommandActorRef: ActorRef[ChainCommand]) = {
        import DataReply._
        var counter = 0
        val startMillis = System.currentTimeMillis()

        def sendMsg(handler: ActorRef[Either[ChainError, PostResult]]) = chainCommandActorRef ! Post(TestMsg("testmsg").asJson, handler)
        val eitherSucceeded: Behavior[Either[ChainError, PostResult]] = Static {
          case Right(s) => {
            counter += 1
            if (counter < cycles) { sendMsg(ctx.child("handler").get.asInstanceOf[ActorRef[Either[ChainError, PostResult]]]) }
            else println(s"All $cycles processed, in: ${System.currentTimeMillis() - startMillis} ms")
          }
          case Left(e) => println(s"Message sending failed with error: $e")
        }
        val handler = ctx.spawn(eitherSucceeded, "handler")
        sendMsg(handler)
      }
      val createdChain =
        ctx.spawn(chainCreationResult(
          chainCommandActorRef => {
            println(s"Chain created: $chainCommandActorRef")
            //chainCommandActorRef ! Post(TestMsg("testmsg").asJson, msgReply())
            msgReply100k(cycles, chainCommandActorRef)
          },
          chainCreationError => println(s"Chain creation failed with error: $chainCreationError")), "createdChain")
      val nodeId = newNodeId(i)

      chains ! CreateChain(newChain(), createdChain)

//      def addParticipant(name: String) = {
//        val partRef = ctx.spawn(participant(name), name)
//        ctx.watch(partRef)
//        chatRoomRef ! ChatRoom.GetSession(name, partRef)
//      }
//      addParticipant("User 1")

      Same
    case Sig(_, Terminated(ref)) =>
      Stopped
  }

val keyPair = CurveContext.currentKeys
val system2 = ActorSystem("AC", main(keyPair, 1, 1000000))
//Await.result(system2.whenTerminated, 100.second)

system2.printTree