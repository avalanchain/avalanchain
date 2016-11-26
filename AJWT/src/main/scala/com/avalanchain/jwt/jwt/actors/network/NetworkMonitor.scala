package com.avalanchain.jwt.jwt.actors.network

/**
  * Created by Yuriy Habarov on 02/05/2016.
  */

import akka.actor.{Actor, ActorLogging, ActorRef, ActorSystem, Address, Props}
import akka.cluster._
import akka.stream.scaladsl.SourceQueue
import akka.stream.actor.ActorPublisher
import akka.stream.actor.ActorPublisherMessage.Request

import io.circe.{Decoder, DecodingFailure, Encoder, Json}
import io.circe.syntax._
import io.circe.parser._
import io.circe.generic.JsonCodec
import io.circe.generic.auto._

import scala.collection.mutable

sealed trait NodeStatus { val address: NodeStatus.Address }
object NodeStatus {
  final case class Address private (host: String, port: Int, httpPort: Int)
  object Address {
    def apply(addr: akka.actor.Address): Address = new Address(addr.host.get, addr.port.get, addr.port.get + 1000)
  }

  final case class NodeUp(address: Address) extends NodeStatus
  final case class NodeDown(address: Address) extends NodeStatus
  final case class NodeJoined(address: Address) extends NodeStatus
  final case class NodeLeft(address: Address) extends NodeStatus
  final case class NodeExited(address: Address) extends NodeStatus
  final case class NodeRemoved(address: Address) extends NodeStatus
  final case class NodeUnreachable(address: Address) extends NodeStatus

  import io.circe.Decoder, io.circe.generic.semiauto._
  implicit val encoderAddress: Encoder[Address] = deriveEncoder
  implicit val decoderAddress: Decoder[Address] = deriveDecoder
  implicit val encoderNodeStatus: Encoder[NodeStatus] = deriveEncoder
  implicit val decoderNodeStatus: Decoder[NodeStatus] = deriveDecoder
}

import NodeStatus._

class NetworkMonitor extends Actor with ActorLogging with ActorPublisher[NodeStatus] {

  val cluster = Cluster(context.system)
  var queue: mutable.Queue[NodeStatus] = mutable.Queue()
  //val cluster = Cluster(system)

  // subscribe to cluster changes, re-subscribe when restart
  override def preStart(): Unit = {
    cluster.subscribe(self, initialStateMode = ClusterEvent.InitialStateAsEvents,
      classOf[ClusterEvent.MemberEvent], classOf[ClusterEvent.UnreachableMember])
  }

  // clean up on shutdown
  override def postStop(): Unit = cluster unsubscribe self

  // handle the member events
  def receive = {
    case Request(cnt)                             => publishIfNeeded()
    case event: ClusterEvent.ClusterDomainEvent   => handle(event)
  }

  def enqueue(event: NodeStatus) {
    queue.enqueue(event)
    publishIfNeeded()
  }

  def handle(event: ClusterEvent.ClusterDomainEvent) {
    val (model: Option[NodeStatus]) = event match {
      case e: ClusterEvent.MemberUp                       => Some(NodeUp(NodeStatus.Address(e.member.address)))
      case e: ClusterEvent.UnreachableMember              => Some(NodeUnreachable(NodeStatus.Address(e.member.address)))
      case e: ClusterEvent.MemberRemoved                  => Some(NodeRemoved(NodeStatus.Address(e.member.address)))
      case e: ClusterEvent.MemberExited                   => Some(NodeExited(NodeStatus.Address(e.member.address)))
      case e: ClusterEvent.MemberJoined                   => Some(NodeJoined(NodeStatus.Address(e.member.address)))
      case e: ClusterEvent.MemberLeft                     => Some(NodeLeft(NodeStatus.Address(e.member.address)))
      case _                                              => None
    }

    if (model.isDefined) enqueue (model.get)
  }

  def publishIfNeeded() = {
    while (queue.nonEmpty && isActive && totalDemand > 0) {
      onNext(queue.dequeue())
    }
  }
}


object NetworkMonitor {

  /**
    * Definition for the controller to create the websocket
    */
  def props = Props[NetworkMonitor]
}

