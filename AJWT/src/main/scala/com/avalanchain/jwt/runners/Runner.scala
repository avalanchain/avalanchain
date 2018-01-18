package com.avalanchain.jwt.runners

import akka.NotUsed
import com.avalanchain.jwt._

import scala.concurrent.{Await, Future}
import akka.pattern.{ask, pipe}
import akka.stream.scaladsl.{Sink, Source}
import akka.util.Timeout
import cats.implicits._
import com.avalanchain.jwt.api.MainCmd.ActorNode
import com.avalanchain.jwt.basicChain._
import com.avalanchain.jwt.jwt.CurveContext
import com.avalanchain.jwt.jwt.actors.{ActorNode, ChainNode}
import com.avalanchain.jwt.jwt.actors.ChainNode.NewChain
import com.avalanchain.jwt.jwt.actors.ChainRegistryActor._
import com.rbmhtechnology.eventuate.adapter.stream.{DurableEventSource, DurableEventWriter}
import com.rbmhtechnology.eventuate.{DurableEvent, ReplicationConnection, ReplicationEndpoint}
import com.rbmhtechnology.eventuate.log.leveldb.LeveldbEventLog

import scala.concurrent.duration._
import scala.concurrent.ExecutionContext.Implicits.global
import io.circe.{Decoder, DecodingFailure, Encoder, Json}
import io.circe.syntax._
import io.circe.parser._
import io.circe.generic.JsonCodec
import io.circe.generic.auto._


case class Node(id: String, port: Int, connectPort: Int) extends ActorNode {
  //val log = system.actorOf(LeveldbEventLog.props(logId = "L1", prefix = id + "-log"))

  val endpoint = new ReplicationEndpoint(id = id, logNames = Set("L", "M"),
    logFactory = logId => LeveldbEventLog.props(logId),
    connections = Set(ReplicationConnection("127.0.0.1", connectPort)),
    applicationName = ActorNode.SystemName)

  endpoint.activate()
}



/**
  * Created by Yuriy on 18/05/2016.
  */
object Runner extends App {
  implicit val timeout = Timeout(5 seconds)

  val node1 = Node("1", 2551, 2552)
  Thread.sleep(1000)
  val node2 = Node("2", 2552, 2551)
  val node3 = Node("3", 2553, 2551)


  val logsNames = node1.endpoint.logNames
  val logs = node1.endpoint.logs

  implicit val system = node1.system
  implicit val materializer = node1.materializer

  val source1 = Source.fromGraph(DurableEventSource(logs("M"))).runForeach(println)

  Source(List("a", "b", "c"))
    .map(DurableEvent(_))
    .via(DurableEventWriter("writerId1", logs("M")))
    .map(event => (event.payload, event.localSequenceNr))
    .runForeach(println)


  //  var chainNode = new ChainNode(2551, CurveContext.currentKeys, Set.empty)
//  val node = chainNode.node
//  node ! "test"
//
//  val chains = Await.result(node ? GetChains, 5 seconds).asInstanceOf[Map[ChainRef, ChainDefToken]]
//  println(s"Chains: ${chains}")
//  chains.foreach(c => println(s"Chain: $c"))
//
//  val newChain = Await.result(node ? NewChain(JwtAlgo.HS512), 5 seconds).asInstanceOf[ChainCreationResult]
//  println(s"Chains: ${newChain}")
//
//  val chains2 = Await.result(node ? GetChains, 5 seconds).asInstanceOf[Map[ChainRef, ChainDefToken]]
//  println(s"Chains: ${chains2}")
//  chains2.foreach(c => println(s"Chain: $c"))
//
//  val chainRef = ChainRef(newChain.chainDefToken)
//
//  val sink = Await.result(node ? GetJsonSink(chainRef), 5 seconds).asInstanceOf[Either[ChainRegistryError, Sink[Json, NotUsed]]]
//  println(s"Sink created: $sink")
//
//  val source = Await.result(node ? GetJsonSource(chainRef, 0, 1000), 5 seconds).asInstanceOf[Either[ChainRegistryError, Source[Either[JwtError, Json], NotUsed]]]
//  println(s"Source created: $source")
//
//  val sourceF = Await.result(node ? GetFrameSource(chainRef, 0, 1000), 5 seconds).asInstanceOf[Either[ChainRegistryError, Source[Either[JwtError, Frame], NotUsed]]]
//  println(s"Source created: $sourceF")
//
//  val sourceFT = Await.result(node ? GetFrameTokenSource(chainRef, 0, 1000), 5 seconds).asInstanceOf[Either[ChainRegistryError, Source[FrameToken, NotUsed]]]
//  println(s"Source created: $sourceFT")
//
//  implicit val materializer = chainNode.materializer
//
//  Future {
//    source.toOption.get.to(Sink.foreach(e => println(s"Record from source: $e"))).run()
//  }
//
//  Future {
//    sourceF.toOption.get.to(Sink.foreach(e => println(s"Record from sourceF: $e"))).run()
//  }
//
//  Future {
//    sourceFT.toOption.get.to(Sink.foreach(e => println(s"Record from sourceFT: $e"))).run()
//  }
//
//  Future {
//    Source(0 until 10).map(e => s"""{ \"v\": $e }""").map(Json.fromString(_)).to(sink.toOption.get).run()
//  }

}
