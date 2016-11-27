package com.avalanchain.jwt.jwt.actors.network

import java.security.KeyPair
import java.util.UUID

import akka.actor.{ActorRef, ActorRefFactory, ActorSystem}
import akka.stream.Materializer
import akka.stream.scaladsl.{Flow, Sink, Source}
import com.avalanchain.jwt.basicChain.ChainDef.{Derived, Fork, New}
import com.avalanchain.jwt.basicChain.ChainDerivationFunction.{Copy, Filter, Fold, GroupBy}
import com.avalanchain.jwt.basicChain.JwtAlgo.{ES512, HS512}
import com.avalanchain.jwt.basicChain._
import com.avalanchain.jwt.jwt.script.{ScriptFunction, ScriptFunction2, ScriptPredicate}
import com.rbmhtechnology.eventuate.DurableEvent
import com.rbmhtechnology.eventuate.adapter.stream.{DurableEventSource, DurableEventWriter}
import com.rbmhtechnology.eventuate.adapter.stream.DurableEventProcessor._
import com.rbmhtechnology.eventuate.log.leveldb.LeveldbEventLog

import io.circe.{Decoder, Encoder, Json}
import io.circe.syntax._
import io.circe.generic.auto._

import scala.collection.immutable.Seq
import scala.util.Try

/**
  * Created by Yuriy Habarov on 26/11/2016.
  */


trait Chain {
  val nodeId: NodeId
  val chainDefToken: ChainDefToken
  val keyPair: KeyPair
  implicit val actorSystem: ActorSystem
  implicit val materializer: Materializer

  if (chainDefToken.payload.isEmpty) throw new RuntimeException(s"Inconsistent ChainDefToken: '$chainDefToken'")
  val chainRef = ChainRef(chainDefToken)
  def status = ChainStatus.Created

  protected def newId() = UUID.randomUUID().toString
  protected def createLog(id: String = ""): ActorRef =
    actorSystem.actorOf(LeveldbEventLog.props(chainRef.sig + (if (id.isEmpty) "" else "." + id), nodeId))
  protected val eventLog = createLog()
  protected val initialState = ChainState(None, new FrameRef(chainRef.sig), -1)
  def sourceFrame = Source.fromGraph(DurableEventSource(eventLog)).map(_.payload.asInstanceOf[FrameToken])
  def source = sourceFrame.map(_.payloadJson)

  private var currentFrame: Option[FrameToken] = None
  private var currentValue: Option[Json] = None
  private var currentPosition: Position = -1
  def current = currentValue
  def pos = currentPosition

  protected val processCurrent = sourceFrame.runWith(Sink.foreach(frame => {
    currentFrame = Some(frame)
    if (frame.payload.nonEmpty) {
      currentPosition = frame.payload.get.pos
      currentValue = Some(frame.payload.get.v
      )
    }
  }))
}

class NewChain(val nodeId: NodeId, val chainDefToken: ChainDefToken, val keyPair: KeyPair,
               implicit val actorSystem: ActorSystem, implicit val materializer: Materializer) extends Chain {

  protected val commandLog = createLog("COM")

  //    def pos: Position = state.pos
  //    def current: Option[FrameToken] = state.frame

  def sink = Flow[Cmd].map(DurableEvent(_)).via(DurableEventWriter(newId, commandLog)).to(Sink.ignore)

  def processingLogic(state: ChainState, event: DurableEvent): (ChainState, Seq[TypedJwtToken[Frame]]) = {
    event.payload match {
      case cmd: Cmd =>
        val newPos: Position = state.pos + 1
        val token = chainDefToken.payload.get.algo match {
          case HS512 => TypedJwtToken[FSym](FSym(chainRef, newPos, state.lastRef, cmd.v), state.lastRef.sig).asInstanceOf[FrameToken]
          case ES512 => TypedJwtToken[FAsym](FAsym(chainRef, newPos, state.lastRef, cmd.v, keyPair.getPublic), keyPair.getPrivate).asInstanceOf[FrameToken]
        }
        (ChainState(Some(token), FrameRef(token), newPos), Seq(token))
    }
  }

  protected val process = Source.fromGraph(DurableEventSource(commandLog))
    .via(statefulProcessor(newId, eventLog)(initialState)(processingLogic))
    .runWith(Sink.ignore)
}


class DerivedChain(val nodeId: NodeId, val chainDefToken: ChainDefToken, val keyPair: KeyPair,
                   implicit val actorSystem: ActorSystem, implicit val materializer: Materializer) extends Chain {

  protected val commandLog = createLog("COM")

  //    def pos: Position = state.pos
  //    def current: Option[FrameToken] = state.frame

  //def sink = Flow[Cmd].map(DurableEvent(_)).via(DurableEventWriter(newId, commandLog)).to(Sink.ignore)

//  val func: (Json, Json) => Json =
//    chainDefToken.payload.get match {
//      case derived: Derived => {
//        derived.cdf match {
//          case Copy => source.map(_.toOption.get).to(PersistentJsonSink(chainDefToken)) // TODO: Deal with failures explicitely
//          case ChainDerivationFunction.Map(f) => source.map(_.toOption.get).map(e => ScriptFunction(f)(e)).to(PersistentSink[Json](chainDefToken))
//          case Filter(f) => source.map(_.toOption.get).filter(e => ScriptPredicate(f)(e)).to(PersistentSink[Json](chainDefToken))
//          //case Fold(f, init) => source.map(_.fold(init)((acc, e) => ScriptFunction2(f)(acc, e)).to(PersistentSink[Json](chainDefToken)))
//          case Fold(f, init) => source.map(_.toOption.get).scan(init)((acc, e) => ScriptFunction2(f)(acc, e)).to(PersistentSink[Json](chainDefToken))
//          case GroupBy(f, max) => source.map(_.toOption.get).groupBy(max, e => ScriptFunction(f)(e)).to(PersistentSink[Json](chainDefToken))
//        }
//      }
//      case New(_, _, _, _) | Fork(_, _, _, _, _) => throw new RuntimeException("Impossible case")

  def processingLogic(state: ChainState, event: DurableEvent): (ChainState, Seq[TypedJwtToken[Frame]]) = {
    event.payload match {
      case cmd: Cmd =>
        val newPos: Position = state.pos + 1
        val token = chainDefToken.payload.get.algo match {
          case HS512 => TypedJwtToken[FSym](FSym(chainRef, newPos, state.lastRef, cmd.v), state.lastRef.sig).asInstanceOf[TypedJwtToken[Frame]]
          case ES512 => TypedJwtToken[FAsym](FAsym(chainRef, newPos, state.lastRef, cmd.v, keyPair.getPublic), keyPair.getPrivate).asInstanceOf[TypedJwtToken[Frame]]
        }
        (ChainState(Some(token), FrameRef(token), newPos), Seq(token))
    }
  }

//  protected val process = Source.fromGraph(DurableEventSource(commandLog))
//    .via(statefulProcessor(newId, eventLog)(initialState)(processingLogic))
//    .runWith(Sink.ignore)
}
