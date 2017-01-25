package com.avalanchain.jwt.jwt.actors

import java.security.{KeyPair, PublicKey}
import java.util.UUID
import java.net._
import java.util.concurrent.atomic.AtomicBoolean

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
class ChainNode2(nodeId: NodeIdToken, val keyPair: KeyPair, knownKeys: Set[PublicKey], connectTo: Set[(String, Int)], initialChainRefs: Set[ChainDefToken] = Set.empty)
                   /* (implicit encoder: Encoder[ChainDef], decoder: Decoder[ChainDef])*/ extends ActorNode {

  implicit val actorSystem = system
  val publicKey = keyPair.getPublic
  val port = nodeId.payload.get.port

  val logIdNodes: String = "_$nodes$_"
  val logIdKnownChains: String = "_$known-chains$_"

  private val chainEndpoints = new java.util.concurrent.ConcurrentHashMap[ChainRef, (ChainDefToken, ReplicationEndpoint)]().asScala
  private val activated = new AtomicBoolean(false)

  def chainRefs() = chainEndpoints.keys.toSet
  protected def getLog(chainRef: ChainRef) = chainEndpoints.get(chainRef).map(r => (r._1, r._2.logs(chainRef.sig)))

  protected def createEventLog(defs: Set[ChainDefToken]): Map[ChainRef, (ChainDefToken, ActorRef)] = {
    if (defs.isEmpty) Map.empty
    else {
      val crsm = defs.map(cr => ChainRef(cr) -> cr).toMap
      val crs = crsm.keys.toSet
      val existingCrs = chainRefs & crs
      val newCrs = chainRefs -- crs
      val endpoint = new ReplicationEndpoint(id = nodeId.sig + "-" + UUID.randomUUID().toString, logNames = newCrs.map(_.sig),
        logFactory = logId => LeveldbEventLog.props(logId, nodeId.sig),
        connections = connectTo.map(ep => ReplicationConnection(ep._1, ep._2)))
      if (activated.get()) endpoint.activate()
      newCrs.foreach(cr => chainEndpoints.put(cr, (crsm.get(cr).get, endpoint)))
      (existingCrs | newCrs).map(cr => (cr, getLog(cr).get)).toMap
    }
  }

  def activate() {
    if (!activated.get())
      chainEndpoints.values.foreach(ep => {
        try {
          ep._2.activate()
        }
        catch {
          case (e: Exception) => println(s"Exception during endpoint '${ep._2.id}' activation for ChainDef '${ep._1}': '$e'")
        }
      })
  }

  createEventLog(initialChainRefs)
  activate()

  def getChain(chainRef: ChainRef) = getLog(chainRef).toRight(ChainNotFound(chainRef))

//  def sink(chainRef: ChainRef) = getChain(chainRef).map()
//    (registry ? GetJsonSink(chainRef)).mapTo[Either[ChainRegistryError, Sink[Json, NotUsed]]]
//
//  def source(chainRef: ChainRef, from: Position, to: Position) =
//    (registry ? GetJsonSource(chainRef, from, to)).mapTo[Either[ChainRegistryError, Source[Either[JwtError, Json], NotUsed]]]
//
//  def sourceF(chainRef: ChainRef, from: Position, to: Position) =
//    (registry ? GetFrameSource(chainRef, from, to)).mapTo[Either[ChainRegistryError, Source[Either[JwtError, Frame], NotUsed]]]
//
//  def sourceFT(chainRef: ChainRef, from: Position, to: Position) =
//    (registry ? GetFrameTokenSource(chainRef, from, to)).mapTo[Either[ChainRegistryError, Source[FrameToken, NotUsed]]]
//
//  protected def tryGetLog(chainRef: ChainRef): Either[ChainRegistryError, (ChainDefToken, ActorRef)] = {
//    getLog(chainRef) match {
//      case None => Either.left(ChainNotFound(chainRef))
//      case Some(chainDefToken) => {
//        context.child(chainRef.sig) match {
//          case Some(actorRef) => Either.right(chainDefToken, actorRef)
//          case None => {
//            val actorRef = context.actorOf(ChainPersistentActor.props(chainDefToken), chainRef.sig)
//            Either.right(chainDefToken, actorRef)
//          }
//        }
//      }
//    }
//  }

///////////////////////////////////////////////

  case class ChainView(chainDef: ChainDefToken, nodeId: NodeIdToken, status: ChainStatus)
  case class NodeView(nodeId: NodeIdToken, chains: Map[ChainRef, ChainView])

  case class ChainUpdated(chainView: ChainView, pub: PubKey) extends JwtPayload.Asym
  type ChainUpdatedToken = TypedJwtToken[ChainUpdated]

  case class NodeUpdated(nodeId: NodeId, pub: PubKey) extends JwtPayload.Asym
  type NodeUpdatedToken = TypedJwtToken[NodeUpdated]


//  protected val endpoint = new ReplicationEndpoint(id = nodeId.sig, logNames = Set(logIdNodes, logIdKnownChains),
//    logFactory = logId => LeveldbEventLog.props(logId, nodeId.sig),
//    connections = connectTo.map(ep => ReplicationConnection(ep._1, ep._2)))
//
//  val nodesService = new ORSetService[NodeView](s"$logIdNodes-$nodeId", endpoint.logs(logIdNodes))
//  val knownChainsService = new MVRegisterService[NodeView](s"$logIdKnownChains-$nodeId", endpoint.logs(logIdKnownChains))

//  knownChainsService.
//  nodesService.
//  def nodes() {
//    nodesService.value("a").
//  }
}
