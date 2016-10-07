package com.avalanchain.cluster

import akka.actor.{Actor, ActorLogging, ActorRef, ActorSystem, Address, Props}
import akka.cluster._
import akka.stream.scaladsl.SourceQueue
import akka.stream.actor.ActorPublisher
import akka.stream.actor.ActorPublisherMessage.Request

import scala.collection.mutable
import spray.json._
import fommil.sjs.FamilyFormats._ // this is spray.json.shapeless

object ClusterMemberViewModels {
  sealed trait ViewModel
  //  final case class MemberWeaklyUp(member: Member) extends ViewModel
  case class MemberUp(address: Address) extends ViewModel
  case class MemberDown(address: Address) extends ViewModel
  //  final case class MemberJoined(member: Member) extends ViewModel
  //  final case class MemberLeft(member: Member) extends ViewModel
  case class MemberExited(address: Address) extends ViewModel
  case class MemberRemoved(address: Address, previousStatus: MemberStatus) extends ViewModel
  //  final case class LeaderChanged(leader: Option[Address]) extends ViewModel
  //  final case class RoleLeaderChanged(role: String, leader: Option[Address]) extends ViewModel
  //  final case object ClusterShuttingDown extends ViewModel
  case class UnreachableMember(address: Address) extends ViewModel
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
    case Request(cnt)                          => publishIfNeeded()

    case event: ClusterEvent.MemberUp                       => handle(MemberUp(event.member.address))
    case event: ClusterEvent.UnreachableMember              => handle(UnreachableMember(event.member.address))
    case event: ClusterEvent.MemberRemoved                  => handle(MemberRemoved(event.member.address, event.previousStatus))
    case event: ClusterEvent.MemberExited                   => handle(MemberExited(event.member.address))
    case _: ClusterEvent.MemberEvent                        => // ignore
  }

  def handle(event: ViewModel) {
    queue.enqueue(event.toJson.toString)
    publishIfNeeded()
  }

  // NOTE: Cannot use a generic handle() because of spray.json.shapeless
  // TODO: Find a way to serialize it with less boilerplate
//  def handleUnreachable(event: UnreachableMember) {
//    val e = UnreachableMemberVM(event.member.address)
//    queue.enqueue((e: ViewModel).toJson.toString)
//    publishIfNeeded()
//  }
//
//  def handleMemberUp(event: MemberUp) {
//    val e = MemberUpVM(event.member.address)
//    queue.enqueue((e: ViewModel).toJson.toString)
//    publishIfNeeded()
//  }
//
//  def handleRemoved(event: MemberRemoved) {
//    val e = MemberRemovedVM(event.member.address, event.previousStatus)
//    queue.enqueue((e: ViewModel).toJson.toString)
//    publishIfNeeded()
//  }
//
//  def handleExit(event: MemberExited) {
//    val e = MemberExitedVM(event.member.address)
//    queue.enqueue((e: ViewModel).toJson.toString)
//    publishIfNeeded()
//  }
//
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
