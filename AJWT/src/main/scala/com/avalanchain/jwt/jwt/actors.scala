package com.avalanchain.jwt

import java.security.{KeyPair, PublicKey}
import java.util.UUID

import akka.NotUsed
import akka.util.Timeout
import akka.actor.ActorDSL._
import akka.actor.{ActorContext, ActorLogging, ActorRef, ActorRefFactory, ActorSystem, Props}
import akka.pattern.{ask, pipe}
import akka.persistence.inmemory.query.scaladsl.InMemoryReadJournal
import akka.persistence.query.PersistenceQuery
import akka.persistence.query.journal.leveldb.scaladsl.LeveldbReadJournal
import akka.persistence.query.scaladsl.{CurrentEventsByPersistenceIdQuery, EventsByPersistenceIdQuery, ReadJournal}
import akka.persistence.{PersistentActor, SnapshotOffer}
import akka.stream.actor.{ActorSubscriber, MaxInFlightRequestStrategy}
import akka.stream.actor.ActorSubscriberMessage.OnNext
import akka.stream.scaladsl.{Flow, RunnableGraph, Sink, Source}
import cats.data.Xor
import com.avalanchain.jwt.actors.ChainNode.{GetSink, NewChain}
import com.avalanchain.jwt.actors.ChainRegistryActor._
import com.avalanchain.jwt.actors.JwtError.{IncorrectJwtTokenFormat, JwtTokenPayloadParsingError}
import com.avalanchain.jwt.basicChain.ChainDef.{Derived, Fork, New}
import com.avalanchain.jwt.basicChain.ChainDerivationFunction._
import com.avalanchain.jwt.basicChain.JwtAlgo.{ES512, HS512}
import com.avalanchain.jwt.basicChain._
import com.avalanchain.jwt.jwt.{NodeContext, NodeExtension}
import com.avalanchain.jwt.jwt.script.{ScriptFunction, ScriptFunction2, ScriptPredicate}
import com.avalanchain.jwt.utils.Pipe
import com.typesafe.config.ConfigFactory
import io.circe.{Decoder, DecodingFailure, Encoder, Json}
import io.circe.syntax._
import io.circe.parser._
import io.circe.generic.JsonCodec
import io.circe.generic.auto._

import scala.collection._
import scala.concurrent._
import scala.concurrent.duration._
import scala.util.Try




/**
  * Created by Yuriy on 10/11/2016.
  */
