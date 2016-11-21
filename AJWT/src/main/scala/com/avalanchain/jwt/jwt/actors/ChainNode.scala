package com.avalanchain.jwt.jwt.actors

import java.security.{KeyPair, PublicKey}
import java.util.UUID

import akka.NotUsed
import akka.util.Timeout
import akka.actor.ActorDSL._
import akka.actor.{ActorContext, ActorLogging, ActorRef, ActorRefFactory, ActorSystem, Props}
import akka.stream.ActorMaterializer
import akka.pattern.{ask, pipe}
import akka.util.Timeout
import com.avalanchain.jwt.basicChain.{ChainDef, JwtAlgo, TypedJwtToken}
import com.avalanchain.jwt.jwt.actors.ChainRegistryActor._
import com.typesafe.config.ConfigFactory
import io.circe.{Decoder, DecodingFailure, Encoder, Json}
import io.circe.syntax._
import io.circe.parser._
import io.circe.generic.JsonCodec
import io.circe.generic.auto._

import scala.collection._
import scala.concurrent._
import scala.concurrent.duration._
import scala.concurrent.ExecutionContext.Implicits.global
import scala.util.Try

/**
  * Created by Yuriy Habarov on 21/11/2016.
  */
object ChainNode {

  sealed trait ChainNodeRequest

  final case class NewChain(jwtAlgo: JwtAlgo, initValue: Option[Json] = Some(Json.fromString("{}"))) extends ChainNodeRequest

  //final case class GetSink(chainRef: ChainRef) extends ChainNodeRequest

  //sealed trait ChainNodeResponse
  //final case class FrameTokenSink(sink: Sink[Json, NotUsed])
  //final case class FrameTokenSource(source: Source[FrameToken, NotUsed])

  def createNode(keyPair: KeyPair, knownKeys: Set[PublicKey])(implicit encoder: Encoder[ChainDef], decoder: Decoder[ChainDef]): (ActorRef, ActorMaterializer) = {

    val system = ActorSystem("node", ConfigFactory.load("application.conf"))
    val materializer = ActorMaterializer()(system)
    implicit val timeout = Timeout(5 seconds)

    val node = actor(system, "node")(new Act {
      val registry = actor("registry")(new ChainRegistryActor())
      val tc = actor("testChild")(new Act {
        whenStarting {
          context.parent ! ("hello from " + self.path)
        }
      })
      become {
        case GetChains => pipe(registry ? GetChains) to sender()
        case PrintState => registry ! PrintState

        case NewChain(jwtAlgo, initValue) =>
          val chainDef: ChainDef = ChainDef.New(jwtAlgo, UUID.randomUUID(), keyPair.getPublic, initValue)
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

    (node, materializer)
  }
}