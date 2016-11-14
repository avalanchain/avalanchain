package com.avalanchain.jwt

import akka.NotUsed
import akka.actor.{ActorContext, ActorLogging, ActorRef, ActorRefFactory, ActorSystem, Props}
import akka.pattern.{ask, pipe}
import akka.persistence.inmemory.query.scaladsl.InMemoryReadJournal
import akka.persistence.query.PersistenceQuery
import akka.persistence.query.journal.leveldb.scaladsl.LeveldbReadJournal
import akka.persistence.query.scaladsl.{EventsByPersistenceIdQuery, ReadJournal}
import akka.persistence.{PersistentActor, SnapshotOffer}
import akka.stream.actor.{ActorSubscriber, MaxInFlightRequestStrategy}
import akka.stream.actor.ActorSubscriberMessage.OnNext
import akka.stream.scaladsl.{Flow, Sink, Source}
import akka.util.Timeout
import cats.data.Xor
import com.avalanchain.jwt.actors.ChainRegistryActor._
import com.avalanchain.jwt.basicChain.ChainDef.{Derived, Nested, New}
import com.avalanchain.jwt.basicChain._

import scala.collection._
import scala.concurrent._
import scala.concurrent.duration._
import scala.util.Try


/**
  * Created by Yuriy on 10/11/2016.
  */
