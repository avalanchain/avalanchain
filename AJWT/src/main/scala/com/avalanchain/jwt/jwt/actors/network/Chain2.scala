package com.avalanchain.jwt.jwt.actors.network

import java.security.{KeyPair, MessageDigest}
import java.util.UUID

import akka.actor.{ActorRef, ActorRefFactory, ActorSystem}
import akka.stream.Materializer
import akka.stream.scaladsl.{Flow, Sink, Source}
import com.avalanchain.jwt.utils._
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
import io.circe.parser._
import io.circe.syntax._
import io.circe.generic.auto._
import cats.implicits._

import scala.collection.immutable.Seq
import scala.util.Try
import java.util.Base64
import java.nio.charset.StandardCharsets

import akka.NotUsed

/**
  * Created by Yuriy Habarov on 26/11/2016.
  */

class LevelDBLogFactory(nodeId: NodeIdToken, implicit val actorSystem: ActorSystem) extends ChainLogFactory {
  private def sha(text: String) = {
    val md = MessageDigest.getInstance("SHA-256");
    md.update(text.getBytes("UTF-8"))
    Base64.getEncoder.encodeToString(md.digest)
  }

  private def createLog(id: String, tag: String = ""): ActorRef = {
    val idCoded = sha(id)
    val nodeCoded = sha(nodeId.sig + tag)
    val actorRef = actorSystem.actorOf(LeveldbEventLog.props(idCoded, nodeCoded))
    println(s"Log created: '$idCoded' for node '$nodeCoded'")
    actorRef
  }

  def chainLog(chainDefToken: ChainDefToken) = createLog(ChainRef(chainDefToken).sig)

  def commandLog(chainDefToken: ChainDefToken) = createLog(ChainRef(chainDefToken).sig, "COM")
}

abstract class Chain (
//  val nodeId: NodeIdToken,
  val chainDefToken: ChainDefToken,
  val keyPair: KeyPair,
  //val logFactory: ChainLogFactory,
  val commandLog: ActorRef,
  val eventLog: ActorRef,
  implicit val actorSystem: ActorSystem,
  implicit val materializer: Materializer) {

  if (chainDefToken.payload.isEmpty) throw new RuntimeException(s"Inconsistent ChainDefToken: '$chainDefToken'")
  val chainRef = ChainRef(chainDefToken)
  var statusInternal: ChainStatus = ChainStatus.Active
  def status = statusInternal

  protected def newId() = randomId

  protected val initialVal = parse("{}").getOrElse(Json.Null)
  protected val initialState = ChainState(chainDefToken, statusInternal, None, new FrameRef(chainRef.sig), -1, initialVal)
  def sourceDES = Source.fromGraph(DurableEventSource(eventLog))
  def sourceFrame = sourceDES.map(_.payload.asInstanceOf[FrameToken])
  def sourceJson = sourceFrame.map(_.payloadJson)
  def source[T <: JwtPayload](implicit decoder: Decoder[T]) = sourceFrame.map(e => {
    val v = e.payload.get.v.as[T]
    v.right.toOption.get
  }).mapMaterializedValue(_ => NotUsed)

  private var currentFrame: Option[FrameToken] = None
  private var currentValue: Option[Json] = None
  private var currentPosition: Position = -1
  def current = currentValue
  def pos = currentPosition

  protected def processCurrent = sourceFrame.runWith(Sink.foreach(frame => {
    currentFrame = Some(frame)
    println(s"Frame: $frame")
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
    (ChainState(chainDefToken, statusInternal, Some(token), FrameRef(token), newPos, v), Seq(token))
  }

  def processingLogic(state: ChainState, event: DurableEvent): (ChainState, Seq[FrameToken])

  def process() =
    Source.fromGraph(DurableEventSource(commandLog))
      .via(statefulProcessor(newId, eventLog)(initialState)(processingLogic))
      .runWith(Sink.ignore)

}
//object Chain {
//
//}

class NewChain(nodeId: NodeIdToken, chainDefToken: ChainDefToken, keyPair: KeyPair)
               (implicit actorSystem: ActorSystem, materializer: Materializer, logFactory: ChainLogFactory)
  extends Chain(chainDefToken, keyPair, logFactory.commandLog(chainDefToken), logFactory.chainLog(chainDefToken), actorSystem, materializer) {


  def sink = Flow[Cmd].map(DurableEvent(_)).via(DurableEventWriter(newId, commandLog)).to(Sink.ignore)

  override def processingLogic(state: ChainState, event: DurableEvent): (ChainState, Seq[FrameToken]) = {
    event.payload match {
      case cmd: Cmd => toFrame(state, cmd.v)
    }
  }
}

class DerivedChain(nodeId: NodeIdToken, chainDefToken: ChainDefToken, keyPair: KeyPair, derived: Derived, parentLogRef: ActorRef)
                   (implicit actorSystem: ActorSystem, materializer: Materializer, logFactory: ChainLogFactory)
  extends Chain(chainDefToken, keyPair, logFactory.commandLog(chainDefToken), parentLogRef, actorSystem, materializer) {

  val func: (Json, Json) => Seq[Json] =
    derived.cdf match {
      case Copy => (_, e) => Seq(e) // TODO: Deal with failures explicitly
      case ChainDerivationFunction.Map(f) => (_, e) => Seq(ScriptFunction(f)(e))
      case Filter(f) => (_, e) => if (ScriptPredicate(f)(e)) Seq(e) else Seq()
      //case Fold(f, init) => source.map(_.fold(init)((acc, e) => ScriptFunction2(f)(acc, e)).to(PersistentSink[Json](chainDefToken)))
      case Fold(f, init) => (acc, e) => Seq(ScriptFunction2(f)(acc, e))
      case GroupBy(f, max) => throw new RuntimeException("Not implemented") //source.map(_.toOption.get).groupBy(max, e => ScriptFunction(f)(e)).to(PersistentSink[Json](chainDefToken))
      case Reduce(f) => (acc, e) => Seq(ScriptFunction2(f)(acc, e))
    }

  override def processingLogic(state: ChainState, event: DurableEvent): (ChainState, Seq[FrameToken]) = {
    event.payload match {
      case jt: JwtToken =>
        val ft = jt.asInstanceOf[FrameToken]
        if (ft.payload.nonEmpty) {
          val ret = func(state.lastValue, ft.payload.get.v).map(v => toFrame(state, v)).unzip
          (ret._1.last, ret._2.flatten)
        }
        else {
          statusInternal = ChainStatus.Failed(s"Incorrect FrameToken payload format: ${ft.payloadJson}")
          (state, Seq[FrameToken]())
        }
      case _ =>
        println(s"Incorrect payload: ${event.payload}!")
        statusInternal = ChainStatus.Failed(s"Incorrect payload: ${event.payload}!")
        (state, Seq[FrameToken]())
    }
  }
}
