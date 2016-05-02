package com.avalanchain.core.chainFlow

import akka.persistence.{PersistentActor, SnapshotOffer}
import com.avalanchain.core.domain.{HashedValue, MerkledRef, StateFrame, _}

/**
  * Created by Yuriy Habarov on 29/04/2016.
  */
class ChainPersistentActor[T](node: CryptoContext, val chainRef: ChainRef, val snapshotInterval: Int, initial: T) extends PersistentActor {
  override def persistenceId = chainRef.hash.toString()

  private def merkledHasher = node.hasher[MerkledRef]

  private def buildInitialFrame: StateFrame[T] = {
    val hashed = node.hasher[T](initial)
    val mr = MerkledRef(chainRef.hash, "", 0, hashed.hash)
    StateFrame.InitialFrame[T](merkledHasher(mr), hashed).asInstanceOf[StateFrame[T]]
  }

  private def saveFrame(frame: StateFrame[T]) = {
    updateState(frame)
    //context.system.eventStream.publish(frame)
    println(s"saved ${frame}")
  }

  private var state: StateFrame[T] = buildInitialFrame
  //saveFrame(state)

  private def updateState(event: StateFrame[T]): Unit = {
    state = event
  }

  def currentState = state

  val receiveRecover: Receive = {
    case evt: StateFrame[T] => updateState(evt)
    case SnapshotOffer(_, snapshot: StateFrame[T]) => state = snapshot
  }

  val receiveCommand: Receive = {
    case data: HashedValue[T] =>
      val mr = MerkledRef(chainRef.hash, state.mref.hash, state.pos + 1, data.hash)
      val newFrame = StateFrame.Frame[T](merkledHasher(mr), data)
      persist(newFrame) (e => saveFrame(newFrame))
      if (mr.pos != 0 && mr.pos % snapshotInterval == 0) saveSnapshot(newFrame)
    //    case data: T =>
    //      val hashedData = node.hasher(data)
    //      val mr = MerkledRef(chainRef.hash, state.mref.hash, state.pos + 1, hashedData.hash)
    //      val newFrame = StateFrame.Frame[T](merkledHasher(mr), hashedData)
    //      persist(newFrame) (e => saveFrame(newFrame))
    //      if (mr.pos != 0 && mr.pos % snapshotInterval == 0) saveSnapshot(newFrame)
    case "print" => println(state)
    case a => println (s"Ignored '$a'")
  }
}

