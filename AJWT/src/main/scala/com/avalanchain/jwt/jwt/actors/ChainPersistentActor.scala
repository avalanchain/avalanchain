package com.avalanchain.jwt.jwt.actors

import akka.actor.{ActorLogging, Props}
import akka.persistence.{PersistentActor, SnapshotOffer}
import com.avalanchain.jwt.basicChain.JwtAlgo.{ES512, HS512}
import com.avalanchain.jwt.basicChain.{FAsym, FSym, _}
import com.avalanchain.jwt.jwt.NodeContext
import io.circe.{Decoder, DecodingFailure, Encoder, Json}
import io.circe.syntax._
import io.circe.parser._
import io.circe.generic.JsonCodec
import io.circe.generic.auto._

/**
  * Created by Yuriy Habarov on 21/05/2016.
  */
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
