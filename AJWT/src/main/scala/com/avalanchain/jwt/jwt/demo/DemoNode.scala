package com.avalanchain.jwt.jwt.demo

import java.security.{KeyPair, PublicKey}

import akka.stream.ThrottleMode
import akka.stream.scaladsl.Source
import com.avalanchain.jwt.KeysDto.PubKey
import com.avalanchain.jwt.basicChain._
import com.avalanchain.jwt.jwt.actors.{ChainNode, ChainNodeFacade}
import com.avalanchain.jwt.jwt.demo.DemoNode.Tick
import com.avalanchain.jwt.utils.Pipe._
import io.circe.{Decoder, DecodingFailure, Encoder, Json}
import io.circe.syntax._
import io.circe.parser._
import io.circe.generic.JsonCodec
import io.circe.generic.auto._

import cats.implicits._

import scala.concurrent.{ExecutionContext, ExecutionContextExecutor, Future}
import scala.concurrent.duration.{DurationInt, FiniteDuration}
import scala.concurrent.{ExecutionContextExecutor, Future}
import scala.collection.Set

/**
  * Created by Yuriy Habarov on 21/05/2016.
  */
class DemoNode(chainNode: ChainNode)(implicit encoder: Encoder[ChainDef], decoder: Decoder[ChainDef]) {
  private val cnf = new ChainNodeFacade(chainNode)
  private implicit val materializer = cnf.materializer

  private val tickerChainRef = ChainRef(cnf.newChain().chainDefToken)
//  private val tickerSink = cnf.sink(tickerChainRef).toOption.get // TODO: Handle errors
//  //private val Future {
//    Source(0 to Int.MaxValue)
//      .throttle(1, 1 second, 1, ThrottleMode.shaping)
//      .map(Tick(_, chainNode.publicKey).asJson)
//      .to(tickerSink)
//      .run()
//  //}
//
//  //private val yahoo = YahooFinSource()
//
//  def tickerSource(from: Position = 0, to: Position = Int.MaxValue) = cnf.source(tickerChainRef, from, to)
  //def yahooSource =
}
object DemoNode {
  case class Tick(index: Int, pub: PubKey) extends JwtPayload.Asym
  type TickToken = TypedJwtToken[Tick]
}