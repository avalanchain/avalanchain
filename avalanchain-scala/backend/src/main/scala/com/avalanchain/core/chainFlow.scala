package com.avalanchain

import java.util.UUID

import akka.NotUsed
import akka.actor.{Actor, ActorRef, ActorSystem, Props}
import akka.persistence.{PersistentActor, SnapshotOffer}
import akka.persistence.query.{EventEnvelope, PersistenceQuery}
import akka.persistence.query.journal.leveldb.scaladsl.LeveldbReadJournal
import akka.stream.actor.ActorSubscriberMessage.OnNext
import akka.stream.actor.{ActorSubscriber, ActorSubscriberMessage, MaxInFlightRequestStrategy}
import akka.stream.scaladsl.{Sink, Source}
import com.avalanchain.core.domain._

/**
  * Created by mytut on 19/04/2016.
  */
package object chainFlow {
  class ChainPersistentActor[T](node: CryptoContext, val chainRef: ChainRef, val snapshotInterval: Int, initial: T) extends PersistentActor {
    override def persistenceId = chainRef.hash.toString()

    private def merkledHasher = node.hasher[MerkledRef]

    private def buildInitialFrame: StateFrame[T] = {
      val hashed = node.hasher[T](initial)
      val mr = MerkledRef(chainRef.hash, "", 0, hashed.hash)
      StateFrame.InitialFrame[T](merkledHasher(mr), hashed).asInstanceOf[StateFrame[T]]
    }

    private def saveFrame(frame: StateFrame[T]) = {
      updateState(frame)
      //context.system.eventStream.publish(frame)
      println(s"saved ${frame}")
    }

    private var state: StateFrame[T] = buildInitialFrame
    //saveFrame(state)

    private def updateState(event: StateFrame[T]): Unit = {
      state = event
    }

    def currentState = state

    val receiveRecover: Receive = {
      case evt: StateFrame[T] => updateState(evt)
      case SnapshotOffer(_, snapshot: StateFrame[T]) => state = snapshot
    }

    val receiveCommand: Receive = {
      case data: HashedValue[T] =>
        val mr = MerkledRef(chainRef.hash, state.mref.hash, state.pos + 1, data.hash)
        val newFrame = StateFrame.Frame[T](merkledHasher(mr), data)
        persist(newFrame) (e => saveFrame(newFrame))
        if (mr.pos != 0 && mr.pos % snapshotInterval == 0) saveSnapshot(newFrame)
      //    case data: T =>
      //      val hashedData = node.hasher(data)
      //      val mr = MerkledRef(chainRef.hash, state.mref.hash, state.pos + 1, hashedData.hash)
      //      val newFrame = StateFrame.Frame[T](merkledHasher(mr), hashedData)
      //      persist(newFrame) (e => saveFrame(newFrame))
      //      if (mr.pos != 0 && mr.pos % snapshotInterval == 0) saveSnapshot(newFrame)
      case "print" => println(state)
      case a => println (s"Ignored '$a'")
    }
  }

  class ChainStreamNode[T](node: CryptoContext, val chainRef: ChainRef, val snapshotInterval: Int, initial: T) extends ActorSubscriber {
    import ActorSubscriberMessage._

    val MaxQueueSize = 10
    var queue = Map.empty[Int, ActorRef]
    val persistence = context.actorOf(Props(new ChainPersistentActor[T](node, chainRef, snapshotInterval, initial)), name = "persistence")

    override val requestStrategy = new MaxInFlightRequestStrategy(max = MaxQueueSize) {
      override def inFlightInternally: Int = queue.size
    }

    def receive = {
      case OnNext(msg) =>
        persistence.forward(msg)
    }
  }

  class ChainGroupByNode[T](node: CryptoContext, val chainRef: ChainRef, val snapshotInterval: Int, initial: T, keySelector: T => String) extends ActorSubscriber {
    import ActorSubscriberMessage._

    val MaxQueueSize = 10
    var queue = Map.empty[Int, ActorRef]
    def getChild(name: String) = {
      context.child(name) match {
        case Some(ch) => ch
        case None => context.actorOf(Props(new ChainPersistentActor[T](node, chainRef, snapshotInterval, initial)), name)
      }
    }

    override val requestStrategy = new MaxInFlightRequestStrategy(max = MaxQueueSize) {
      override def inFlightInternally: Int = queue.size
    }

    def receive = {
      case OnNext(msg: HashedValue[T]) =>
        getChild(keySelector(msg.value)).forward(msg)
    }
  }

  class ChainFlow[T](node: CryptoContext, chainRef: ChainRef, implicit val system: ActorSystem) {
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

      new ChainFlow[B](node, childChainRef, system)
    }

    def filter(f: T => Boolean, initialValue: T, snapshotInterval: Int = 100): ChainFlow[T] = {
      val childChainRef = node.hasher(ChainRefData(UUID.randomUUID(), chainRef.value.name + "\\filter", 0))
      val sinkActor = Sink.actorSubscriber(Props(new ChainStreamNode[T](node, childChainRef, snapshotInterval, initialValue)))

      val stream = eventStream().filter(e => f(e.value)).runWith(sinkActor)

      new ChainFlow[T](node, childChainRef, system)
    }

    def fold[B](f: B => T => B, initialValue: B, snapshotInterval: Int = 100): ChainFlow[B] = {
      val childChainRef = node.hasher(ChainRefData(UUID.randomUUID(), chainRef.value.name + "\\fold", 0))
      val sinkActor = Sink.actorSubscriber(Props(new ChainStreamNode(node, childChainRef, snapshotInterval, initialValue)))

      val stream = eventStream().fold(node.hasher(initialValue))((state, e) => node.hasher(f(state.value) (e.value))).runWith(sinkActor)

      new ChainFlow[B](node, childChainRef, system)
    }

    //  def reduce(f: T => T => T, snapshotInterval: Int = 100): ChainFlow[T] = {
    //    fold[T](f, null, snapshotInterval)
    //  }

    def groupBy(f: T => String, maxSubStreams: Int, initialValue: T, snapshotInterval: Int = 100): ChainFlow[T] = {
      val childChainRef = node.hasher(ChainRefData(UUID.randomUUID(), chainRef.value.name + "\\groupBy", 0))
      val sinkActor = Sink.actorSubscriber(Props(new ChainGroupByNode[T](node, childChainRef, snapshotInterval, initialValue, f)))

      val stream = eventStream().runWith(sinkActor)

      new ChainFlow[T](node, childChainRef, system)
    }

    def mapFrame[B](f: StateFrame[T] => B, initialValue: B, snapshotInterval: Int = 100): ChainFlow[B] = {
      val childChainRef = node.hasher(ChainRefData(UUID.randomUUID(), chainRef.value.name + "\\mapFrame", 0))
      val sinkActor = Sink.actorSubscriber(Props(new ChainStreamNode(node, childChainRef, snapshotInterval, initialValue)))

      val stream = frameStream().map(e => node.hasher(f(e))).runWith(sinkActor)

      new ChainFlow[B](node, childChainRef, system)
    }

    def filterFrame(f: StateFrame[T] => Boolean, initialValue: T, snapshotInterval: Int = 100): ChainFlow[T] = {
      val childChainRef = node.hasher(ChainRefData(UUID.randomUUID(), chainRef.value.name + "\\filterFrame", 0))
      val sinkActor = Sink.actorSubscriber(Props(new ChainStreamNode(node, childChainRef, snapshotInterval, initialValue)))

      val stream = frameStream().filter(e => f(e)).runWith(sinkActor)

      new ChainFlow[T](node, childChainRef, system)
    }

    def foldFrame[B](f: B => StateFrame[T] => B, initialValue: B, snapshotInterval: Int = 100): ChainFlow[B] = {
      val childChainRef = node.hasher(ChainRefData(UUID.randomUUID(), chainRef.value.name + "\\fold", 0))
      val sinkActor = Sink.actorSubscriber(Props(new ChainStreamNode(node, childChainRef, snapshotInterval, initialValue)))

      val stream = frameStream().fold(node.hasher(initialValue))((state, e) => node.hasher(f(state.value) (e))).runWith(sinkActor)

      new ChainFlow[B](node, childChainRef, system)
    }
  }

  object ChainFlow {
    def create[T](node: CryptoContext, name: String, source: Source[T, NotUsed], initialValue: T, snapshotInterval: Int = 100)(implicit system: ActorSystem): ChainFlow[T] = {
      val childChainRef = node.hasher(ChainRefData(UUID.randomUUID(), name, 0))
      val sinkActor = Sink.actorSubscriber(Props(new ChainStreamNode[T](node, childChainRef, snapshotInterval, initialValue)))

      val stream = source.map(e => node.hasher(e)).runWith(sinkActor)

      new ChainFlow[T](node, childChainRef, system)
    }
  }
}
