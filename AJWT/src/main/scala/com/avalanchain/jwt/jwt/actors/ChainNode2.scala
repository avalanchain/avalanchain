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
import com.avalanchain.jwt.basicChain.{Frame, _}
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

//import scala.collection._
import scala.concurrent._
import scala.concurrent.duration._
import scala.concurrent.ExecutionContext.Implicits.global
import scala.util.Try

/**
  * Created by Yuriy Habarov on 21/05/2016.
  */
class ChainNode2(nodeId: NodeId, val port: Int, keyPair: KeyPair, knownKeys: Set[PublicKey], connectTo: Set[(String, Int)])
                (implicit encoder: Encoder[ChainDef], decoder: Decoder[ChainDef]) extends ActorNode {

  implicit val actorSystem = system
  val publicKey = keyPair.getPublic

  val logIdNodes: String = "_$nodes$_"
  val logIdKnownChains: String = "_$known-chains$_"

  case class ChainView(chainDef: ChainDefToken, nodeId: NodeId, status: ChainStatus)
  case class NodeView(nodeId: NodeId, host: String, port: Int, chains: Map[ChainRef, ChainView])


  protected def createEventLog(id: String, nodeId: String): ActorRef = system.actorOf(LeveldbEventLog.props(id, nodeId))

  protected val endpoint = new ReplicationEndpoint(id = nodeId, logNames = Set(logIdNodes, logIdKnownChains),
    logFactory = logId => LeveldbEventLog.props(logId, nodeId),
    connections = connectTo.map(ep => ReplicationConnection(ep._1, ep._2)))

  val nodesService = new ORSetService[NodeView](s"$logIdNodes-$nodeId", endpoint.logs(logIdNodes))
  val knownChainsService = new MVRegisterService[NodeView](s"$logIdKnownChains-$nodeId", endpoint.logs(logIdKnownChains))

  def activate() {
    endpoint.activate()
    nodesService.add(nodeId, NodeView(nodeId, localhost, port, Map.empty))
  }

  activate()

//  def nodes() {
//    nodesService.value("a").
//  }
}
