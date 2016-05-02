package com.avalanchain.persistence

import akka.persistence.{PersistentActor, SnapshotOffer}

import scala.util.{Success, Try}

/**
  * Created by Yuriy Habarov on 02/05/2016.
  */
case class KVCommand[T](id: String, value: T)
case class KVEvent[T](cmd: KVCommand[T])

class SimpleKeyValue[T](realm: String, id: String, commandChecker: KVCommand[T] => Try[Unit] = cmd => Success(Unit), snapshotInterval: Int = 10)
  extends PersistentActor {
  override def persistenceId = realm + "##" + id
  type StateType = Option[KVEvent[T]]

  var state: StateType = None
  var pos = -1

  def updateState(event: KVEvent[T]): Unit =
    state = Some(event)
    pos += 1
    if (pos >= snapshotInterval) {
      saveSnapshot(state)
      pos = 0
    }


  val receiveRecover: Receive = {
    case event: KVEvent[T]                     => updateState(event)
    case SnapshotOffer(_, snapshot: StateType) =>
      state = snapshot
      pos = 0
  }

  val receiveCommand: Receive = {
    case cmd: KVCommand[T] =>
      val checked = commandChecker(cmd)
      checked match {
        case Success(()) =>
          persistAsync(KVEvent(cmd))(updateState)
      }
      sender() ! checked

    case "print" => println(state)
    case "id" => sender() ! persistenceId
  }
}
