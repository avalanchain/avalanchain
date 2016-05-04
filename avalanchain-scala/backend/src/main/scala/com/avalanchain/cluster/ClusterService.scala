package com.avalanchain.cluster

import java.util.UUID

import akka.actor.{Actor, ActorRef, ActorSystem, Props}
import akka.stream.scaladsl.SourceQueue
import com.typesafe.config.ConfigFactory

import scala.collection.JavaConverters._

/**
  * Created by Yuriy Habarov on 04/05/2016.
  */
object ClusterService {
  val defaultGroup = "_DEFAULT_"

  def startOn(props: Props, name: String)(implicit system: ActorSystem): ActorRef = {
    system.actorOf(props, name)
  }

  def startMonitor(implicit system: ActorSystem): ActorRef = {
    startOn (MembersMonitor.props, "monitor" + (UUID.randomUUID().toString.replace("-", "")))(system)
  }

  def deployNode (port: Int, groupNames: String = defaultGroup) : ActorSystem = {
    // System initialization
    val properties = Map(
      "akka.remote.netty.tcp.port" -> port.toString
    ).asJava

    val system = ActorSystem("avalanchain", (ConfigFactory parseMap properties)
      .withFallback(ConfigFactory parseResources "avalanchain.conf")
      .withFallback(ConfigFactory.parseString(s"akka.cluster.roles = [$groupNames]"))
      .withFallback(ConfigFactory.load())
    )

    // Deploy actors and services
    startOn (Props[PrintActor], "print")(system)
    startOn (Props[EchoActor], "echo")(system)

    system
  }

  class PrintActor() extends Actor {
    def receive = {
      case a => println(a)
    }
  }

  class EchoActor() extends Actor {
    def receive = {
      case a: String => sender() ! a
    }
  }

  val firstNode = deployNode(2551)
}


