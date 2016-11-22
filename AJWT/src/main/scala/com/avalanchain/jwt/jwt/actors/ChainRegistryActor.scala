package com.avalanchain.jwt.jwt.actors

import akka.NotUsed
import akka.actor.{ActorLogging, ActorRef, Props}
import akka.persistence.PersistentActor
import akka.persistence.inmemory.query.scaladsl.InMemoryReadJournal
import akka.persistence.query.PersistenceQuery
import akka.persistence.query.journal.leveldb.scaladsl.LeveldbReadJournal
import akka.persistence.query.scaladsl.EventsByPersistenceIdQuery
import akka.stream.ActorMaterializer
import akka.stream.scaladsl.{Sink, Source}
import cats.implicits._
import com.avalanchain.jwt.jwt.actors.ChainRegistryActor.JwtError.IncorrectJwtTokenFormat
import io.circe.DecodingFailure
//import com.avalanchain.jwt.actors.ChainRegistryActor.{ChainNotFound, GetSnapshotJsonSource, _}
import com.avalanchain.jwt.basicChain.{ChainRef, Frame, _}
import io.circe.Json

import scala.collection.mutable

/**
  * Created by Yuriy Habarov on 21/05/2016.
  */
object ChainRegistryActor {
  sealed trait Command
  final case class CreateChain(chainDefToken: ChainDefToken) extends Command
  final case class GetChainByRef(chainRef: ChainRef) extends Command

  final case class GetJsonSink(chainRef: ChainRef) extends Command

  final case class GetJsonSource(chainRef: ChainRef, from: Position, to: Position) extends Command
  final case class GetFrameSource(chainRef: ChainRef, fromPos: Position, toPos: Position) extends Command
  final case class GetFrameTokenSource(chainRef: ChainRef, fromPos: Position, toPos: Position) extends Command

  final case class GetSnapshotJsonSource(chainRef: ChainRef, from: Position, to: Position) extends Command
  final case class GetSnapshotFrameSource(chainRef: ChainRef, fromPos: Position, toPos: Position) extends Command
  final case class GetSnapshotFrameTokenSource(chainRef: ChainRef, fromPos: Position, toPos: Position) extends Command

  object GetChains extends Command
  object GetState extends Command
  object PrintState extends Command

  sealed trait ChainCreationResult { val chainDefToken: ChainDefToken }
  final case class ChainCreated(chainDefToken: ChainDefToken) extends ChainCreationResult
  final case class ChainAlreadyExists(chainDefToken: ChainDefToken) extends ChainCreationResult
  //final case class ChainCreationFailed(chainDefToken: ChainDefToken, ref: ActorRef) extends ChainCreationResult

  sealed trait ChainRegistryError
  //final case class ChainFound(chainDefToken: ChainDefToken, actorRef: ActorRef) extends ChainRegistryError
  final case class ChainNotFound(chainRef: ChainRef) extends ChainRegistryError

  sealed trait ChainCreationError extends ChainRegistryError
  object ChainCreationError {
    final case class ChainNotDefined(chainRef: ChainRef) extends ChainCreationError
    final case class CannotWriteIntoDerivedChain(chainRef: ChainRef) extends ChainCreationError
    final case class InvalidChainDefToken(chainDefToken: ChainDefToken) extends ChainCreationError
  }

  sealed trait JwtError
  object JwtError {
    final case object IncorrectJwtTokenFormat extends JwtError
    final case class JwtTokenPayloadParsingError(e: DecodingFailure) extends JwtError
  }

  type GetChainResult = Either[ChainCreationError, (ChainDefToken, ActorRef)]

  def props() = Props(new ChainRegistryActor())
  val actorId = "chainregistry"
}

class ChainRegistryActor() extends PersistentActor with ActorLogging {
  import ChainRegistryActor._

  private implicit val materializer = ActorMaterializer()

  override def persistenceId = actorId

  private val state = mutable.HashMap.empty[ChainRef, ChainDefToken]

  private def updateState(chainDefToken: ChainDefToken): Unit = {
    state += (ChainRef(chainDefToken) -> chainDefToken)
    log.info(s"ChainDefToken $chainDefToken registered.")
  }

  def currentState = state

  val receiveRecover: Receive = {
    case chainDefToken: ChainDefToken => updateState(chainDefToken)
  }

  def save(chainDefToken: ChainDefToken, actorRef: ActorRef) = {
    if (log.isDebugEnabled) {
      log.debug(s"ChainDefToken received: $chainDefToken")
    }
    persist(chainDefToken) (cdf => {
      updateState(cdf)
      sender() ! ChainCreated(cdf)//, actorRef)
    })
  }

