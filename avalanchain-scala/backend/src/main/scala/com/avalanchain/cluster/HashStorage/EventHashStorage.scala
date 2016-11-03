package com.avalanchain.cluster.HashStorage

import akka._
import akka.actor._
import akka.cluster.sharding.ShardRegion
import akka.persistence.{PersistentActor, RecoveryCompleted, SnapshotOffer, Update}
import com.avalanchain.core.domain.{Hashed, HashedValue}

import scala.concurrent.ExecutionContext.Implicits.global
import scala.concurrent.duration._


/**
  * Created by Yuriy Habarov on 03/11/2016.
  */
class EventHashStorage(val idPrefix: String) extends PersistentActor with ActorLogging {
  import ShardRegion.Passivate

  context.setReceiveTimeout(120.seconds)

  object Stop
  case class SaverInfo(ref: ActorRef) // TODO: Add at least public key
  case class HashEvt(hashed: Hashed, saver: SaverInfo)

  // self.path.name is the entity identifier (utf-8 URL-encoded)
  override def persistenceId: String = s"$idPrefix-" + self.path.name

  var count = 0

//  def updateState(event: CounterChanged): Unit =
//    count += event.delta
//
  override def receiveRecover: Receive = {
//    case evt: CounterChanged ⇒ updateState(evt)
    case _ => ???
  }

  override def receiveCommand: Receive = {
//    case Increment      ⇒ persist(CounterChanged(+1))(updateState)
//    case Decrement      ⇒ persist(CounterChanged(-1))(updateState)
//    case Get(_)         ⇒ sender() ! count
    case ReceiveTimeout ⇒ context.parent ! Passivate(stopMessage = Stop)
//    case Stop           ⇒ context.stop(self)
  }
}
