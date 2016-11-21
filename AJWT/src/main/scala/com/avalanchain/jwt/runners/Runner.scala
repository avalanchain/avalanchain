package com.avalanchain.jwt.runners

import akka.NotUsed
import com.avalanchain.jwt._

import scala.concurrent.{Await, Future}
import akka.pattern.{ask, pipe}
import akka.stream.scaladsl.{Sink, Source}
import akka.util.Timeout
import cats.data.Xor
import com.avalanchain.jwt.basicChain._
import com.avalanchain.jwt.jwt.CurveContext
import com.avalanchain.jwt.jwt.actors.ChainNode
import com.avalanchain.jwt.jwt.actors.ChainNode.NewChain
import com.avalanchain.jwt.jwt.actors.ChainRegistryActor._

import scala.concurrent.duration._
import scala.concurrent.ExecutionContext.Implicits.global
import io.circe.{Decoder, DecodingFailure, Encoder, Json}
import io.circe.syntax._
import io.circe.parser._
import io.circe.generic.JsonCodec
import io.circe.generic.auto._

/**
  * Created by Yuriy on 18/11/2016.
  */
object Runner extends App {
  implicit val timeout = Timeout(5 seconds)

  val (node, materializer) = ChainNode.createNode(CurveContext.currentKeys, Set.empty)
  node ! "test"

  val chains = Await.result(node ? GetChains, 5 seconds).asInstanceOf[Map[ChainRef, ChainDefToken]]
  println(s"Chains: ${chains}")
  chains.foreach(c => println(s"Chain: $c"))

  val newChain = Await.result(node ? NewChain(JwtAlgo.HS512), 5 seconds).asInstanceOf[ChainCreationResult]
  println(s"Chains: ${newChain}")

  val chains2 = Await.result(node ? GetChains, 5 seconds).asInstanceOf[Map[ChainRef, ChainDefToken]]
  println(s"Chains: ${chains2}")
  chains2.foreach(c => println(s"Chain: $c"))

  val chainRef = ChainRef(newChain.chainDefToken)

  val sink = Await.result(node ? GetJsonSink(chainRef), 5 seconds).asInstanceOf[Xor[ChainRegistryError, Sink[Json, NotUsed]]]
  println(s"Sink created: $sink")

  val source = Await.result(node ? GetJsonSource(chainRef, 0, 1000), 5 seconds).asInstanceOf[Xor[ChainRegistryError, Source[Xor[JwtError, Json], NotUsed]]]
  println(s"Source created: $source")

  val sourceF = Await.result(node ? GetFrameSource(chainRef, 0, 1000), 5 seconds).asInstanceOf[Xor[ChainRegistryError, Source[Xor[JwtError, Frame], NotUsed]]]
  println(s"Source created: $sourceF")

  val sourceFT = Await.result(node ? GetFrameTokenSource(chainRef, 0, 1000), 5 seconds).asInstanceOf[Xor[ChainRegistryError, Source[FrameToken, NotUsed]]]
  println(s"Source created: $sourceFT")

  Future {
    source.toOption.get.to(Sink.foreach(e => println(s"Record from source: $e"))).run()(materializer)
  }

  Future {
    sourceF.toOption.get.to(Sink.foreach(e => println(s"Record from sourceF: $e"))).run()(materializer)
  }

  Future {
    sourceFT.toOption.get.to(Sink.foreach(e => println(s"Record from sourceFT: $e"))).run()(materializer)
  }

  Future {
    Source(0 until 10).map(e => s"""{ \"v\": $e }""").map(Json.fromString(_)).to(sink.toOption.get).run()(materializer)
  }

}