  val receiveCommand: Receive = {
    case CreateChain(chainDefToken) => {
      val pid: String = chainDefToken.sig
      context.child(pid) match {
        case Some(actorRef) => sender() ! ChainAlreadyExists(chainDefToken) //, actorRef)
        case None => {
          val actorRef = context.actorOf(ChainPersistentActor.props(chainDefToken), pid)
          save(chainDefToken, actorRef)
        }
      }
    }

    case GetChainByRef(chainRef) => sender() ! tryGetChain(chainRef)

    case GetJsonSink(chainRef) => sender() ! tryGetChain(chainRef).map(c =>
      Sink.actorRefWithAck[Json](c._2, ChainPersistentActor.Init, ChainPersistentActor.Ack, ChainPersistentActor.Complete))

    case GetFrameTokenSource(chainRef, fromPos, toPos) => sender() ! (tryGetChain(chainRef).map(_ => getTokenFrameSource(chainRef, fromPos, toPos)))
    case GetFrameSource(chainRef, fromPos, toPos) => sender() ! (tryGetChain(chainRef).map(_ => getFrameSource(chainRef, fromPos, toPos)))
    case GetJsonSource(chainRef, fromPos, toPos) => sender() ! (tryGetChain(chainRef).map(_ => getFrameSource(chainRef, fromPos, toPos).map(_.map(_.v))))

    case GetSnapshotFrameTokenSource(chainRef, fromPos, toPos) => sender() ! (tryGetChain(chainRef).map(_ => getSnapshotTokenFrameSource(chainRef, fromPos, toPos)))
    case GetSnapshotFrameSource(chainRef, fromPos, toPos) => sender() ! (tryGetChain(chainRef).map(_ => getSnapshotFrameSource(chainRef, fromPos, toPos)))
    case GetSnapshotJsonSource(chainRef, fromPos, toPos) => sender() ! (tryGetChain(chainRef).map(_ => getSnapshotFrameSource(chainRef, fromPos, toPos).map(_.map(_.v))))

    case PrintState | "print" => println(s"State: $state")
    case GetChains | GetState | "state" => sender() ! collection.immutable.Map(state.toSeq: _*)
    case a => log.info(s"Ignored '${a}' '${a.getClass}'")
  }

  def getFrameSource(chainRef: ChainRef, fromPos: Position, toPos: Position): Source[Either[JwtError, Frame], NotUsed] = {
    getTokenFrameSource(chainRef, fromPos, toPos).map(x => Either.fromOption[JwtError, Frame](x.payload, IncorrectJwtTokenFormat))
  }

  def getTokenFrameSource(chainRef: ChainRef, fromPos: Position, toPos: Position): Source[FrameToken, NotUsed] = {
    val readJournal: EventsByPersistenceIdQuery =
      if (false) PersistenceQuery(context.system).readJournalFor[InMemoryReadJournal](InMemoryReadJournal.Identifier)
      else PersistenceQuery(context.system).readJournalFor[LeveldbReadJournal](LeveldbReadJournal.Identifier).asInstanceOf[EventsByPersistenceIdQuery]

    readJournal.eventsByPersistenceId(chainRef.sig, fromPos, toPos).map(_.event.asInstanceOf[FrameToken])
  }

  def getSnapshotFrameSource(chainRef: ChainRef, fromPos: Position, toPos: Position): Source[Either[JwtError, Frame], NotUsed] = {
    getSnapshotTokenFrameSource(chainRef, fromPos, toPos).map(x => Either.fromOption[JwtError, Frame](x.payload, IncorrectJwtTokenFormat))
  }

  def getSnapshotTokenFrameSource(chainRef: ChainRef, fromPos: Position, toPos: Position): Source[FrameToken, NotUsed] = {
    val readJournal: EventsByPersistenceIdQuery =
      if (false) PersistenceQuery(context.system).readJournalFor[InMemoryReadJournal](InMemoryReadJournal.Identifier)
      else PersistenceQuery(context.system).readJournalFor[LeveldbReadJournal](LeveldbReadJournal.Identifier).asInstanceOf[EventsByPersistenceIdQuery]

    readJournal.eventsByPersistenceId(chainRef.sig, fromPos, toPos).map(_.event.asInstanceOf[FrameToken])
  }

  protected def tryGetChain(chainRef: ChainRef): Either[ChainRegistryError, (ChainDefToken, ActorRef)] = {
    state.get(chainRef) match {
      case None => Either.left(ChainNotFound(chainRef))
      case Some(chainDefToken) => {
        context.child(chainRef.sig) match {
          case Some(actorRef) => Either.right(chainDefToken, actorRef)
          case None => {
            val actorRef = context.actorOf(ChainPersistentActor.props(chainDefToken), chainRef.sig)
            Either.right(chainDefToken, actorRef)
          }
        }
      }
    }
  }

  log.info(s"Persistent Actor with id: '${persistenceId}' created.")
}