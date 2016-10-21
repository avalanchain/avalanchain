package com.avalanchain.cluster

/**
  * Created by Yuriy Habarov on 01/06/2016.
  */
import akka.actor.{Actor, ActorRef, Props}
import akka.cluster.Cluster
import akka.cluster.ddata.{DistributedData, LWWMap, LWWMapKey}
import com.avalanchain.core.domain._

object NodeKeysRepository {

  def props: Props = Props[NodeKeysRepository]

  private final case class Request(signingPublicKey: SigningPublicKey, replyTo: ActorRef)

  // TODO: Add NodeRef
  final case class Register(nodeInfo: NodeInfo, signature: Signature)
  //final case class CheckPublicKey(nodeRef: Option[(SigningPublicKey, Signature)])
  //final case class Evict(key: String)
  final case class GetFromCache(signingPublicKey: SigningPublicKey)
  final case class ValidatedKey(signingPublicKey: SigningPublicKey, value: Option[(NodeInfo, Signature)])
}

class NodeKeysRepository extends Actor {
  import NodeKeysRepository._
  import akka.cluster.ddata.Replicator._

  val replicator = DistributedData(context.system).replicator
  implicit val cluster = Cluster(context.system)

  def dataKey(signingPublicKey: SigningPublicKey) = {
    val key = signingPublicKey.toString
    val cacheKey: LWWMapKey[Any] = LWWMapKey(key)
    (key, cacheKey)
  }

  def receive = {
    case Register(nodeInfo, signature) =>
      val key = dataKey(nodeInfo.signingPublicKey)
      val v: Any = (nodeInfo, signature)
      replicator ! Update.apply(key._2, LWWMap.empty[Any], WriteLocal)((a: LWWMap[Any]) => a + (key._1 -> v))
//    case Evict(key) =>
//      replicator ! Update(dataKey(key), LWWMap(), WriteLocal)(_ - key)

    case GetFromCache(signingPublicKey) =>
      val key = dataKey(signingPublicKey)
      replicator ! Get(key._2, ReadLocal, Some(Request(signingPublicKey, sender())))
    case g @ GetSuccess(LWWMapKey(_), Some(Request(signingPublicKey, replyTo))) => {
      val key = dataKey(signingPublicKey)
      g.dataValue match {
        case data: LWWMap[(NodeInfo, Signature)] => data.get(key._1) match {
          case Some(value) => replyTo ! ValidatedKey(signingPublicKey, Some(value))
          case None => replyTo ! ValidatedKey(signingPublicKey, None)
        }
      }
    }
    case NotFound(_, Some(Request(signingPublicKey, replyTo))) =>
      replyTo ! ValidatedKey(signingPublicKey, None)
    case _: UpdateResponse[_] => // ok
  }

}
