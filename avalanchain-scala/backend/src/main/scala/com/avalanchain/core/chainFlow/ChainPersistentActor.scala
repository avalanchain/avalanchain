package com.avalanchain.core.chainFlow

import java.util.UUID

import akka.persistence.{PersistentActor, SnapshotOffer}
import akka.stream.actor.{ActorSubscriber, MaxInFlightRequestStrategy}
import com.avalanchain.core.domain
import com.avalanchain.core.domain.ChainStream.Hash
import com.avalanchain.core.domain.{HashedValue, MerkledRef, StateFrame, _}

/**
  * Created by Yuriy Habarov on 29/04/2016.
  */
class ChainPersistentActor[T](node: CryptoContext, val chainRef: ChainRef, initial: Option[T], val snapshotInterval: Int, val maxInFlight: Int)
  extends PersistentActor with ActorSubscriber {
  override def persistenceId = chainRef.hash.toString()

  private def merkledHasher = node.hasher[MerkledRef]

  private def saveFrame(frame: StateFrame[T]) = {
    updateState(frame)
    //context.system.eventStream.publish(frame)
    println(s"saved ${frame}")
  }

  private var state: StateFrame[T] = FrameBuilder.buildInitialFrame(node, chainRef, initial)
  //saveFrame(state)

  private def updateState(event: StateFrame[T]): Unit = {
    state = event
  }

  private var inFlight = 0

  def currentState = state

  val receiveRecover: Receive = {
    case evt: StateFrame[T] => updateState(evt)
    case SnapshotOffer(_, snapshot: StateFrame[T]) => state = snapshot
  }

  val receiveCommand: Receive = {
    case data: HashedValue[T] =>
      val newFrame = FrameBuilder.buildFrame[T](node, chainRef, currentState, data.value)
      inFlight += 1
      persistAsync(newFrame) (e => {
        saveFrame(newFrame)
        if (newFrame.mref.value.pos != 0 && newFrame.mref.value.pos % snapshotInterval == 0) saveSnapshot(newFrame)
        inFlight -= 1
      })

    //    case data: T =>
    //      val hashedData = node.hasher(data)
    //      val mr = MerkledRef(chainRef.hash, state.mref.hash, state.pos + 1, hashedData.hash)
    //      val newFrame = StateFrame.Frame[T](merkledHasher(mr), hashedData)
    //      persist(newFrame) (e => saveFrame(newFrame))
    //      if (mr.pos != 0 && mr.pos % snapshotInterval == 0) saveSnapshot(newFrame)
    case "print" => println(state)
    case a => println (s"Ignored '$a'")
  }

  override val requestStrategy = new MaxInFlightRequestStrategy(max = maxInFlight) {
    override def inFlightInternally: Int = inFlight
  }
}

