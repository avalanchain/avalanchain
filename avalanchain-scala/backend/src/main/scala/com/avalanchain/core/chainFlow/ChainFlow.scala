package com.avalanchain.core.chainFlow

import java.util.UUID

import akka.NotUsed
import akka.actor.{ActorSystem, Props}
import akka.persistence.query.journal.leveldb.scaladsl.LeveldbReadJournal
import akka.persistence.query.{EventEnvelope, PersistenceQuery}
import akka.stream.Materializer
import akka.stream.scaladsl.{Sink, Source}
import com.avalanchain.core.domain._

/**
  * Created by Yuriy Habarov on 19/04/2016.
  */

class ChainFlow[T](node: CryptoContext, chainRef: ChainRef, implicit val system: ActorSystem, implicit val materializer: Materializer) {
  private val queries = PersistenceQuery(system).readJournalFor[LeveldbReadJournal](LeveldbReadJournal.Identifier)

  def envelopStream() = {
    val src: Source[EventEnvelope, NotUsed] = queries.eventsByPersistenceId(chainRef.hash.toString(), 0L, Long.MaxValue)
    src
  }

  def frameStream() = envelopStream().map(_.event.asInstanceOf[StateFrame[T]])

  def eventStream() = envelopStream().map(_.event.asInstanceOf[StateFrame[T]].value)

  def map[B](f: T => B, initialValue: B, snapshotInterval: Int = 100): ChainFlow[B] = {
    val childChainRef = node.hasher(ChainRefData(UUID.randomUUID(), chainRef.value.name + "\\map", 0))
    val sinkActor = Sink.actorSubscriber(Props(new ChainStreamNode(node, childChainRef, snapshotInterval, initialValue)))

    val stream = eventStream().map(e => node.hasher(f(e.value))).runWith(sinkActor)

    new ChainFlow[B](node, childChainRef, system, materializer)
  }

  def filter(f: T => Boolean, initialValue: T, snapshotInterval: Int = 100): ChainFlow[T] = {
    val childChainRef = node.hasher(ChainRefData(UUID.randomUUID(), chainRef.value.name + "\\filter", 0))
    val sinkActor = Sink.actorSubscriber(Props(new ChainStreamNode[T](node, childChainRef, snapshotInterval, initialValue)))

    val stream = eventStream().filter(e => f(e.value)).runWith(sinkActor)

    new ChainFlow[T](node, childChainRef, system, materializer)
  }

  def fold[B](f: B => T => B, initialValue: B, snapshotInterval: Int = 100): ChainFlow[B] = {
    val childChainRef = node.hasher(ChainRefData(UUID.randomUUID(), chainRef.value.name + "\\fold", 0))
    val sinkActor = Sink.actorSubscriber(Props(new ChainStreamNode(node, childChainRef, snapshotInterval, initialValue)))

    val stream = eventStream().fold(node.hasher(initialValue))((state, e) => node.hasher(f(state.value) (e.value))).runWith(sinkActor)

    new ChainFlow[B](node, childChainRef, system, materializer)
  }

  //  def reduce(f: T => T => T, snapshotInterval: Int = 100): ChainFlow[T] = {
  //    fold[T](f, null, snapshotInterval)
  //  }

  def groupBy(f: T => String, maxSubStreams: Int, initialValue: T, snapshotInterval: Int = 100): ChainFlow[T] = {
    val childChainRef = node.hasher(ChainRefData(UUID.randomUUID(), chainRef.value.name + "\\groupBy", 0))
    val sinkActor = Sink.actorSubscriber(Props(new ChainGroupByNode[T](node, childChainRef, snapshotInterval, initialValue, f)))

    val stream = eventStream().runWith(sinkActor)

    new ChainFlow[T](node, childChainRef, system, materializer)
  }

  def mapFrame[B](f: StateFrame[T] => B, initialValue: B, snapshotInterval: Int = 100): ChainFlow[B] = {
    val childChainRef = node.hasher(ChainRefData(UUID.randomUUID(), chainRef.value.name + "\\mapFrame", 0))
    val sinkActor = Sink.actorSubscriber(Props(new ChainStreamNode(node, childChainRef, snapshotInterval, initialValue)))

    val stream = frameStream().map(e => node.hasher(f(e))).runWith(sinkActor)

    new ChainFlow[B](node, childChainRef, system, materializer)
  }

  def filterFrame(f: StateFrame[T] => Boolean, initialValue: T, snapshotInterval: Int = 100): ChainFlow[T] = {
    val childChainRef = node.hasher(ChainRefData(UUID.randomUUID(), chainRef.value.name + "\\filterFrame", 0))
    val sinkActor = Sink.actorSubscriber(Props(new ChainStreamNode(node, childChainRef, snapshotInterval, initialValue)))

    val stream = frameStream().filter(e => f(e)).runWith(sinkActor)

    new ChainFlow[T](node, childChainRef, system, materializer)
  }

  def foldFrame[B](f: B => StateFrame[T] => B, initialValue: B, snapshotInterval: Int = 100): ChainFlow[B] = {
    val childChainRef = node.hasher(ChainRefData(UUID.randomUUID(), chainRef.value.name + "\\fold", 0))
    val sinkActor = Sink.actorSubscriber(Props(new ChainStreamNode(node, childChainRef, snapshotInterval, initialValue)))

    val stream = frameStream().fold(node.hasher(initialValue))((state, e) => node.hasher(f(state.value) (e))).runWith(sinkActor)

    new ChainFlow[B](node, childChainRef, system, materializer)
  }
}

object ChainFlow {
  def create[T](node: CryptoContext, name: String, source: Source[T, NotUsed], initialValue: T, snapshotInterval: Int = 100)
               (implicit system: ActorSystem, materializer: Materializer) : ChainFlow[T] = {
    val childChainRef = node.hasher(ChainRefData(UUID.randomUUID(), name, 0))
    val sinkActor = Sink.actorSubscriber(Props(new ChainStreamNode[T](node, childChainRef, snapshotInterval, initialValue)))

    val stream = source.map(e => node.hasher(e)).runWith(sinkActor)

    new ChainFlow[T](node, childChainRef, system, materializer)
  }
}
