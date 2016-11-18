package com.avalanchain.jwt.runners

import com.avalanchain.jwt._
import com.avalanchain.jwt.actors.ChainRegistryActor.{ChainCreationResult, GetChains}

import scala.concurrent.{Await, Future}
import akka.pattern.{ask, pipe}
import akka.util.Timeout
import com.avalanchain.jwt.actors.ChainNode.NewChain
import com.avalanchain.jwt.basicChain.{ChainDefToken, ChainRef, JwtAlgo}
import com.avalanchain.jwt.jwt.CurveContext

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

  val node = actors.createNode(CurveContext.currentKeys, Set.empty)
  node ! "test"

  val chains = Await.result(node ? GetChains, 5 seconds).asInstanceOf[Map[ChainRef, ChainDefToken]]
  println(s"Chains: ${chains}")
  chains.foreach(c => println(s"Chain: $c"))

  val newChain = Await.result(node ? NewChain(JwtAlgo.HS512), 5 seconds).asInstanceOf[ChainCreationResult]
  println(s"Chains: ${newChain}")

  val chains2 = Await.result(node ? GetChains, 5 seconds).asInstanceOf[Map[ChainRef, ChainDefToken]]
  println(s"Chains: ${chains2}")
  chains2.foreach(c => println(s"Chain: $c"))
}
