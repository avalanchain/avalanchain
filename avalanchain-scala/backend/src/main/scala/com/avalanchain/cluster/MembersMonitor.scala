package com.avalanchain.cluster

import akka.actor.{Actor, ActorLogging, ActorRef, ActorSystem, Address, Props}
import akka.cluster.ClusterEvent.{MemberEvent, MemberExited, _}
import akka.cluster.{Cluster, Member, MemberStatus, UniqueAddress}
import akka.stream.scaladsl.SourceQueue
import spray.json._
import DefaultJsonProtocol._
import akka.stream.actor.ActorPublisher
import akka.stream.actor.ActorPublisherMessage.Request

import scala.collection.mutable
import scala.pickling._
import scala.pickling.json._    // Imports PickleFormat
//import scala.pickling.static._  // Avoid runtime pickler

import scala.pickling.Defaults.{ pickleOps, unpickleOps }
import scala.pickling.Defaults.{ stringPickler, intPickler, refUnpickler, nullPickler }

/**
  * Created by Yuriy Habarov on 02/05/2016.
  */
class MembersMonitor extends Actor with ActorLogging with ActorPublisher[String] {

  val cluster = Cluster(context.system)
  var queue: mutable.Queue[String] = mutable.Queue()
  //val cluster = Cluster(system)

  implicit val nonePickler = Pickler.generate[None.type]
  implicit val someStringPickler = Pickler.generate[Some[String]]
  implicit val someIntPickler = Pickler.generate[Some[Int]]
  implicit val optionStringPickler = Pickler.generate[Option[String]]
  implicit val optionSetPickler = Pickler.generate[Set[String]]
  implicit val optionIntPickler = Pickler.generate[Option[Int]]
  implicit val addressPickler = Pickler.generate[Address]
  implicit val uniqueAddressPickler = Pickler.generate[UniqueAddress]
  implicit val memberStatusUpPickler = Pickler.generate[MemberStatus.Up.type]
  implicit val memberStatusDownPickler = Pickler.generate[MemberStatus.Down.type]
  implicit val memberStatusExitingPickler = Pickler.generate[MemberStatus.Exiting.type]
  implicit val memberStatusLeavingPickler = Pickler.generate[MemberStatus.Leaving.type]
  implicit val memberStatusWeaklyUpPickler = Pickler.generate[MemberStatus.WeaklyUp.type]
  implicit val memberStatusRemovedPickler = Pickler.generate[MemberStatus.Removed.type]
  implicit val memberStatusJoiningPickler = Pickler.generate[MemberStatus.Joining.type]
  implicit val memberStatusPickler = Pickler.generate[MemberStatus]
  implicit val memberPickler = Pickler.generate[Member]
  implicit val memberUpPickler = Pickler.generate[MemberUp]
  implicit val memberExitedPickler = Pickler.generate[MemberExited]
  implicit val memberRemovedPickler = Pickler.generate[MemberRemoved]
  implicit val unreachableMemberPickler = Pickler.generate[UnreachableMember]


  // subscribe to cluster changes, re-subscribe when restart
  override def preStart(): Unit = {
    cluster.subscribe(self, initialStateMode = InitialStateAsEvents,
      classOf[MemberEvent], classOf[UnreachableMember])
  }

  // clean up on shutdown
  override def postStop(): Unit = cluster unsubscribe self

  // handle the member events
  def receive = {
    case Request(cnt)                          => publishIfNeeded()

    case event: MemberUp                       => handleMemberUp(event)
    case event: UnreachableMember              => handleUnreachable(event)
    case event: MemberRemoved                  => handleRemoved(event)
    case event: MemberExited                   => handleExit(event)
    case _: MemberEvent                        => // ignore
  }

  def handleMemberUp(event: MemberUp) {
    queue.enqueue(event.pickle.value)
    publishIfNeeded()
  }

  def handleUnreachable(event: UnreachableMember) {
    queue.enqueue(event.pickle.value)
    publishIfNeeded()
  }

  def handleRemoved(event: MemberRemoved) {
    queue.enqueue(event.pickle.value)
    publishIfNeeded()
  }

  def handleExit(event: MemberExited) {
    queue.enqueue(event.pickle.value)
    publishIfNeeded()
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
