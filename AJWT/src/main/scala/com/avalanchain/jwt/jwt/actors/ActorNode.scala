package com.avalanchain.jwt.jwt.actors

import java.net.InetAddress

import akka.actor.ActorDSL._
import akka.actor.{ActorContext, ActorLogging, ActorRef, ActorRefFactory, ActorSystem, ExtendedActorSystem, Props}
import akka.pattern.{ask, pipe}
import akka.stream.ActorMaterializer
import akka.util.Timeout
import com.typesafe.config.ConfigFactory

import scala.concurrent.{ExecutionContext, Future}
import scala.concurrent._
import scala.concurrent.duration._
import scala.concurrent.ExecutionContext.Implicits.global

/**
  * Created by Yuriy Habarov on 25/11/2016.
  */
trait ActorNode {
  val SystemName: String = "avalanchain"

  private val myAddress = system.asInstanceOf[ExtendedActorSystem].provider.rootPath.address
  val localhost = InetAddress.getLocalHost.getHostAddress

  val port: Int

  implicit val system = ActorSystem(SystemName,
    ConfigFactory.parseString(s"akka.remote.netty.tcp.host = ${localhost}")
      .withFallback(ConfigFactory.parseString(s"akka.remote.netty.tcp.port = $port"))
      .withFallback(ConfigFactory.load("node.conf")))
  implicit val materializer = ActorMaterializer()(system)
  implicit val executor: ExecutionContext = system.dispatcher
  implicit val timeout = Timeout(5 seconds)

  case object GetNodePort

  private val addr = actor("addr")(new Act {
    become {
      case GetNodePort => sender() ! system.asInstanceOf[ExtendedActorSystem].provider.getDefaultAddress.port
    }
  })

  def localport(): Future[Int] = (addr ? GetNodePort).map(_.asInstanceOf[Option[Int]].get)


}