package object actors {

  object ChainPersistentActorSubscriber {
    object GetState
    object PrintState

    def props(pid: String) = Props(new ChainPersistentActorSubscriber(pid))
  }
  class ChainPersistentActorSubscriber(val pid: String) extends PersistentActor with ActorSubscriber with ActorLogging {

    val snapshotInterval: Int = 100
    val maxInFlight: Int = 10

    override def persistenceId = pid

    private var state: ChainState = ChainState(None, new FrameRef(""), -1) // TODO: Replace "" with chainRef.sig
    //saveFrame(state)

    private def applyToken(frameToken: FrameToken): ChainState = {
      ChainState(Some(frameToken), FrameRef(frameToken), frameToken.payload.get.pos)
    }

    private def updateState(chainState: ChainState): Unit = {
      state = chainState
      if (log.isDebugEnabled) {
        log.debug(s"State updated to $state, inFlight: ${inFlight}")
      }
    }

    private var inFlight = 0

    def currentState = state

    val receiveRecover: Receive = {
      case evt: ChainState => updateState(evt)
      case SnapshotOffer(_, snapshot: ChainState) => state = snapshot
    }

    def save(data: FrameToken) = {
      if (log.isDebugEnabled) {
        log.debug(s"Received $data, inFlight: ${inFlight}")
      }
      inFlight += 1
      persist(data) (e => {
        updateState(applyToken(e))
        if (e.payload.get.pos != 0 && e.payload.get.pos % snapshotInterval == 0) {saveSnapshot(currentState)}
        inFlight -= 1
      })
    }

    val receiveCommand: Receive = {
      case OnNext(data: FrameToken) => save(data)

      //    case ChainPersistentActor.PrintState => println(s"State: $state")
      //    case ChainPersistentActor.GetState => sender() ! state
      case "print" => println(s"State: $state")
      case "state" => sender() ! state
      case a => log.info(s"Ignored '$a'")
    }

    private def inf() = inFlight

    override val requestStrategy = new MaxInFlightRequestStrategy(max = maxInFlight) {
      override def inFlightInternally: Int = inFlight
    }

    log.info(s"Persistent Actor with id: '${persistenceId}' created.")
  }

  object ChainRegistryActor {
    sealed trait Command
    final case class CreateChain(chainDefToken: ChainDefToken) extends Command
    final case class GetChainByRef(chainRef: ChainRef) extends Command
    object GetChains extends Command
    object GetState extends Command
    object PrintState extends Command

    sealed trait ChainCreationResult { val chainDefToken: ChainDefToken; val ref: ActorRef }
    final case class ChainCreated(chainDefToken: ChainDefToken, ref: ActorRef) extends ChainCreationResult
    final case class ChainAlreadyExists(chainDefToken: ChainDefToken, ref: ActorRef) extends ChainCreationResult
    //final case class ChainCreationFailed(chainDefToken: ChainDefToken, ref: ActorRef) extends ChainCreationResult

    sealed trait GetChainByRefResult
    final case class ChainFound(chainDefToken: ChainDefToken, actorRef: ActorRef) extends GetChainByRefResult
    final case class ChainNotFound(chainRef: ChainRef) extends GetChainByRefResult

    def props() = Props(new ChainRegistryActor())
    val actorId = "chainregistry"
  }

  class ChainRegistryActor() extends PersistentActor with ActorLogging {
    import ChainRegistryActor._

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
        sender() ! ChainCreated(cdf, actorRef)
      })
    }

    val receiveCommand: Receive = {
      case CreateChain(chainDefToken) => {
        val pid: String = chainDefToken.sig
        context.child(pid) match {
          case Some(actorRef) => sender() ! ChainAlreadyExists(chainDefToken, actorRef)
          case None => {
            val actorRef = context.actorOf(ChainPersistentActor.props(pid), pid)
            save(chainDefToken, actorRef)
          }
        }
      }

      case GetChainByRef(chainRef) => {
        val pid: String = chainRef.sig
        val ret: GetChainByRefResult = state.get(chainRef) match {
          case None => ChainNotFound(chainRef)
          case Some(chainDefToken) => {
            context.child(pid) match {
              case Some(actorRef) => ChainFound(chainDefToken, actorRef)
              case None => {
                val actorRef = context.actorOf(ChainPersistentActor.props(pid), pid)
                ChainFound(chainDefToken, actorRef)
              }
            }
          }
        }
        sender() ! ret
      }

      case PrintState | "print" => println(s"State: $state")
      case GetChains | GetState | "state" => sender() ! collection.mutable.Map(state.toSeq: _*)
      case a => log.info(s"Ignored '${a}' '${a.getClass}'")
    }

    log.info(s"Persistent Actor with id: '${persistenceId}' created.")
  }

  object ChainPersistentActor {
    trait Command
    object GetState extends Command
    object PrintState extends Command

    trait WithAck
    object Init extends WithAck
    object Ack extends WithAck
    object Complete extends WithAck

    def props(pid: String) = Props(new ChainPersistentActor(pid))
  }

  class ChainPersistentActor(val pid: String, val snapshotInterval: Int = 10000) extends PersistentActor with ActorLogging {
    import ChainPersistentActor._

    override def persistenceId = pid

    private var state: ChainState = ChainState(None, new FrameRef(pid), -1) // pid expected to be chainRef

    private def applyToken(frameToken: FrameToken): ChainState = {
      ChainState(Some(frameToken), FrameRef(frameToken), frameToken.payload.get.pos)
    }

    private def updateState(chainState: ChainState): Unit = {
      state = chainState
      if (log.isDebugEnabled) {
        log.debug(s"State updated to $state")
      }
    }

    def currentState = state

    val receiveRecover: Receive = {
      case evt: ChainState => updateState(evt)
      case SnapshotOffer(_, snapshot: ChainState) => state = snapshot
    }

    def save(data: FrameToken) = {
      if (log.isDebugEnabled) {
        log.debug(s"Received $data")
      }
      persist(data) (e => {
        updateState(applyToken(e))
        if (e.payload.get.pos != 0 && e.payload.get.pos % snapshotInterval == 0) {saveSnapshot(currentState)}
        sender() ! Ack
      })
    }

    val receiveCommand: Receive = {
      case Init => sender() ! Ack
      case data: FrameToken => save(data)
      //case OnNext(data: FrameToken) => save(data)
      case Complete =>

      case ChainPersistentActor.PrintState | "print" => println(s"State: $state")
      case ChainPersistentActor.GetState | "state" => sender() ! state
      case a => {
        log.info(s"Ignored '${a}' '${a.getClass}'")
        sender() ! Ack
      }
    }

    log.info(s"Persistent Actor with id: '${persistenceId}' created.")
  }

