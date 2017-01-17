package com.avalanchain.jwt.jwt.actors

import java.security.{KeyPair, PublicKey}
import java.util.UUID
import java.net._

import akka.NotUsed
import akka.util.Timeout
import akka.actor.ActorDSL._
import akka.actor.{ActorContext, ActorLogging, ActorRef, ActorRefFactory, ActorSystem, Props}
import akka.stream.ActorMaterializer
import akka.pattern.{ask, pipe}
import akka.stream.scaladsl.{Sink, Source}
import akka.util.Timeout
import cats.implicits._
import com.avalanchain.jwt.KeysDto.PubKey
import com.avalanchain.jwt.basicChain.{ChainRef, Frame, _}
import com.avalanchain.jwt.jwt.CurveContext
import com.avalanchain.jwt.jwt.actors.ChainNode.NewChain
import com.avalanchain.jwt.jwt.actors.ChainRegistryActor._
import com.rbmhtechnology.eventuate.crdt.{MVRegisterService, ORSetService}
import com.rbmhtechnology.eventuate.{ReplicationConnection, ReplicationEndpoint}
import com.rbmhtechnology.eventuate.log.leveldb.LeveldbEventLog
import com.typesafe.config.ConfigFactory
import io.circe.{Decoder, DecodingFailure, Encoder, Json}
import io.circe.syntax._
import io.circe.parser._
import io.circe.generic.JsonCodec
import io.circe.generic.auto._

import scala.collection.mutable

//import scala.collection._
import scala.concurrent._
import scala.concurrent.duration._
import scala.concurrent.ExecutionContext.Implicits.global
import scala.util.Try
import collection.JavaConverters._

/**
  * Created by Yuriy Habarov on 21/05/2016.
  */
class ChainNode2(nodeId: NodeIdToken, keyPair: KeyPair, knownKeys: Set[PublicKey], connectTo: Set[(String, Int)])
                (implicit encoder: Encoder[ChainDef], decoder: Decoder[ChainDef]) extends ActorNode {

  implicit val actorSystem = system
  val publicKey = keyPair.getPublic
  val port = nodeId.payload.get.port

  val logIdNodes: String = "_$nodes$_"
  val logIdKnownChains: String = "_$known-chains$_"

  private val chainEndpoints = new java.util.concurrent.ConcurrentHashMap[ChainRef, ReplicationEndpoint]().asScala

  def chainRefs() = chainEndpoints.keys.toSet
  protected def getLog(chainRef: ChainRef) = chainEndpoints.get(chainRef).map(_.logs(chainRef.sig))

  protected def createEventLog(crs: Set[ChainRef]): Map[ChainRef, ActorRef] = {
    if (crs.isEmpty) Map.empty
    else {
      val existingCrs = chainRefs & crs //.map(cr => (cr, chainEndpoints.get(cr)))
      val newCrs = chainRefs -- crs
      val endpoint = new ReplicationEndpoint(id = nodeId.sig + "-" + UUID.randomUUID().toString, logNames = newCrs.map(_.sig),
        logFactory = logId => LeveldbEventLog.props(logId, nodeId.sig),
        connections = connectTo.map(ep => ReplicationConnection(ep._1, ep._2)))
      newCrs.foreach(chainEndpoints.put(_, endpoint))
      (existingCrs | newCrs).map(cr => (cr, getLog(cr).get)).toMap
    }

    //system.actorOf(LeveldbEventLog.props(id, nodeId))
  }


  case class ChainView(chainDef: ChainDefToken, nodeId: NodeIdToken, status: ChainStatus)
  case class NodeView(nodeId: NodeIdToken, chains: Map[ChainRef, ChainView])

  case class ChainUpdated(chainView: ChainView, pub: PubKey) extends JwtPayload.Asym
  type ChainUpdatedToken = TypedJwtToken[ChainUpdated]

  case class NodeUpdated(nodeId: NodeId, pub: PubKey) extends JwtPayload.Asym
  type NodeUpdatedToken = TypedJwtToken[NodeUpdated]


  protected val endpoint = new ReplicationEndpoint(id = nodeId.sig, logNames = Set(logIdNodes, logIdKnownChains),
    logFactory = logId => LeveldbEventLog.props(logId, nodeId.sig),
    connections = connectTo.map(ep => ReplicationConnection(ep._1, ep._2)))

  val nodesService = new ORSetService[NodeView](s"$logIdNodes-$nodeId", endpoint.logs(logIdNodes))
  val knownChainsService = new MVRegisterService[NodeView](s"$logIdKnownChains-$nodeId", endpoint.logs(logIdKnownChains))

  def activate() {
    endpoint.activate()
    nodesService.add(nodeId.token, NodeView(nodeId, Map.empty))
  }

  activate()

//  knownChainsService.
//  nodesService.
//  def nodes() {
//    nodesService.value("a").
//  }
}
