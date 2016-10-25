package com.avalanchain.core.chainFlow

import akka.actor.{ActorRef, Props}
import akka.stream.actor.{ActorSubscriber, ActorSubscriberMessage, MaxInFlightRequestStrategy}
import com.avalanchain.core.chain.{ChainRef, MerkledRef}
import com.avalanchain.core.domain.{HashedValue, _}

/**
  * Created by Yuriy on 29/04/2016.
  */
class ChainGroupByNode[T](val chainRef: ChainRef, keySelector: T => String, initial: Option[T], val snapshotInterval: Int, maxInFlight: Int)
                         (implicit hasherT: Hasher[T], hasherMR: Hasher[MerkledRef])
  extends ActorSubscriber {
  import ActorSubscriberMessage._

  val MaxQueueSize = 10
  var queue = Map.empty[Int, ActorRef]
  def getChild(name: String) = {
    context.child(name) match {
      case Some(ch) => ch
      case None => context.actorOf(Props(new ChainPersistentActor[T](chainRef, initial, snapshotInterval, maxInFlight)), name)
    }
  }

  override val requestStrategy = new MaxInFlightRequestStrategy(max = MaxQueueSize) {
    override def inFlightInternally: Int = queue.size
  }

  def receive = {
    case OnNext(msg: HashedValue[T]) =>
      getChild(keySelector(msg.value)).forward(msg)
  }
}

