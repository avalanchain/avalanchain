package com.avalanchain.cluster

import akka.actor.Actor.Receive
import akka.actor.{Actor, ActorLogging, Props}
import akka.cluster.pubsub.DistributedPubSub
import akka.cluster.pubsub.DistributedPubSubMediator.{Publish, Subscribe}
import akka.stream.actor.ActorSubscriberMessage.OnNext
import akka.stream.actor.{ActorPublisher, ActorSubscriber, MaxInFlightRequestStrategy, RequestStrategy}
import com.avalanchain.cluster.ClusterService.{InternalTransferFrame, InternalTransferValue}
import com.avalanchain.core.chainFlow.ChainPersistentActor
import com.avalanchain.core.domain.{HashedValue, _}

import scala.collection.mutable

/**
  * Created by Yuriy Habarov on 06/05/2016.
  */
class ClusterNodeChainSender[T](node: CryptoContext, val chainDef: ChainDef, val maxQueue: Int = 100) extends Actor with ActorLogging with ActorSubscriber {
  val chainTopic = "chains" + chainDef.hash.toString
  log.info(s"Subscribed to cluster chain {$chainTopic}")
  val mediator = DistributedPubSub(context.system).mediator
  //mediator ! Subscribe(chainTopic, self)

  var queue: mutable.Queue[HashedValue[T]] = mutable.Queue()


  override def receive: Receive = {
    case OnNext(hashedValue: HashedValue[T]) =>
      mediator ! Publish(chainTopic, InternalTransferValue(hashedValue))
    case OnNext(frame: StateFrame[T]) =>
      mediator ! Publish(chainTopic, InternalTransferFrame(frame))
  }

  override val requestStrategy = new MaxInFlightRequestStrategy(max = maxQueue) {
    override def inFlightInternally: Int = 1 //queue.size
  }
}
