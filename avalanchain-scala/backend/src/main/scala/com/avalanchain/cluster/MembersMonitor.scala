package com.avalanchain.cluster

import akka.actor.{Actor, ActorLogging, ActorRef, ActorSystem, Address, Props}
import akka.cluster._
import akka.stream.scaladsl.SourceQueue
import akka.stream.actor.ActorPublisher
import akka.stream.actor.ActorPublisherMessage.Request

import scala.collection.mutable
import spray.json._
import fommil.sjs.FamilyFormats._ // NOTE: this is spray.json.shapeless. Do not remove.

object ClusterMemberViewModels {
  sealed trait ClusterViewModel
  final case class MemberUp(address: Address) extends ClusterViewModel
  final case class MemberDown(address: Address) extends ClusterViewModel
  final case class MemberJoined(address: Address) extends ClusterViewModel
  final case class MemberLeft(address: Address) extends ClusterViewModel
  final case class MemberExited(address: Address) extends ClusterViewModel
  final case class MemberRemoved(address: Address, previousStatus: MemberStatus) extends ClusterViewModel
  final case class UnreachableMember(address: Address) extends ClusterViewModel
  //  final case class MemberWeaklyUp(member: Member) extends ViewModel
  //  final case class LeaderChanged(leader: Option[Address]) extends ViewModel
  //  final case class RoleLeaderChanged(role: String, leader: Option[Address]) extends ViewModel
  //  final case object ClusterShuttingDown extends ViewModel
  //  final case class ReachableMember(member: Member) extends ViewModel
}

/**
  * Created by Yuriy Habarov on 02/05/2016.
  */
import ClusterMemberViewModels._

class MembersMonitor extends Actor with ActorLogging with ActorPublisher[String] {

  val cluster = Cluster(context.system)
  var queue: mutable.Queue[String] = mutable.Queue()
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

  def enqueue(event: ClusterViewModel) {
    queue.enqueue(event.toJson.toString)
    publishIfNeeded()
  }

  def handle(event: ClusterEvent.ClusterDomainEvent) {
    val (model: Option[ClusterViewModel]) = event match {
      case e: ClusterEvent.MemberUp                       => Some(MemberUp(e.member.address))
      case e: ClusterEvent.UnreachableMember              => Some(UnreachableMember(e.member.address))
      case e: ClusterEvent.MemberRemoved                  => Some(MemberRemoved(e.member.address, e.previousStatus))
      case e: ClusterEvent.MemberExited                   => Some(MemberExited(e.member.address))
      case e: ClusterEvent.MemberJoined                   => Some(MemberJoined(e.member.address))
      case e: ClusterEvent.MemberLeft                     => Some(MemberLeft(e.member.address))
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


object MembersMonitor {

  /**
    * Definition for the controller to create the websocket
    */
  def props = Props[MembersMonitor]
}
