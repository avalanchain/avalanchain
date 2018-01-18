package com.avalanchain.jwt.utils

import java.nio.charset.StandardCharsets
import java.nio.file.{Files, Path, StandardOpenOption}
import java.util.concurrent.ConcurrentHashMap

import akka.NotUsed
import akka.actor.ActorSystem
import akka.stream.{ActorMaterializer, OverflowStrategy}
import akka.stream.scaladsl.{BroadcastHub, Flow, Keep, Sink, Source}
import com.avalanchain.jwt.basicChain.{Frame, TypedJwtToken, _}
import io.circe.Decoder

import scala.concurrent.duration.FiniteDuration
import scala.util.{Success, Try}

import java.nio.charset.{Charset, StandardCharsets}
import java.nio.file.{Files, Path, Paths, StandardOpenOption}
import java.security.{PrivateKey, PublicKey}

import pdi.jwt._
import java.time.Instant
import java.util.UUID
import java.time.OffsetDateTime
import java.time.format.DateTimeFormatter
import java.util.concurrent.ConcurrentHashMap

import com.avalanchain.jwt.jwt.{CurveContext, UserInfo}
import io.circe._
import io.circe.generic.auto._
import io.circe.parser._
import io.circe.syntax._

import scala.collection.immutable._
import scala.util.{Success, Try}
import collection.JavaConverters._
import CurveContext._
import akka.NotUsed
import akka.actor.ActorSystem
import akka.persistence.inmemory.query.scaladsl.InMemoryReadJournal
import akka.persistence.query.PersistenceQuery
import akka.stream.{ActorMaterializer, OverflowStrategy, SinkShape}
import akka.stream.scaladsl.{BroadcastHub, Flow, Keep, MergeHub, RunnableGraph, Sink, Source}
import com.avalanchain.jwt.KeysDto._
import com.avalanchain.jwt.basicChain._
import com.typesafe.config.ConfigFactory
import io.circe.generic.JsonCodec
import pdi.jwt.exceptions.JwtLengthException

import scala.collection.mutable.ArrayBuffer
import scala.concurrent.Await
import scala.concurrent.ExecutionContext.Implicits.global
import collection.JavaConverters._
import com.avalanchain.jwt.utils._

import scala.collection.mutable

import scala.concurrent.duration._
import scala.concurrent._



/**
  * Created by Yuriy on 29/11/2016.
  */
class FileTokenStorage(val folder: Path, val id: String, batchSize: Int = 100, timeWindow: FiniteDuration = 1 second,
                       implicit val system: ActorSystem, implicit val materializer: ActorMaterializer) extends FrameTokenStorage {
  val location = folder.resolve(id)
  val sigMap = new ConcurrentHashMap[FrameRef, FrameToken].asScala

  Files.createDirectories(location)

  private var groupIdx = -1

  val fileSinkDef: Sink[FrameToken, NotUsed] = Flow[FrameToken]
    .groupedWithin(batchSize, timeWindow)
    .map(g => { groupIdx += 1; (groupIdx, g) })
    .to(Sink.foreach(b => {
      val fileName = location.resolve(f"${b._1}%08d")
      Files.deleteIfExists(fileName)
      Files.write(fileName, b._2.map(_.toString).asJavaCollection, StandardCharsets.UTF_8, StandardOpenOption.CREATE_NEW)
    }))

  val queueDef = Source.queue[FrameToken](batchSize, OverflowStrategy.backpressure)

  val (queue, broadcastQueue) = queueDef.toMat(BroadcastHub.sink(bufferSize = 256))(Keep.both).run()

  val fileSink = broadcastQueue.runWith(fileSinkDef)

  override def add(frameToken: FrameToken): Try[Unit] = {
    queue.offer(frameToken)
    sigMap += (FrameRef(frameToken) -> frameToken)
    Success()
  }

  override def get(frameRef: FrameRef): Option[FrameToken] = sigMap.get(frameRef)

  def getFromSnapshot(fromPosition: Position, toPosition: Position)(implicit decoder: Decoder[Frame]): Source[FrameToken, NotUsed] = {
    Source.fromIterator[Path](() => Files.newDirectoryStream(location).iterator().asScala.toList.sorted.iterator)
      .mapConcat[String](f => Files.readAllLines(f).asScala.toList).map(s => new TypedJwtToken[Frame](s))
      //      .fold(ArrayBuffer[FrameToken]())((acc: ArrayBuffer[FrameToken], ft) => { acc += ft; acc }) // TODO: Uncomment for pos sorting within the files
      //      .mapConcat(a => a.sortBy(_.payload.get.pos).toList)
      .filter(_.payload.get.pos >= fromPosition)
      .takeWhile(_.payload.get.pos <= toPosition)
  }

  def getFrom(fromPosition: Position, toPosition: Position)(implicit decoder: Decoder[Frame]): Source[FrameToken, NotUsed] = {
    getFromSnapshot(fromPosition, toPosition).concat(broadcastQueue).takeWhile(_.payload.get.pos <= toPosition)
  }
}


class PersistenceTokenStorage(pid: String, implicit val system: ActorSystem, implicit val materializer: ActorMaterializer) extends FrameTokenStorage {
  val readJournal: InMemoryReadJournal = PersistenceQuery(system).readJournalFor[InMemoryReadJournal](InMemoryReadJournal.Identifier)

  override def add(frameToken: FrameToken): Try[Unit] = {
    //queue.offer(frameToken)
    //sigMap += (FrameRef(frameToken) -> frameToken)
    Success()
  }

  override def get(frameRef: FrameRef): Option[FrameToken] = ???

  def getFromSnapshot(fromPosition: Position, toPosition: Position)(implicit decoder: Decoder[Frame]): Source[FrameToken, NotUsed] = {
    readJournal.eventsByPersistenceId(pid, fromPosition, toPosition).map(_.event.asInstanceOf[FrameToken])
  }

  def getFrom(fromPosition: Position, toPosition: Position)(implicit decoder: Decoder[Frame]): Source[FrameToken, NotUsed] = {
    getFromSnapshot(fromPosition, toPosition)
  }
}
