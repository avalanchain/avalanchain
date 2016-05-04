

import akka.actor._
import akka.actor.ActorDSL._
import akka.cluster.{Cluster, Member, UniqueAddress}
import akka.cluster.ClusterEvent._
import akka.cluster.pubsub.{DistributedPubSub, DistributedPubSubMediator}
import akka.cluster.routing.{ClusterRouterGroup, ClusterRouterGroupSettings, ClusterRouterPool, ClusterRouterPoolSettings}
import akka.cluster.singleton.{ClusterSingletonManager, ClusterSingletonManagerSettings}
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

def deployNode (port: Int, props: Props, groupNames: String = defaultGroup) : ActorSystem = {
  // System initialization
  val properties = Map(
    "akka.remote.netty.tcp.port" -> port.toString
  )

  val system = ActorSystem("application", (ConfigFactory parseMap properties)
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


val cs1 = deployNode (2551, Props[Destination], "_AA_")
val cs2 = deployNode (2552, Props[Destination], "_AA_")
//
//for( a <- 1 until 100){
//  deployNode ("0", Props[PrintActor], "_AA_")
//}


val workerRouter1 = cs1.actorOf(
  ClusterRouterPool(BroadcastPool(0), ClusterRouterPoolSettings(
    totalInstances = 100000000, maxInstancesPerNode = 5,
    allowLocalRoutees = true, useRole = Some("_AA_"))).props(Props[Destination]),
  name = "dest")

val workerRouter2 = cs2.actorOf(
  ClusterRouterPool(BroadcastPool(10), ClusterRouterPoolSettings(
    totalInstances = 10, maxInstancesPerNode = 5,
    allowLocalRoutees = true, useRole = Some("_AA_"))).props(Props[Destination]),
  name = "dest8")

val workerRouter3 = cs2.actorOf(ClusterRouterGroup(BroadcastGroup(List("/user/print")),
  ClusterRouterGroupSettings(
    totalInstances = 10, routeesPaths = List("/user/print"),
    allowLocalRoutees = true, useRole = None)).props(), name = "dest8")


val path = cs1.actorSelection("/user/dest") ! "tuk"

val path1 = cs1.actorSelection("/user/print") ! "tuk"
val path2 = cs2.actorSelection("/user/print") ! "tuk"

val path3 = cs2.actorSelection("/user/dest8") ! "tuk"

val path4 = cs1.actorSelection("/application/user/dest") ! "tuk"

val path5 = cs1.actorSelection("/dest") ! "tuk"

val print = cs1.actorOf(Props[PrintActor], name = "print")
val print2 = cs1.actorOf(Props[Destination], name = "print2")

val path6 = cs1.actorSelection("/user/print") ! "tuk"
val path7 = cs1.actorSelection("/user/print2") ! "tuk"

val router18=
  cs1.actorOf(BroadcastPool(10).props(Props[PrintActor]), "router18")

val path8 = cs1.actorSelection("/user/router18") ! "tuk"



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

class SimpleClusterListener extends Actor with ActorLogging {

  var members: Map[Address, Member] = Map()

  val cluster = Cluster(context.system)

  // subscribe to cluster changes, re-subscribe when restart
  override def preStart(): Unit = {
    //#subscribe
    cluster.subscribe(self, initialStateMode = InitialStateAsEvents,
      classOf[MemberEvent], classOf[UnreachableMember])
    //#subscribe
  }
  override def postStop(): Unit = cluster.unsubscribe(self)

  def receive = {
    case MemberUp(member) =>
      members = members + (member.address -> member)
      log.info("Member is Up: {}", member.address)
      log.info("Members: {}", members)
    case ReachableMember(member) =>
      members = members + (member.address -> member)
      log.info("Member detected reachable again: {}", member)
      log.info("Members: {}", members)
    case UnreachableMember(member) =>
      members = members - (member.address)
      log.info("Member detected as unreachable: {}", member)
      log.info("Members: {}", members)
    case MemberRemoved(member, previousStatus) =>
      members = members - (member.address)
      log.info("Member is Removed: {} after {}", member.address, previousStatus)
      log.info("Members: {}", members)
    case e: MemberEvent =>
      log.info("Member Event ignored: {}", e)
  }
}

val single = cs1.actorOf(ClusterSingletonManager.props(
  singletonProps = Props(classOf[SimpleClusterListener]),
  terminationMessage = PoisonPill,
  settings = ClusterSingletonManagerSettings(cs1)), name = "clusterListenerSingleton")

implicit val actorSystem = cs1

//val a = actor("aaa") (new Act {
//  become {
//    case "hello" => sender() ! "hi"
//  }
//})
//
//val a1 = actor(new Act {
//  become {
//    case "hello" => sender() ! "hi"
//  }
//})