//  def PersistentSink2[T](pid: String)(implicit actorRefFactory: ActorRefFactory) =
//    Sink.actorRefWithAck[T](actorRefFactory.actorOf(ChainPersistentActor.props(pid), pid),
//      ChainPersistentActor.Init, ChainPersistentActor.Ack, ChainPersistentActor.Complete)

  sealed trait SinkCreationError
  object SinkCreationError {
    final case class ChainNotDefined(chainRef: ChainRef) extends SinkCreationError
    final case class CannotWriteIntoDerivedChain(chainRef: ChainRef) extends SinkCreationError
    final case class InvalidChainDefToken(chainDefToken: ChainDefToken) extends SinkCreationError
  }

  sealed trait SourceCreationError
  object SourceCreationError {
    final case class ChainNotDefined(chainRef: ChainRef) extends SourceCreationError
  }

  import SinkCreationError._

  def PersistentSink[T](chainDefToken: ChainDefToken)(implicit actorRefFactory: ActorRefFactory, timeout: Timeout) = {
    val chainCreationResult = Await.result({
      (actorRefFactory.actorSelection(ChainRegistryActor.actorId) ? CreateChain(chainDefToken)).mapTo[ChainCreationResult]
    }, 5 seconds)

    //    implicit val askTimeout = Timeout(5.seconds)
    //    val flow = Source
    //      .single(chainRef)
    //      .mapAsync(parallelism = 1)(ccr => (actorRefFactory.actorSelection(ccr.sig) ? GetChainByRef(chainRef)).mapTo[ChainCreationResult])
    //      .map(e => e.ref)

    Sink.actorRefWithAck[T](chainCreationResult.ref,
      ChainPersistentActor.Init, ChainPersistentActor.Ack, ChainPersistentActor.Complete)
  }

  def PersistentSink[T <: FrameToken](chainRef: ChainRef)(implicit actorRefFactory: ActorRefFactory, timeout: Timeout): Xor[SinkCreationError, Sink[T, NotUsed]] = {
    val chainByRefResult = Await.result({
      (actorRefFactory.actorSelection(ChainRegistryActor.actorId) ? GetChainByRef(chainRef)).mapTo[GetChainByRefResult]
    }, 5 seconds)

    chainByRefResult match {
      case ChainFound(chainDefToken: ChainDefToken, actorRef) =>
        if (chainDefToken.payload.isEmpty) Xor.left(InvalidChainDefToken(chainDefToken))
        else chainDefToken.payload.get match {
          case New(_, _, _, _) | Nested(_, _, _, _, _) => Xor.right(Sink.actorRefWithAck[T](actorRef,
            ChainPersistentActor.Init, ChainPersistentActor.Ack, ChainPersistentActor.Complete))
          case Derived(_, _, _, _, _) => Xor.left(CannotWriteIntoDerivedChain(chainRef))
        }
      case ChainNotFound(chainRef) => Xor.left(ChainNotDefined(chainRef))
    }
  }

  import SourceCreationError._

  def PersistentSource[T](chainRef: ChainRef, fromPos: Position, toPos: Position, inMem: Boolean)
                         (implicit actorSystem: ActorSystem, timeout: Timeout): Xor[SourceCreationError, Source[T, NotUsed]] = {
    val chainByRefResult = Await.result({
      (actorSystem.actorSelection(ChainRegistryActor.actorId) ? GetChainByRef(chainRef)).mapTo[GetChainByRefResult]
    }, 5 seconds)

    chainByRefResult match {
      case ChainFound(chainDefToken, actorRef) => {
        val readJournal: EventsByPersistenceIdQuery =
          if (inMem) PersistenceQuery(actorSystem).readJournalFor[InMemoryReadJournal](InMemoryReadJournal.Identifier)
          else PersistenceQuery(actorSystem).readJournalFor[LeveldbReadJournal](LeveldbReadJournal.Identifier).asInstanceOf[EventsByPersistenceIdQuery]

        Xor.right(readJournal.eventsByPersistenceId(chainRef.sig, fromPos, toPos).map(_.event.asInstanceOf[T]))
      }
      case ChainNotFound(chainRef) => Xor.left(SourceCreationError.ChainNotDefined(chainRef))
    }
  }
}
