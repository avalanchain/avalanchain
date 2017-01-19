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
import com.avalanchain.jwt.jwt.CurveContext
import com.avalanchain.jwt.jwt.actors.ChainNode.{GetNetworkMonitor, NewChain}
import com.avalanchain.jwt.jwt.actors.ChainRegistryActor._
import com.avalanchain.jwt.jwt.actors.network.{NetworkMonitor, NodeStatus}
import com.avalanchain.jwt.jwt.chat.ChatNode
import com.avalanchain.jwt.jwt.demo.account.CurrencyNode
import com.avalanchain.jwt.utils.CirceCodecs
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
class ChainNode(val nodeName: String, val port: Int, val keyPair: KeyPair, knownKeys: Set[PublicKey]) extends ActorNode with CirceCodecs {

  val publicKey = keyPair.getPublic
  val nodeIdToken: NodeIdToken = NodeIdToken(nodeName, localhost, port, keyPair.getPublic, keyPair.getPrivate)

  private val registry = actor("registry")(new ChainRegistryActor())

  def chains() = (registry ? GetChains).mapTo[Map[ChainRef, ChainDefToken]]

  val chatNode = new ChatNode(nodeIdToken, keyPair, cn => newChain(JwtAlgo.ES512, cn))
  val currencyNode = new CurrencyNode(nodeIdToken, keyPair, cn => newChain(JwtAlgo.ES512, cn))

  private val bot = actor("bot")(new Act {
    become {
      case "tick" => currencyNode.randomPayment()
    }
  })

  val cancellable =
    system.scheduler.schedule(
      2 seconds,
      50 milliseconds,
      bot,
      "tick")

//  def newChain(jwtAlgo: JwtAlgo = JwtAlgo.HS512, id: Id = UUID.randomUUID().toString.replace("-", ""), initValue: Option[Json] = Some(Json.fromString("{}"))) = {
//    val chainDef: ChainDef = ChainDef.New(jwtAlgo, id, keyPair.getPublic, ResourceGroup.ALL, initValue.map(_.asString.getOrElse("{}")))
//    val chainDefToken = TypedJwtToken[ChainDef](chainDef, keyPair.getPrivate)
//    chainDefToken
//  }
//
//  def derivedChain(parentRef: ChainRef, jwtAlgo: JwtAlgo = JwtAlgo.HS512, id: Id = UUID.randomUUID().toString.replace("-", "")): (ChainDefToken, ChainDef.Derived) = {
//    val chainDef = ChainDef.Derived(jwtAlgo, id, keyPair.getPublic, ResourceGroup.ALL, parentRef, ChainDerivationFunction.Map("function(a) { return { b: a.e + 'aaa' }; }"))
//    val chainDefToken = TypedJwtToken[ChainDef](chainDef, keyPair.getPrivate)
//    (chainDefToken, chainDef)
//  }

  def newChain2(jwtAlgo: JwtAlgo = JwtAlgo.HS512, id: Id = UUID.randomUUID().toString.replace("-", ""), initValue: Option[Json] = Some(Json.fromString("{}"))) = {
    (registry ? CreateChain(newChain(jwtAlgo, id, initValue))).mapTo[ChainCreationResult]
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
