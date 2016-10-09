package com.avalanchain.cluster

import javassist.bytecode.stackmap.TypeTag

import akka.actor.{Actor, ActorLogging, ActorRef}
import akka.cluster.Cluster
import akka.cluster.ddata.Replicator._
import akka.cluster.ddata._
import com.avalanchain.core.domain.{ChainRef, HashedValue}
import shapeless.TypeClass

import scala.concurrent.duration._
import scala.reflect.runtime.universe._



/**
  * Created by Yuriy Habarov on 09/05/2016.
  */
class ChainRegistry[T](val name: String) extends Actor with ActorLogging {
  final case class Add[T](value: HashedValue[T])
  sealed trait GetCommand
  final case class Get(ref: String) extends GetCommand
  final case object GetAll extends GetCommand
  final case object GetHashes extends GetCommand

  val RegistryMapKey = LWWMapKey[HashedValue[T]]("registry_" + name)

  implicit val node = Cluster(context.system)
  val replicator = DistributedData(context.system).replicator
  val writeAll = WriteAll(timeout = 10.seconds)
  val writeMajority = WriteMajority(timeout = 5.seconds)

  override def receive: Receive = {
    case add: Add[T] =>
      replicator ! Update(RegistryMapKey, LWWMap.empty[HashedValue[T]], writeAll)(_ + (add.value.hash.toString -> add.value))

    case getCommand: GetCommand =>
      replicator ! Replicator.Get(RegistryMapKey, ReadLocal, Some((sender(), getCommand)))

    case g @ GetSuccess(RegistryMapKey, Some((replyTo: ActorRef, getCommand: GetCommand))) =>
      getCommand match {
        case Get(v) => replyTo ! g.get(RegistryMapKey).get(v)
        case GetAll => replyTo ! g.get(RegistryMapKey).entries.values.toArray
        case GetHashes => replyTo ! g.get(RegistryMapKey).entries.keys.toArray
      }
    case GetFailure(RegistryMapKey, Some((replyTo: ActorRef, getCommand: GetCommand))) =>
      getCommand match {
        case Get(v) => replyTo ! None
        case GetAll => replyTo ! Array.empty
        case GetHashes => replyTo ! Array.empty
      }
    case NotFound(RegistryMapKey, Some((replyTo: ActorRef, getCommand: GetCommand))) =>
      getCommand match {
        case Get(v) => replyTo ! None
        case GetAll => replyTo ! Array.empty
        case GetHashes => replyTo ! Array.empty
      }

    case a => log.info("Message not processed: " + a.toString)
  }
}
