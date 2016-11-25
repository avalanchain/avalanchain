package com.avalanchain.jwt.jwt.actors

import java.security.{KeyPair, PublicKey}
import java.util.UUID

import akka.NotUsed
import akka.util.Timeout
import akka.actor.ActorDSL._
import akka.actor.{ActorContext, ActorLogging, ActorRef, ActorRefFactory, ActorSystem, Props}
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
class ChainNode(val port: Int, keyPair: KeyPair, knownKeys: Set[PublicKey])
  extends ActorNode {

  val publicKey = keyPair.getPublic

  val node = actor(system, "node")(new Act {
    private val registry = actor("registry")(new ChainRegistryActor())
    val tc = actor("testChild")(new Act {
      whenStarting {
        context.parent ! ("hello from " + self.path)
      }
    })
    become {
      case GetNetworkMonitor => sender() ! actor("monitor" + (UUID.randomUUID().toString.replace("-", "")))(new NetworkMonitor())
      case GetChains => pipe(registry ? GetChains) to sender()
      case PrintState => registry ! PrintState

      case NewChain(jwtAlgo, initValue) =>
        val chainDef: ChainDef = ChainDef.New(jwtAlgo, UUID.randomUUID(), keyPair.getPublic, initValue.map(_.noSpaces))
        val chainDefToken = TypedJwtToken[ChainDef](chainDef, keyPair.getPrivate)
        pipe(registry ? CreateChain(chainDefToken)) to sender()
      case gc: GetChainByRef => pipe(registry ? gc) to sender()
      case gjs: GetJsonSink => pipe(registry ? gjs) to sender()

      case gjs: GetFrameTokenSource => pipe(registry ? gjs) to sender()
      case gjs: GetFrameSource => pipe(registry ? gjs) to sender()
      case gjs: GetJsonSource => pipe(registry ? gjs) to sender()

      case s: String => println(s"Echo $s")

      case c => println(s"Handler for $c not found")
    }
  })
}
object ChainNode {
  sealed trait ChainNodeRequest

  final case class NewChain(jwtAlgo: JwtAlgo, initValue: Option[Json] = Some(Json.fromString("{}"))) extends ChainNodeRequest

  final case object GetNetworkMonitor extends ChainNodeRequest
  //final case class GetSink(chainRef: ChainRef) extends ChainNodeRequest
}

class ChainNodeFacade(chainNode: ChainNode, atMost: FiniteDuration = 5 seconds) {
  implicit val timeout = Timeout(atMost)

  def nodeActorRef = chainNode.node
  val materializer = chainNode.materializer

  def chains() = Await.result(nodeActorRef ? GetChains, atMost).asInstanceOf[Map[ChainRef, ChainDefToken]]

  def newChain() = Await.result(nodeActorRef ? NewChain(JwtAlgo.HS512), atMost).asInstanceOf[ChainCreationResult]

  def sink(chainRef: ChainRef) = Await.result(nodeActorRef ? GetJsonSink(chainRef), atMost).asInstanceOf[Either[ChainRegistryError, Sink[Json, NotUsed]]]

  def source(chainRef: ChainRef, from: Position, to: Position) =
    Await.result(nodeActorRef ? GetJsonSource(chainRef, from, to), atMost).asInstanceOf[Either[ChainRegistryError, Source[Either[JwtError, Json], NotUsed]]]

  def sourceF(chainRef: ChainRef, from: Position, to: Position) =
    Await.result(nodeActorRef ? GetFrameSource(chainRef, from, to), atMost).asInstanceOf[Either[ChainRegistryError, Source[Either[JwtError, Frame], NotUsed]]]

  def sourceFT(chainRef: ChainRef, from: Position, to: Position) =
    Await.result(nodeActorRef ? GetFrameTokenSource(chainRef, from, to), atMost).asInstanceOf[Either[ChainRegistryError, Source[FrameToken, NotUsed]]]

  def monitorSource() = {
    val monitorRef = Await.result(nodeActorRef ? GetNetworkMonitor, atMost).asInstanceOf[ActorRef]
    Source.fromPublisher[NodeStatus](ActorPublisher(monitorRef))
  }

  def nodesSnapshot() = {
    //val statuses = monitorSource().runWith(Sink.fold(Map.empty[NodeStatus.Address, NodeStatus])((acc, s) => acc + (s.address -> s))(chainNode.materializer)
    //Await.result(statuses, atMost).asInstanceOf[Map[NodeStatus.Address, NodeStatus]]
    implicit val mat = ActorMaterializer()(chainNode.system)
    val sink = Sink.fold(Map.empty[NodeStatus.Address, NodeStatus])((acc, s: NodeStatus) => acc + (s.address -> s))
    val statuses = monitorSource().runWith(sink)
    //Await.result(statuses, atMost)
    statuses
  }
}
object ChainNodeFacade {
  def apply(chainNode: ChainNode, atMost: FiniteDuration = 5 seconds): ChainNodeFacade = new ChainNodeFacade(chainNode, atMost)
}