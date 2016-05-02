package com.avalanchain.core.chainFlow

import akka.actor.{ActorRef, Props}
import akka.stream.actor.ActorSubscriberMessage.OnNext
import akka.stream.actor.{ActorSubscriber, ActorSubscriberMessage, MaxInFlightRequestStrategy}
import com.avalanchain.core.domain._

/**
  * Created by Yuriy on 29/04/2016.
  */
class ChainStreamNode[T](node: CryptoContext, val chainRef: ChainRef, val snapshotInterval: Int, initial: T) extends ActorSubscriber {
  import ActorSubscriberMessage._

  val MaxQueueSize = 10
  var queue = Map.empty[Int, ActorRef]
  val persistence = context.actorOf(Props(new ChainPersistentActor[T](node, chainRef, snapshotInterval, initial)), name = "persistence")

  override val requestStrategy = new MaxInFlightRequestStrategy(max = MaxQueueSize) {
    override def inFlightInternally: Int = queue.size
  }

  def receive = {
    case OnNext(msg) =>
      persistence.forward(msg)
  }
}

