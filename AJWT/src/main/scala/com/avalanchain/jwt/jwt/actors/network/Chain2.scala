package com.avalanchain.jwt.jwt.actors.network

import java.security.KeyPair
import java.util.UUID

import akka.actor.{ActorRef, ActorRefFactory, ActorSystem}
import akka.stream.Materializer
import akka.stream.scaladsl.{Flow, Sink, Source}
import com.avalanchain.jwt.basicChain.ChainDef.{Derived, Fork, New}
import com.avalanchain.jwt.basicChain.ChainDerivationFunction._
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
  var statusInternal: ChainStatus = ChainStatus.Created
  def status = statusInternal

  protected def newId() = UUID.randomUUID().toString
  protected def createLog(cr:ChainRef, id: String = ""): ActorRef =
    actorSystem.actorOf(LeveldbEventLog.props(cr.sig + (if (id.isEmpty) "" else "." + id), nodeId))

  protected val commandLog: Option[ActorRef]
  protected val eventLog = createLog(chainRef)
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

  protected def toFrame(state: ChainState, v: Json) = {
    val newPos: Position = state.pos + 1
    val token = chainDefToken.payload.get.algo match {
      case HS512 => TypedJwtToken[FSym](FSym(chainRef, newPos, state.lastRef, v), state.lastRef.sig).asInstanceOf[FrameToken]
      case ES512 => TypedJwtToken[FAsym](FAsym(chainRef, newPos, state.lastRef, v, keyPair.getPublic), keyPair.getPrivate).asInstanceOf[FrameToken]
    }
    (ChainState(Some(token), FrameRef(token), newPos), Seq(token))
  }

  def processingLogic(state: ChainState, event: DurableEvent): (ChainState, Seq[FrameToken])

  protected val process =
    commandLog.map(
      cl => Source.fromGraph(DurableEventSource(cl))
        .via(statefulProcessor(newId, eventLog)(initialState)(processingLogic))
        .runWith(Sink.ignore))

}

class NewChain(val nodeId: NodeId, val chainDefToken: ChainDefToken, val keyPair: KeyPair,
               implicit val actorSystem: ActorSystem, implicit val materializer: Materializer) extends Chain {

  protected val commandLog = Some(createLog(chainRef, "COM"))

  def sink = Flow[Cmd].map(DurableEvent(_)).via(DurableEventWriter(newId, commandLog.get)).to(Sink.ignore)

  def processingLogic(state: ChainState, event: DurableEvent): (ChainState, Seq[FrameToken]) = {
    event.payload match {
      case cmd: Cmd => toFrame(state, cmd.v)
    }
  }
}


class DerivedChain(val nodeId: NodeId, val chainDefToken: ChainDefToken, val keyPair: KeyPair,
                   implicit val actorSystem: ActorSystem, implicit val materializer: Materializer) extends Chain {

  val derived: Derived = chainDefToken.payload.get match {
    case derived: Derived => derived
    case New(_, _, _, _) | Fork(_, _, _, _, _) => {
      statusInternal = ChainStatus.Failed("Impossible case")
      throw new RuntimeException("Impossible case")
    }
  }

  protected val commandLog = Some(createLog(derived.parent))

  val func: (Json, Json) => Seq[Json] =
    derived.cdf match {
      case Copy => (_, e) => Seq(e) // TODO: Deal with failures explicitely
      case ChainDerivationFunction.Map(f) => (_, e) => Seq(ScriptFunction(f)(e))
      case Filter(f) => (_, e) => if (ScriptPredicate(f)(e)) Seq(e) else Seq()
      //case Fold(f, init) => source.map(_.fold(init)((acc, e) => ScriptFunction2(f)(acc, e)).to(PersistentSink[Json](chainDefToken)))
      case Fold(f, init) => (acc, e) => Seq(ScriptFunction2(f)(acc, e))
      case GroupBy(f, max) => throw new RuntimeException("Not implemented") //source.map(_.toOption.get).groupBy(max, e => ScriptFunction(f)(e)).to(PersistentSink[Json](chainDefToken))
      case Reduce(f) => (acc, e) => Seq(ScriptFunction2(f)(acc, e))
    }

  def processingLogic(state: ChainState, event: DurableEvent): (ChainState, Seq[FrameToken]) = {
    event.payload match {
      case ft: FrameToken => if (ft.payload.nonEmpty) toFrame(state, ft.payload.get.v) else {
        statusInternal = ChainStatus.Failed(s"Incorrect FrameToken payload format: ${ft.payloadJson}")
        (state, Seq[FrameToken]())
      }
    }
  }
}
