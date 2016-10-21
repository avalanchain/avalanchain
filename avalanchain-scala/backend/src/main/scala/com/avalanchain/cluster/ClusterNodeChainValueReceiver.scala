package com.avalanchain.cluster

import scala.reflect.runtime.universe._
import akka.actor.{Actor, ActorLogging}
import akka.actor.Actor.Receive
import akka.cluster.pubsub.DistributedPubSub
import akka.cluster.pubsub.DistributedPubSubMediator.{Publish, Subscribe}
import akka.stream.actor.ActorPublisher
import com.avalanchain.cluster.ClusterService.InternalTransferValue
//import com.avalanchain.core.domain.{ChainDef, HashedValue}

import scala.collection.mutable

/**
  * Created by Yuriy Habarov on 06/05/2016.
  */
//class ClusterNodeChainValueReceiver[T](val chainDef: ChainDef) extends Actor with ActorLogging with ActorPublisher[HashedValue[T]] {
//  // TODO: Add chainDef validation
//  val chainTopic = "chains" + chainDef.hash.toString
//  log.info(s"Subscribed to cluster chain {$chainTopic}")
//  val mediator = DistributedPubSub(context.system).mediator
//  mediator ! Subscribe(chainTopic, self)
//
//  var queue: mutable.Queue[HashedValue[T]] = mutable.Queue()
//
//  override def receive: Receive = {
//    case v: InternalTransferValue[T] =>
//      queue.enqueue(v.hashedValue)
//      publishIfNeeded()
//      //mediator ! Publish(topic, ChatClient.Message(name, msg))
//  }
//
//  def publishIfNeeded() = {
//    while (queue.nonEmpty && isActive && totalDemand > 0) {
//      onNext(queue.dequeue())
//    }
//  }
//}
