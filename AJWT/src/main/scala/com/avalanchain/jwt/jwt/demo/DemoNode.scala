package com.avalanchain.jwt.jwt.demo

import java.security.{KeyPair, PublicKey}

import akka.stream.ThrottleMode
import akka.stream.scaladsl.Source
import com.avalanchain.jwt.KeysDto.PubKey
import com.avalanchain.jwt.basicChain.{ChainDef, JwtPayload, TypedJwtToken}
import com.avalanchain.jwt.jwt.actors.ChainNode
import com.avalanchain.jwt.jwt.demo.DemoNode.Tick
import io.circe.{Decoder, DecodingFailure, Encoder, Json}
import io.circe.syntax._
import io.circe.parser._
import io.circe.generic.JsonCodec
import io.circe.generic.auto._

import scala.concurrent.{ExecutionContext, ExecutionContextExecutor, Future}
import scala.concurrent.duration.{DurationInt, FiniteDuration}
import scala.concurrent.{ExecutionContextExecutor, Future}
import scala.collection.Set

/**
  * Created by Yuriy Habarov on 21/11/2016.
  */
class DemoNode(keyPair: KeyPair, knownKeys: Set[PublicKey])(implicit encoder: Encoder[ChainDef], decoder: Decoder[ChainDef]) {
  val (node, materializer) = ChainNode.createNode(keyPair, knownKeys)

  def tickSource = Source(0 to Int.MaxValue)
    .throttle(1, 1 second, 1, ThrottleMode.shaping)
    .map(Tick(_, keyPair.getPublic))
}
object DemoNode {
  case class Tick(index: Int, pub: PubKey) extends JwtPayload.Asym
  type TickToken = TypedJwtToken[Tick]
}