package object actors {

//  object ChainPersistentActorSubscriber {
//    object GetState
//    object PrintState
//
//    def props(pid: String) = Props(new ChainPersistentActorSubscriber(pid))
//  }
//  class ChainPersistentActorSubscriber(val pid: String) extends PersistentActor with ActorSubscriber with ActorLogging {
//
//    val snapshotInterval: Int = 100
//    val maxInFlight: Int = 10
//
//    override def persistenceId = pid
//
//    private var state: ChainState = ChainState(None, new FrameRef(""), -1) // TODO: Replace "" with chainRef.sig
//    //saveFrame(state)
//
//    private def applyToken(frameToken: FrameToken): ChainState = {
//      ChainState(Some(frameToken), FrameRef(frameToken), frameToken.payload.get.pos)
//    }
//
//    private def updateState(chainState: ChainState): Unit = {
//      state = chainState
//      if (log.isDebugEnabled) {
//        log.debug(s"State updated to $state, inFlight: ${inFlight}")
//      }
//    }
//
//    private var inFlight = 0
//
//    def currentState = state
//
//    val receiveRecover: Receive = {
//      case evt: ChainState => updateState(evt)
//      case SnapshotOffer(_, snapshot: ChainState) => state = snapshot
//    }
//
//    def save(data: FrameToken) = {
//      if (log.isDebugEnabled) {
//        log.debug(s"Received $data, inFlight: ${inFlight}")
//      }
//      inFlight += 1
//      persist(data) (e => {
//        updateState(applyToken(e))
//        if (e.payload.get.pos != 0 && e.payload.get.pos % snapshotInterval == 0) {saveSnapshot(currentState)}
//        inFlight -= 1
//      })
//    }
//
//    val receiveCommand: Receive = {
//      case OnNext(data: FrameToken) => save(data)
//
//      //    case ChainPersistentActor.PrintState => println(s"State: $state")
//      //    case ChainPersistentActor.GetState => sender() ! state
//      case "print" => println(s"State: $state")
//      case "state" => sender() ! state
//      case a => log.info(s"Ignored '$a'")
//    }
//
//    private def inf() = inFlight
//
//    override val requestStrategy = new MaxInFlightRequestStrategy(max = maxInFlight) {
//      override def inFlightInternally: Int = inFlight
//    }
//
//    log.info(s"Persistent Actor with id: '${persistenceId}' created.")
//  }

  object ChainRegistryActor {
    sealed trait Command
    final case class CreateChain(chainDefToken: ChainDefToken) extends Command
    final case class GetChainByRef(chainRef: ChainRef) extends Command
    final case class GetJsonSink(chainRef: ChainRef) extends Command
    final case class GetJsonSource(chainRef: ChainRef) extends Command
    object GetChains extends Command
    object GetState extends Command
    object PrintState extends Command

    sealed trait ChainCreationResult { val chainDefToken: ChainDefToken; val ref: ActorRef }
    final case class ChainCreated(chainDefToken: ChainDefToken, ref: ActorRef) extends ChainCreationResult
    final case class ChainAlreadyExists(chainDefToken: ChainDefToken, ref: ActorRef) extends ChainCreationResult
    //final case class ChainCreationFailed(chainDefToken: ChainDefToken, ref: ActorRef) extends ChainCreationResult

    sealed trait ChainRegistryError
    //final case class ChainFound(chainDefToken: ChainDefToken, actorRef: ActorRef) extends ChainRegistryError
    final case class ChainNotFound(chainRef: ChainRef) extends ChainRegistryError

    type GetChainResult = Xor[ChainCreationError, (ChainDefToken, ActorRef)]

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
            val actorRef = context.actorOf(ChainPersistentActor.props(chainDefToken), pid)
            save(chainDefToken, actorRef)
          }
        }
      }

      case GetChainByRef(chainRef) => sender() ! tryGetChain(chainRef)

      case GetJsonSink(chainRef) => tryGetChain(chainRef).map(c =>
        Sink.actorRefWithAck[Json](c._2, ChainPersistentActor.Init, ChainPersistentActor.Ack, ChainPersistentActor.Complete))

      case GetJsonSource(chainRef) => tryGetChain(chainRef).map(c =>
        Sink.actorRefWithAck[Json](c._2, ChainPersistentActor.Init, ChainPersistentActor.Ack, ChainPersistentActor.Complete))


      case PrintState | "print" => println(s"State: $state")
      case GetChains | GetState | "state" => sender() ! collection.immutable.Map(state.toSeq: _*)
      case a => log.info(s"Ignored '${a}' '${a.getClass}'")
    }

    protected def tryGetChain(chainRef: ChainRef): Xor[ChainRegistryError, (ChainDefToken, ActorRef)] = {
      state.get(chainRef) match {
        case None => Xor.left(ChainNotFound(chainRef))
        case Some(chainDefToken) => {
          context.child(chainRef.sig) match {
            case Some(actorRef) => Xor.right(chainDefToken, actorRef)
            case None => {
              val actorRef = context.actorOf(ChainPersistentActor.props(chainDefToken), chainRef.sig)
              Xor.right(chainDefToken, actorRef)
            }
          }
        }
      }
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

    def props(chainDefToken: ChainDefToken) = Props(new ChainPersistentActor(chainDefToken))
  }

  class ChainPersistentActor(val chainDefToken: ChainDefToken, val snapshotInterval: Int = 10000) extends PersistentActor with NodeContext with ActorLogging {
    import ChainPersistentActor._

    val chainRef = ChainRef(chainDefToken)

    override def persistenceId = chainRef.sig

    private var state: ChainState = ChainState(None, new FrameRef(chainRef.sig), -1) // pid expected to be chainRef

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

    def add(v: Json) = {
      val newPos = state.pos + 1
      val frameToken: TypedJwtToken[Frame] = chainDefToken.payload.get.algo match {
        case HS512 => TypedJwtToken[FSym](FSym(chainRef, newPos, state.lastRef, v), state.lastRef.sig).asInstanceOf[TypedJwtToken[Frame]]
        case ES512 => TypedJwtToken[FAsym](FAsym(chainRef, newPos, state.lastRef, v, cryptoContext.currentKeys.getPublic), cryptoContext.currentKeys.getPrivate).asInstanceOf[TypedJwtToken[Frame]]
      }
      save(frameToken)
    }

    val receiveCommand: Receive = {
      case Init => sender() ! Ack
      case frameToken: FrameToken => save(frameToken)
      case json: Json => add(json)
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

  sealed trait ChainCreationError extends ChainRegistryError
  object ChainCreationError {
    final case class ChainNotDefined(chainRef: ChainRef) extends ChainCreationError
    final case class CannotWriteIntoDerivedChain(chainRef: ChainRef) extends ChainCreationError
    final case class InvalidChainDefToken(chainDefToken: ChainDefToken) extends ChainCreationError
  }

  sealed trait JwtError
  object JwtError {
    final case object IncorrectJwtTokenFormat
    final case class JwtTokenPayloadParsingError(e: DecodingFailure)
  }

  import ChainCreationError._

  def PersistentJsonSink(chainDefToken: ChainDefToken)(implicit actorRefFactory: ActorRefFactory, timeout: Timeout) = {
    //: Xor[ChainCreationError, Sink[FrameToken, NotUsed]] = {
    val chainCreationResult = Await.result({
      (actorRefFactory.actorSelection(ChainRegistryActor.actorId) ? CreateChain(chainDefToken)).mapTo[ChainCreationResult]
    }, 5 seconds)

    //    implicit val askTimeout = Timeout(5.seconds)
    //    val flow = Source
    //      .single(chainRef)
    //      .mapAsync(parallelism = 1)(ccr => (actorRefFactory.actorSelection(ccr.sig) ? GetChainByRef(chainRef)).mapTo[ChainCreationResult])
    //      .map(e => e.ref)

    Sink.actorRefWithAck[Json](chainCreationResult.ref,
      ChainPersistentActor.Init, ChainPersistentActor.Ack, ChainPersistentActor.Complete)
  }

  def PersistentSink[T](chainDefToken: ChainDefToken)(implicit actorRefFactory: ActorRefFactory, timeout: Timeout, encoder: Encoder[T]) = {
    Flow[T].map(_.asJson).to(PersistentJsonSink(chainDefToken))
  }

  def PersistentFrameSink(chainRef: ChainRef)(implicit actorRefFactory: ActorRefFactory, timeout: Timeout): Xor[ChainRegistryError, Sink[FrameToken, NotUsed]] = {
    val chainByRefResult = Await.result({
      (actorRefFactory.actorSelection(ChainRegistryActor.actorId) ? GetChainByRef(chainRef)).mapTo[GetChainResult]
    }, 5 seconds)

    chainByRefResult match {
      case Xor.Right((chainDefToken, actorRef)) =>
        if (chainDefToken.payload.isEmpty) Xor.left(InvalidChainDefToken(chainDefToken))
        else chainDefToken.payload.get match {
          case New(_, _, _, _) | Fork(_, _, _, _, _) => Xor.right(Sink.actorRefWithAck[FrameToken](actorRef,
            ChainPersistentActor.Init, ChainPersistentActor.Ack, ChainPersistentActor.Complete))
          case Derived(_, _, _, _, _) => Xor.left(CannotWriteIntoDerivedChain(chainRef))
        }
      case Xor.Left(e: ChainRegistryError) => Xor.Left(e)
    }
  }

  def PersistentFrameTokenSource(chainRef: ChainRef, fromPos: Position, toPos: Position, inMem: Boolean)
                         (implicit actorSystem: ActorSystem, timeout: Timeout): Xor[ChainRegistryError, Source[FrameToken, NotUsed]] = {
    val chainByRefResult = Await.result({
      (actorSystem.actorSelection(ChainRegistryActor.actorId) ? GetChainByRef(chainRef)).mapTo[GetChainResult]
    }, 5 seconds)

    chainByRefResult match {
      case Xor.Right((chainDefToken, actorRef)) => {
        val readJournal: EventsByPersistenceIdQuery =
          if (inMem) PersistenceQuery(actorSystem).readJournalFor[InMemoryReadJournal](InMemoryReadJournal.Identifier)
          else PersistenceQuery(actorSystem).readJournalFor[LeveldbReadJournal](LeveldbReadJournal.Identifier).asInstanceOf[EventsByPersistenceIdQuery]

        Xor.right(readJournal.eventsByPersistenceId(chainRef.sig, fromPos, toPos).map(_.event.asInstanceOf[FrameToken]))
      }
      case Xor.Left(e: ChainRegistryError) => Xor.Left(e)
    }
  }

  def PersistentFrameSource(chainRef: ChainRef, fromPos: Position, toPos: Position, inMem: Boolean)
                        (implicit actorSystem: ActorSystem, timeout: Timeout): Xor[ChainRegistryError, Source[Xor[JwtError, Frame], NotUsed]] = {
    PersistentFrameTokenSource(chainRef, fromPos, toPos, inMem)(actorSystem, timeout).
      map(_.map(x => Xor.fromOption[JwtError, Frame](x.payload, IncorrectJwtTokenFormat.asInstanceOf[JwtError])))
  }

  def PersistentJsonSource(chainRef: ChainRef, fromPos: Position, toPos: Position, inMem: Boolean)
                         (implicit actorSystem: ActorSystem, timeout: Timeout): Xor[ChainRegistryError, Source[Xor[JwtError, Json], NotUsed]] = {
    PersistentFrameSource(chainRef, fromPos, toPos, inMem)(actorSystem, timeout).map(_.map(_.map(_.v)))
  }

  def PersistentSource[T](chainRef: ChainRef, fromPos: Position, toPos: Position, inMem: Boolean)
                         (implicit actorSystem: ActorSystem, timeout: Timeout, decoder: Decoder[T]): Xor[ChainRegistryError, Source[Xor[JwtError, T], NotUsed]] = {
    PersistentJsonSource(chainRef, fromPos, toPos, inMem)(actorSystem, timeout).
      map(_.map(_.flatMap(_.as[T].leftMap(e => JwtTokenPayloadParsingError(e).asInstanceOf[JwtError]))))
  }

  def SnapshotFrameTokenSource(chainRef: ChainRef, fromPos: Position, toPos: Position, inMem: Boolean)
                                (implicit actorSystem: ActorSystem, timeout: Timeout): Xor[ChainRegistryError, Source[FrameToken, NotUsed]] = {
    val chainByRefResult = Await.result({
      (actorSystem.actorSelection(ChainRegistryActor.actorId) ? GetChainByRef(chainRef)).mapTo[GetChainResult]
    }, 5 seconds)

    chainByRefResult.map(_ => {
      val readJournal: CurrentEventsByPersistenceIdQuery =
        if (inMem) PersistenceQuery(actorSystem).readJournalFor[InMemoryReadJournal](InMemoryReadJournal.Identifier)
        else PersistenceQuery(actorSystem).readJournalFor[LeveldbReadJournal](LeveldbReadJournal.Identifier).asInstanceOf[CurrentEventsByPersistenceIdQuery]

      readJournal.currentEventsByPersistenceId(chainRef.sig, fromPos, toPos).map(_.event.asInstanceOf[FrameToken])
    })
  }

  def SnapshotFrameSource(chainRef: ChainRef, fromPos: Position, toPos: Position, inMem: Boolean)
                           (implicit actorSystem: ActorSystem, timeout: Timeout): Xor[ChainRegistryError, Source[Xor[JwtError, Frame], NotUsed]] = {
    SnapshotFrameTokenSource(chainRef, fromPos, toPos, inMem)(actorSystem, timeout).
      map(_.map(x => Xor.fromOption[JwtError, Frame](x.payload, IncorrectJwtTokenFormat.asInstanceOf[JwtError])))
  }

  def SnapshotJsonSource(chainRef: ChainRef, fromPos: Position, toPos: Position, inMem: Boolean)
                          (implicit actorSystem: ActorSystem, timeout: Timeout): Xor[ChainRegistryError, Source[Xor[JwtError, Json], NotUsed]] = {
    SnapshotFrameSource(chainRef, fromPos, toPos, inMem)(actorSystem, timeout).map(_.map(_.map(_.v)))
  }

  def SnapshotSource[T](chainRef: ChainRef, fromPos: Position, toPos: Position, inMem: Boolean)
                         (implicit actorSystem: ActorSystem, timeout: Timeout, decoder: Decoder[T]): Xor[ChainRegistryError, Source[Xor[JwtError, T], NotUsed]] = {
    SnapshotJsonSource(chainRef, fromPos, toPos, inMem)(actorSystem, timeout).
      map(_.map(_.flatMap(_.as[T].leftMap(e => JwtTokenPayloadParsingError(e).asInstanceOf[JwtError]))))
  }

  def derivedChain(chainDefToken: ChainDefToken, inMem: Boolean)(implicit actorSystem: ActorSystem, timeout: Timeout):
    Xor[ChainRegistryError, RunnableGraph[NotUsed]] = {
    chainDefToken.payload.get match {
      case derived: Derived => {
        PersistentJsonSource(derived.parent, 0, Long.MaxValue, inMem).map(source =>
          derived.cdf match {
            case Copy => source.map(_.toOption.get).to(PersistentJsonSink(chainDefToken)) // TODO: Deal with failures explicitely
            case ChainDerivationFunction.Map(f) => source.map(_.toOption.get).map(e => ScriptFunction(f)(e)).to(PersistentSink[Json](chainDefToken))
            case Filter(f) => source.map(_.toOption.get).filter(e => ScriptPredicate(f)(e)).to(PersistentSink[Json](chainDefToken))
            //case Fold(f, init) => source.map(_.fold(init)((acc, e) => ScriptFunction2(f)(acc, e)).to(PersistentSink[Json](chainDefToken)))
            case Fold(f, init) => source.map(_.toOption.get).scan(init)((acc, e) => ScriptFunction2(f)(acc, e)).to(PersistentSink[Json](chainDefToken))
            case GroupBy(f, max) => source.map(_.toOption.get).groupBy(max, e => ScriptFunction(f)(e)).to(PersistentSink[Json](chainDefToken))
          }
        )
      }
      case New(_, _, _, _) | Fork(_, _, _, _, _) => throw new RuntimeException("Impossible case")
    }
  }


  object ChainNode {
    sealed trait ChainNodeRequest
    final case class NewChain(jwtAlgo: JwtAlgo, initValue: Option[Json] = Some(Json.fromString("{}"))) extends ChainNodeRequest
    final case class GetSink(chainRef: ChainRef) extends ChainNodeRequest
    final case class GetFrameTokenSource(chainRef: ChainRef, fromPos: Position, toPos: Position) extends ChainNodeRequest

    //sealed trait ChainNodeResponse
    //final case class FrameTokenSink(sink: Sink[Json, NotUsed])
    //final case class FrameTokenSource(source: Source[FrameToken, NotUsed])
  }

  def createNode(keyPair: KeyPair, knownKeys: Set[PublicKey]) (implicit encoder: Encoder[ChainDef], decoder: Decoder[ChainDef]): ActorRef = {

    val system = ActorSystem("node", ConfigFactory.load("application.conf")) // TODO: Add config
    import system.dispatcher
    implicit val timeout = Timeout(5 seconds)

    val node = actor(system, "node")(new Act {
      val registry = actor("registry")(new ChainRegistryActor())
      val tc = actor("testChild")(new Act {
        whenStarting {
          context.parent ! ("hello from " + self.path)
        }
      })
      become {
        case GetChains => pipe(registry ? GetChains) to sender()
        case PrintState => registry ! PrintState

        case NewChain(jwtAlgo, initValue) =>
          val chainDef: ChainDef = ChainDef.New(jwtAlgo, UUID.randomUUID(), keyPair.getPublic, initValue)
          val chainDefToken = TypedJwtToken[ChainDef](chainDef, keyPair.getPrivate)
          pipe(registry ? CreateChain(chainDefToken)) to sender()
        case gc: GetChainByRef => pipe(registry ? gc) to sender()

        case GetSink(chainRef) =>

        case s: String => println(s"Echo $s")
      }
    })

    node
  }
}
