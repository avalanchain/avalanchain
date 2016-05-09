package com.avalanchain.cluster

import akka.actor.{Actor, ActorLogging, ActorRef, ActorSystem, Props}
import akka.cluster.ClusterEvent.{MemberEvent, MemberExited, _}
import akka.cluster.{Cluster, Member, MemberStatus}
import akka.stream.scaladsl.SourceQueue
import spray.json._
import DefaultJsonProtocol._
import akka.stream.actor.ActorPublisher
import akka.stream.actor.ActorPublisherMessage.Request

import scala.collection.mutable

/**
  * Created by Yuriy Habarov on 02/05/2016.
  */
class MembersMonitor extends Actor with ActorLogging with ActorPublisher[String] {

  val cluster = Cluster(context.system)
  var queue: mutable.Queue[String] = mutable.Queue()
  //val cluster = Cluster(system)

  //  object MemberUpProtocol extends DefaultJsonProtocol {
//    implicit val memberUpFormat = jsonFormat1(MemberUp)
//  }
//
//  object MemberExitedProtocol extends DefaultJsonProtocol {
//    implicit val memberExitedFormat = jsonFormat1(MemberExited)
//  }

  //  object MemberProtocol extends DefaultJsonProtocol {
//    implicit val colorFormat = jsonFormat1(Member)
//  }

//  object MemberProtocol extends DefaultJsonProtocol {
//    implicit object ColorJsonFormat extends RootJsonFormat[Member] {
//      def write(m: Member) =
//        JsObject(("address", JsString(m.address.toString)), ("status", JsString(m.status.toString)))
//        //JsArray(JsString(c.name), JsNumber(c.red), JsNumber(c.green), JsNumber(c.blue))
//
//      def read(value: JsValue) = value match {
//        //case JsArray(Vector(JsString(name), JsNumber(red), JsNumber(green), JsNumber(blue))) =>
//        //  new Color(name, red.toInt, green.toInt, blue.toInt)
//        case _ => deserializationError("Not implemented")
//      }
//    }
//  }

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
    case MemberRemoved(member, previousStatus) => handleRemoved(member, previousStatus)
    case MemberExited(member)                  => handleExit(member)
    case _: MemberEvent                        => // ignore
  }

  def handleMemberUp(event: MemberUp) {
    //out ! (Json.obj("state" -> JString("up")) ++ toJson(member).as[JsObject])
    //implicit val colorFormat = jsonFormat4(Color)
    queue.enqueue(event.toString)
    publishIfNeeded()
  }

  def handleUnreachable(member: UnreachableMember) {
    //out ! (Json.obj("state" -> "unreachable") ++ toJson(member).as[JsObject])
    //out.offer(member.toJson.toString)
    queue.enqueue(member.toString)
    publishIfNeeded()
  }

  def handleRemoved(member: Member, previousStatus: MemberStatus) {
    //out ! (Json.obj("state" -> "removed") ++ toJson(member).as[JsObject])
    //out.offer(member.toJson.toString)
    queue.enqueue(member.toString)
    publishIfNeeded()
  }

  def handleExit(member: Member) {
    //out ! (Json.obj("state" -> "exit") ++ toJson(member).as[JsObject])
    //out.offer(member.toJson.toString)
    queue.enqueue(member.toString)
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
