

import akka.actor._
import akka.cluster.pubsub.{DistributedPubSub, DistributedPubSubMediator}
import akka.cluster.routing.{ClusterRouterGroup, ClusterRouterGroupSettings, ClusterRouterPool, ClusterRouterPoolSettings}
import akka.routing.{BroadcastGroup, BroadcastPool}
import com.typesafe.config.ConfigFactory

import scala.collection.JavaConversions._

val defaultGroup = "_DEFAULT_"

class Destination extends Actor with ActorLogging {
  import DistributedPubSubMediator.Put
  val mediator = DistributedPubSub(context.system).mediator
  // register to the path
  mediator ! Put(self)

  def receive = {
    case s: String =>
      log.info("Got '{}'", s)
  }
}

def deployNode (sys: String, port: String, props: Props, groupNames: String = defaultGroup) : ActorSystem = {
  // System initialization
  val properties = Map(
    "akka.remote.netty.tcp.port" -> port
  )

  val system = ActorSystem(sys, (ConfigFactory parseMap properties)
    .withFallback(ConfigFactory parseResources "akka.conf")
    .withFallback(ConfigFactory.parseString(s"akka.cluster.roles = [$groupNames]"))
    .withFallback(ConfigFactory.load())
  )

  def startOn(system: ActorSystem) {
    system.actorOf(props, name = "print")
  }

  // Deploy actors and services
  startOn (system)

  system
}



class PrintActor() extends Actor {

  def receive = {
    case a => println(a)
  }
}

//val cs1 = deployNode ("application", "2551", Props[Destination], "")
//val cs2 = deployNode ("application", "2552", Props[Destination], "")
val cs1 = deployNode ("application", "2551", Props[Destination], "_AA_")
val cs2 = deployNode ("application", "2552", Props[Destination], "_AA_")
//
//for( a <- 1 until 100){
//  deployNode ("0", Props[PrintActor], "_AA_")
//}


val workerRouter = cs1.actorOf(
  ClusterRouterPool(BroadcastPool(0), ClusterRouterPoolSettings(
    totalInstances = 100000000, maxInstancesPerNode = 5,
    allowLocalRoutees = true, useRole = Some("_AA_"))).props(Props[Destination]),
  name = "dest")

val workerRouter = cs2.actorOf(
  ClusterRouterPool(BroadcastPool(10), ClusterRouterPoolSettings(
    totalInstances = 10, maxInstancesPerNode = 5,
    allowLocalRoutees = true, useRole = Some("_AA_"))).props(Props[Destination]),
  name = "dest8")

val workerRouter = cs2.actorOf(ClusterRouterGroup(BroadcastGroup(List("/user/print")),
  ClusterRouterGroupSettings(
    totalInstances = 10, routeesPaths = List("/user/print"),
    allowLocalRoutees = true, useRole = None)).props(), name = "dest8")


val path = cs1.actorSelection("/user/dest") ! "tuk"

val path = cs1.actorSelection("/user/print") ! "tuk"
val path = cs2.actorSelection("/user/print") ! "tuk"

val path = cs2.actorSelection("/user/dest8") ! "tuk"

val path = cs1.actorSelection("/application/user/dest") ! "tuk"

val path = cs1.actorSelection("/dest") ! "tuk"

val print = cs1.actorOf(Props[PrintActor], name = "print")
val print2 = cs1.actorOf(Props[Destination], name = "print2")

val path = cs1.actorSelection("/user/print") ! "tuk"
val path = cs1.actorSelection("/user/print2") ! "tuk"

val router18=
  cs1.actorOf(BroadcastPool(10).props(Props[PrintActor]), "router18")

val path = cs1.actorSelection("/user/router18") ! "tuk"



/**
  * Booting a cluster backend node with all actors
  */
object ResourceGroupNode extends App {
  // Simple cli parsing
  val (port, groupNames) = args match {
    case Array()     => ("0", defaultGroup)
    case Array(port) => (port, defaultGroup)
    case Array(port, groupNames) => (port, groupNames)
    case args        => throw new IllegalArgumentException(s"only ports and group names. Args [ $args ] are invalid")
  }

  deployNode (port, Props[Actor], groupNames)
}
