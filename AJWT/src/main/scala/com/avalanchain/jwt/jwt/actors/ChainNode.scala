package com.avalanchain.jwt.jwt.actors

import java.security.{KeyPair, PublicKey}
import java.util.UUID

import akka.NotUsed
import akka.util.Timeout
import akka.actor.ActorDSL._
import akka.actor.{ActorContext, ActorLogging, ActorRef, ActorRefFactory, ActorSystem, ExtendedActorSystem, Props}
import akka.stream.ActorMaterializer
import akka.pattern.{ask, pipe}
import akka.stream.actor.ActorPublisher
import akka.stream.scaladsl.{Sink, Source}
import akka.util.Timeout
import cats.implicits._
import com.avalanchain.jwt.basicChain.{Frame, _}
import com.avalanchain.jwt.basicChain.ChainDefCodecs._
import com.avalanchain.jwt.jwt.CurveContext
import com.avalanchain.jwt.jwt.actors.ChainNode.{GetNetworkMonitor, NewChain}
import com.avalanchain.jwt.jwt.actors.ChainRegistryActor._
import com.avalanchain.jwt.jwt.actors.network.{NetworkMonitor, NodeStatus}
import com.typesafe.config.ConfigFactory
import io.circe.{Decoder, DecodingFailure, Encoder, Json}
import io.circe.syntax._
import io.circe.parser._
import io.circe.generic.JsonCodec
import io.circe.generic.auto._

import scala.concurrent._
import scala.concurrent.duration._
import scala.concurrent.ExecutionContext.Implicits.global
import scala.util.Try

/**
  * Created by Yuriy Habarov on 21/05/2016.
  */
class ChainNode(val port: Int, keyPair: KeyPair, knownKeys: Set[PublicKey]/*, atMost: FiniteDuration = 5 seconds*/) extends ActorNode {

  val publicKey = keyPair.getPublic

  private val registry = actor("registry")(new ChainRegistryActor())
  private val addr = actor("addr")(new Act {
    become {
      case "port" => sender() ! system.asInstanceOf[ExtendedActorSystem].provider.getDefaultAddress.port
    }
  })

  def localport(): Future[Int] = (addr ? "port").map(_.asInstanceOf[Option[Int]].get)

  def chains() = (registry ? GetChains).mapTo[Map[ChainRef, ChainDefToken]]

  def newChain(jwtAlgo: JwtAlgo = JwtAlgo.HS512, initValue: Option[Json] = Some(Json.fromString("{}"))) = {
    val chainDef: ChainDef = ChainDef.New(jwtAlgo, UUID.randomUUID(), keyPair.getPublic, initValue.map(_.noSpaces))
    val chainDefToken = TypedJwtToken[ChainDef](chainDef, keyPair.getPrivate)
    (registry ? CreateChain(chainDefToken)).mapTo[ChainCreationResult]
  }

  def getChain(chainRef: ChainRef) = (registry ? GetChainByRef(chainRef)).mapTo[Either[ChainRegistryError, (ChainDefToken, ActorRef)]]

  def sink(chainRef: ChainRef) = (registry ? GetJsonSink(chainRef)).mapTo[Either[ChainRegistryError, Sink[Json, NotUsed]]]

  def source(chainRef: ChainRef, from: Position, to: Position) =
    (registry ? GetJsonSource(chainRef, from, to)).mapTo[Either[ChainRegistryError, Source[Either[JwtError, Json], NotUsed]]]

  def sourceF(chainRef: ChainRef, from: Position, to: Position) =
    (registry ? GetFrameSource(chainRef, from, to)).mapTo[Either[ChainRegistryError, Source[Either[JwtError, Frame], NotUsed]]]

  def sourceFT(chainRef: ChainRef, from: Position, to: Position) =
    (registry ? GetFrameTokenSource(chainRef, from, to)).mapTo[Either[ChainRegistryError, Source[FrameToken, NotUsed]]]

  def monitorSource() = {
    val monitorRef = actor("monitor" + (UUID.randomUUID().toString.replace("-", "")))(new NetworkMonitor())
    Source.fromPublisher[NodeStatus](ActorPublisher(monitorRef))
  }

  def nodesSnapshot(): Future[Map[NodeStatus.Address, NodeStatus]] = {
    monitorSource().takeWithin(10 milliseconds).runFold(Map.empty[NodeStatus.Address, NodeStatus])((acc, s) => { acc + (s.address -> s) })
  }
}
object ChainNode {
  sealed trait ChainNodeRequest

  final case class NewChain(jwtAlgo: JwtAlgo, initValue: Option[Json] = Some(Json.fromString("{}"))) extends ChainNodeRequest

  final case object GetNetworkMonitor extends ChainNodeRequest
  //final case class GetSink(chainRef: ChainRef) extends ChainNodeRequest
}
