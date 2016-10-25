package com.avalanchain.cluster

import akka.actor.{Actor, ActorLogging}
import akka.cluster.pubsub.DistributedPubSub
import akka.cluster.pubsub.DistributedPubSubMediator.Subscribe
import akka.stream.actor.ActorPublisher
import com.avalanchain.cluster.ClusterService.InternalTransferFrame
import com.avalanchain.core.chain.{ChainDef, StateFrame}

import scala.collection.mutable

/**
  * Created by Yuriy Habarov on 06/05/2016.
  */
class ClusterNodeChainFrameReceiver[T](val chainDef: ChainDef) extends Actor with ActorLogging with ActorPublisher[StateFrame[T]] {
  // TODO: Add chainDef validation
  val chainTopic = "chains" + chainDef.hash.toString
  log.info(s"Subscribed to cluster chain {$chainTopic}")
  val mediator = DistributedPubSub(context.system).mediator
  mediator ! Subscribe(chainTopic, self)

  var queue: mutable.Queue[StateFrame[T]] = mutable.Queue()

  override def receive: Receive = {
    case v: InternalTransferFrame[T] =>
      queue.enqueue(v.frame)
      publishIfNeeded()
      //mediator ! Publish(topic, ChatClient.Message(name, msg))
  }

  def publishIfNeeded() = {
    while (queue.nonEmpty && isActive && totalDemand > 0) {
      onNext(queue.dequeue())
    }
  }
}
