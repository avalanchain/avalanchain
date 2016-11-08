package com.avalanchain.core.chainFlow

import java.util.UUID

import akka.actor.ActorLogging
import akka.persistence.{PersistentActor, SnapshotOffer}
import akka.stream.actor.ActorSubscriberMessage.OnNext
import akka.stream.actor.{ActorSubscriber, MaxInFlightRequestStrategy}
import com.avalanchain.core.chain.{ChainRef, FrameBuilder, MerkledRef, StateFrame}
import com.avalanchain.core.domain._

/**
  * Created by Yuriy Habarov on 29/04/2016.
  */
class ChainPersistentActor[T](val chainRef: ChainRef, initial: Option[T], val snapshotInterval: Int = 100, val maxInFlight: Int = 10)
                             (implicit hasherT: Hasher[T], hasherMR: Hasher[MerkledRef], bytes2Hexed: Bytes2Hexed)
  extends PersistentActor with ActorSubscriber with ActorLogging {

  override def persistenceId = bytes2Hexed(chainRef.hash.hash)

  private def saveFrame(frame: StateFrame[T]) = {
    updateState(frame)
    //context.system.eventStream.publish(frame)
    log.info(s"Saved ${frame}, inFlight: ${inFlight}")
  }

  private var state: StateFrame[T] = FrameBuilder.buildInitialFrame(chainRef, initial)
  //saveFrame(state)

  private def updateState(event: StateFrame[T]): Unit = {
    state = event
    log.info(s"State updated to $event, inFlight: ${inFlight}")
  }

  private var inFlight = 0

  def currentState = state

  val receiveRecover: Receive = {
    case evt: StateFrame[T] => updateState(evt)
    case SnapshotOffer(_, snapshot: StateFrame[T]) => state = snapshot
  }

  def save(data: HashedValue[T]) = {
    log.info(s"Received $data, inFlight: ${inFlight}")
    val frame = FrameBuilder.buildFrame[T](chainRef, currentState, data.value)
    inFlight += 1
    persist(frame) (e => {
      saveFrame(e)
      if (e.mref.value.pos != 0 && e.mref.value.pos % snapshotInterval == 0) {saveSnapshot(currentState)}
      inFlight -= 1
    })
  }

  val receiveCommand: Receive = {
    case OnNext(data: HashedValue[T]) =>
      save(data)

    case data: HashedValue[T] =>
      save(data)

//    case data: T =>
//      save(hasherT(data))


    //    case data: T =>
//      val newFrame = FrameBuilder.buildFrame[T](chainRef, currentState, data.value)
//      save(newFrame)
    case "print" => println(state)
    case a => println (s"Ignored '$a'")
  }

  override val requestStrategy = new MaxInFlightRequestStrategy(max = maxInFlight) {
    override def inFlightInternally: Int = inFlight
  }
}

