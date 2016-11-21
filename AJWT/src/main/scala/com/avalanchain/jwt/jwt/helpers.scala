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
import akka.stream.ActorMaterializer
import akka.stream.actor.{ActorSubscriber, MaxInFlightRequestStrategy}
import akka.stream.actor.ActorSubscriberMessage.OnNext
import akka.stream.scaladsl.{Flow, RunnableGraph, Sink, Source}
import cats.data.Xor
import com.avalanchain.jwt.basicChain.ChainDef.{Derived, Fork, New}
import com.avalanchain.jwt.basicChain.ChainDerivationFunction._
import com.avalanchain.jwt.basicChain.JwtAlgo.{ES512, HS512}
import com.avalanchain.jwt.basicChain._
import com.avalanchain.jwt.jwt.actors.ChainRegistryActor.JwtError.{IncorrectJwtTokenFormat, JwtTokenPayloadParsingError}
import com.avalanchain.jwt.jwt.actors.{ChainPersistentActor, ChainRegistryActor}
import com.avalanchain.jwt.jwt.actors.ChainRegistryActor._
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
package object helpers {

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





//  def PersistentSink2[T](pid: String)(implicit actorRefFactory: ActorRefFactory) =
//    Sink.actorRefWithAck[T](actorRefFactory.actorOf(ChainPersistentActor.props(pid), pid),
//      ChainPersistentActor.Init, ChainPersistentActor.Ack, ChainPersistentActor.Complete)




  import ChainCreationError._

//  def PersistentJsonSink(chainDefToken: ChainDefToken)(implicit actorRefFactory: ActorRefFactory, timeout: Timeout) = {
//    //: Xor[ChainCreationError, Sink[FrameToken, NotUsed]] = {
//    val chainCreationResult = Await.result({
//      (actorRefFactory.actorSelection(ChainRegistryActor.actorId) ? CreateChain(chainDefToken)).mapTo[ChainCreationResult]
//    }, 5 seconds)
//
//    //    implicit val askTimeout = Timeout(5.seconds)
//    //    val flow = Source
//    //      .single(chainRef)
//    //      .mapAsync(parallelism = 1)(ccr => (actorRefFactory.actorSelection(ccr.sig) ? GetChainByRef(chainRef)).mapTo[ChainCreationResult])
//    //      .map(e => e.ref)
//
//    Sink.actorRefWithAck[Json](chainCreationResult.ref,
//      ChainPersistentActor.Init, ChainPersistentActor.Ack, ChainPersistentActor.Complete)
//  }
//
//  def PersistentSink[T](chainDefToken: ChainDefToken)(implicit actorRefFactory: ActorRefFactory, timeout: Timeout, encoder: Encoder[T]) = {
//    Flow[T].map(_.asJson).to(PersistentJsonSink(chainDefToken))
//  }
//
//  def PersistentFrameSink(chainRef: ChainRef)(implicit actorRefFactory: ActorRefFactory, timeout: Timeout): Xor[ChainRegistryError, Sink[FrameToken, NotUsed]] = {
//    val chainByRefResult = Await.result({
//      (actorRefFactory.actorSelection(ChainRegistryActor.actorId) ? GetChainByRef(chainRef)).mapTo[GetChainResult]
//    }, 5 seconds)
//
//    chainByRefResult match {
//      case Xor.Right((chainDefToken, actorRef)) =>
//        if (chainDefToken.payload.isEmpty) Xor.left(InvalidChainDefToken(chainDefToken))
//        else chainDefToken.payload.get match {
//          case New(_, _, _, _) | Fork(_, _, _, _, _) => Xor.right(Sink.actorRefWithAck[FrameToken](actorRef,
//            ChainPersistentActor.Init, ChainPersistentActor.Ack, ChainPersistentActor.Complete))
//          case Derived(_, _, _, _, _) => Xor.left(CannotWriteIntoDerivedChain(chainRef))
//        }
//      case Xor.Left(e: ChainRegistryError) => Xor.Left(e)
//    }
//  }

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
      map(_.map(x => Xor.fromOption[JwtError, Frame](x.payload, IncorrectJwtTokenFormat)))
  }

  def PersistentJsonSource(chainRef: ChainRef, fromPos: Position, toPos: Position, inMem: Boolean)
                         (implicit actorSystem: ActorSystem, timeout: Timeout): Xor[ChainRegistryError, Source[Xor[JwtError, Json], NotUsed]] = {
    PersistentFrameSource(chainRef, fromPos, toPos, inMem)(actorSystem, timeout).map(_.map(_.map(_.v)))
  }

  def PersistentSource[T](chainRef: ChainRef, fromPos: Position, toPos: Position, inMem: Boolean)
                         (implicit actorSystem: ActorSystem, timeout: Timeout, decoder: Decoder[T]): Xor[ChainRegistryError, Source[Xor[JwtError, T], NotUsed]] = {
    PersistentJsonSource(chainRef, fromPos, toPos, inMem)(actorSystem, timeout).
      map(_.map(_.flatMap(_.as[T].leftMap(e => JwtTokenPayloadParsingError(e)))))
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

//  def derivedChain(chainDefToken: ChainDefToken, inMem: Boolean)(implicit actorSystem: ActorSystem, timeout: Timeout):
//    Xor[ChainRegistryError, RunnableGraph[NotUsed]] = {
//    chainDefToken.payload.get match {
//      case derived: Derived => {
//        PersistentJsonSource(derived.parent, 0, Long.MaxValue, inMem).map(source =>
//          derived.cdf match {
//            case Copy => source.map(_.toOption.get).to(PersistentJsonSink(chainDefToken)) // TODO: Deal with failures explicitely
//            case ChainDerivationFunction.Map(f) => source.map(_.toOption.get).map(e => ScriptFunction(f)(e)).to(PersistentSink[Json](chainDefToken))
//            case Filter(f) => source.map(_.toOption.get).filter(e => ScriptPredicate(f)(e)).to(PersistentSink[Json](chainDefToken))
//            //case Fold(f, init) => source.map(_.fold(init)((acc, e) => ScriptFunction2(f)(acc, e)).to(PersistentSink[Json](chainDefToken)))
//            case Fold(f, init) => source.map(_.toOption.get).scan(init)((acc, e) => ScriptFunction2(f)(acc, e)).to(PersistentSink[Json](chainDefToken))
//            case GroupBy(f, max) => source.map(_.toOption.get).groupBy(max, e => ScriptFunction(f)(e)).to(PersistentSink[Json](chainDefToken))
//          }
//        )
//      }
//      case New(_, _, _, _) | Fork(_, _, _, _, _) => throw new RuntimeException("Impossible case")
//    }
//  }
}
