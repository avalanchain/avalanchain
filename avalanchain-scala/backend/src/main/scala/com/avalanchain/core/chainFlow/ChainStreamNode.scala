package com.avalanchain.core.chainFlow

import akka.actor.{ActorRef, Props}
import akka.stream.actor.ActorSubscriberMessage.OnNext
import akka.stream.actor.{ActorSubscriber, ActorSubscriberMessage, MaxInFlightRequestStrategy}
import com.avalanchain.core.domain._

/**
  * Created by Yuriy on 29/04/2016.
  */
class ChainStreamNode[T](node: CryptoContext, val chainRef: ChainRef, val snapshotInterval: Int, initial: Option[T]) extends ActorSubscriber {
  import ActorSubscriberMessage._

  val MaxQueueSize = 1000
  val persistence = context.actorOf(Props(new ChainPersistentActor[T](node, chainRef, initial, snapshotInterval, MaxQueueSize)), name = "persistence")

  override val requestStrategy = new MaxInFlightRequestStrategy(max = MaxQueueSize) {
    override def inFlightInternally: Int = MaxQueueSize // TODO: fix this
  }

  def receive = {
    case OnNext(msg) =>
      persistence.forward(msg)
  }
}

