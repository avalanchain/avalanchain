package com.avalanchain.core.chainFlow

import java.util.UUID

import akka.NotUsed
import akka.actor.{ActorSystem, Props}
import akka.persistence.query.journal.leveldb.scaladsl.LeveldbReadJournal
import akka.persistence.query.{EventEnvelope, PersistenceQuery}
import akka.stream.Materializer
import akka.stream.scaladsl.{Sink, Source}
import com.avalanchain.core.chain.{ChainRefData, _}
import com.avalanchain.core.domain._

/**
  * Created by Yuriy Habarov on 19/04/2016.
  */

class ChainFlow[T](chainRef: ChainRef)
                  (implicit val system: ActorSystem, implicit val materializer: Materializer, hasherT: Hasher[T], hasherMR: Hasher[MerkledRef], hasherCRD: Hasher[ChainRefData]) {
  private val queries = PersistenceQuery(system).readJournalFor[LeveldbReadJournal](LeveldbReadJournal.Identifier)

  def envelopStream() = {
    val src: Source[EventEnvelope, NotUsed] = queries.eventsByPersistenceId(chainRef.hash.toString(), 0L, Long.MaxValue)
    src
  }

  def frameStream() = envelopStream().map(_.event.asInstanceOf[StateFrame[T]])

  def eventStream() = envelopStream().map(_.event.asInstanceOf[StateFrame[T]].value)

  def map[B](f: T => B, snapshotInterval: Int = 1000, maxInFlight: Int = 1000)(implicit hasherB: Hasher[B]): ChainFlow[B] = {
    val childChainRef = hasherCRD(ChainRefData(UUID.randomUUID(), chainRef.value.name + "\\map", 0))
    val sinkActor = Sink.actorSubscriber(Props(new ChainPersistentActor(childChainRef, None, snapshotInterval, maxInFlight)(hasherT, hasherMR)))

    val stream = eventStream().map(_.map(v => hasherB(f(v.value))).getOrElse(Hash.Zero)).runWith(sinkActor)

    new ChainFlow[B](childChainRef)
  }

  def filter(f: T => Boolean, snapshotInterval: Int = 1000, maxInFlight: Int = 1000): ChainFlow[T] = {
    val childChainRef = hasherCRD(ChainRefData(UUID.randomUUID(), chainRef.value.name + "\\filter", 0))
    val sinkActor = Sink.actorSubscriber(Props(new ChainPersistentActor[T](childChainRef, None, snapshotInterval, maxInFlight)))

    val stream = eventStream().filter(_.map(v => f(v.value)).getOrElse(false)).runWith(sinkActor)

    new ChainFlow[T](childChainRef)
  }

  def fold[B](f: (Option[B], Option[T]) => Option[B], initialValue: Option[B], snapshotInterval: Int = 1000, maxInFlight: Int = 1000)(implicit hasherB: Hasher[B]): ChainFlow[B] = {
    val childChainRef = hasherCRD(ChainRefData(UUID.randomUUID(), chainRef.value.name + "\\fold", 0))
    val sinkActor = Sink.actorSubscriber(Props(new ChainPersistentActor(childChainRef, initialValue, snapshotInterval, maxInFlight)))

    val stream = eventStream().fold[Option[HashedValue[B]]](initialValue.map(hasherB))((state, e) => f(state.map(_.value), e.map(_.value)).map(hasherB)).runWith(sinkActor)

    new ChainFlow[B](childChainRef)
  }

  def reduce(f: (Option[T], Option[T]) => Option[T], snapshotInterval: Int = 100, maxInFlight: Int = 1000): ChainFlow[T] = {
    fold[T](f, None, snapshotInterval, maxInFlight)
  }

  def groupBy(f: T => String, maxSubStreams: Int, initialValue: Option[T], snapshotInterval: Int = 1000, maxInFlight: Int = 1000): ChainFlow[T] = {
    val childChainRef = hasherCRD(ChainRefData(UUID.randomUUID(), chainRef.value.name + "\\groupBy", 0))
    val sinkActor = Sink.actorSubscriber(Props(new ChainGroupByNode[T](childChainRef, f, initialValue, snapshotInterval, maxInFlight)))

    val stream = eventStream().runWith(sinkActor)

    new ChainFlow[T](childChainRef)
  }

  def mapFrame[B](f: StateFrame[T] => B, initialValue: Option[B], snapshotInterval: Int = 1000, maxInFlight: Int = 1000)(implicit hasherB: Hasher[B]): ChainFlow[B] = {
    val childChainRef = hasherCRD(ChainRefData(UUID.randomUUID(), chainRef.value.name + "\\mapFrame", 0))
    val sinkActor = Sink.actorSubscriber(Props(new ChainPersistentActor(childChainRef, initialValue, snapshotInterval, maxInFlight)(hasherB, hasherMR)))

    val stream = frameStream().map(e => hasherB(f(e))).runWith(sinkActor)

    new ChainFlow[B](childChainRef)
  }

  def filterFrame(f: StateFrame[T] => Boolean, initialValue: Option[T], snapshotInterval: Int = 1000, maxInFlight: Int = 1000): ChainFlow[T] = {
    val childChainRef = hasherCRD(ChainRefData(UUID.randomUUID(), chainRef.value.name + "\\filterFrame", 0))
    val sinkActor = Sink.actorSubscriber(Props(new ChainPersistentActor(childChainRef, initialValue, snapshotInterval, maxInFlight)(hasherT, hasherMR)))

    val stream = frameStream().filter(e => f(e)).runWith(sinkActor)

    new ChainFlow[T](childChainRef)
  }

  def foldFrame[B](f: (StateFrame[B], StateFrame[T]) => Option[B], initialValue: Option[B], snapshotInterval: Int = 1000, maxInFlight: Int = 1000)
                  (implicit hasherB: Hasher[B]): ChainFlow[B] = {
    val childChainRef = hasherCRD(ChainRefData(UUID.randomUUID(), chainRef.value.name + "\\fold", 0))
    val sinkActor = Sink.actorSubscriber(Props(new ChainPersistentActor(childChainRef, initialValue, snapshotInterval, maxInFlight)(hasherB, hasherMR)))

    val stream = frameStream().fold[StateFrame[B]](FrameBuilder.buildInitialFrame(childChainRef, initialValue))((state: StateFrame[B], e: StateFrame[T]) => state).
      runWith(sinkActor)

//    val stream = frameStream().fold[StateFrame[B]]((FrameBuilder.buildInitialFrame(node, childChainRef, initialValue))
//      ((state: StateFrame[B], e: StateFrame[T]) =>
//        f(state, e).
//          map(v => FrameBuilder.buildFrame(node, childChainRef, state, node.hasher(v))).
//          getOrElse(state))).
//      runWith(sinkActor)


    new ChainFlow[B](childChainRef)
  }
}

object ChainFlow {
  def create[T](name: String, source: Source[T, NotUsed], initialValue: Option[T], snapshotInterval: Int = 1000, maxInFlight: Int = 1000)
               (implicit system: ActorSystem, materializer: Materializer, hasherT: Hasher[T], hasherMR: Hasher[MerkledRef], hasherCRD: Hasher[ChainRefData]) : ChainFlow[T] = {
    val childChainRef = hasherCRD(ChainRefData(UUID.randomUUID(), name, 0))
    val sinkActor = Sink.actorSubscriber(Props(new ChainPersistentActor[T](childChainRef, initialValue, snapshotInterval, maxInFlight)(hasherT, hasherMR)))

    val stream = source.map(e => hasherT(e)).runWith(sinkActor)

    new ChainFlow[T](childChainRef)
  }
}
