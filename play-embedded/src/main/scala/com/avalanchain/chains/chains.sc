import java.security.PublicKey
import java.util.UUID

import akka.NotUsed
import akka.actor.{Actor, ActorRef, ActorSelection, ActorSystem, Inbox, Props}
import akka.persistence.query.EventEnvelope
import akka.persistence.{PersistentActor, SnapshotOffer}
import akka.stream.{ActorMaterializer, OverflowStrategy}
import akka.stream.actor.{ActorSubscriber, ActorSubscriberMessage, MaxInFlightRequestStrategy}
import akka.stream.scaladsl.{Keep, Sink, Source}
import com.typesafe.config.ConfigFactory

import scala.concurrent.Future
import scala.reflect.ClassTag

//type PublicKey = byte array
type SigningPublicKey = PublicKey
type EncryptionPublicKey = PublicKey

trait ExecGroup

//trait NodeSelectionStrategy
//object NodeSelectionStrategy {
//  case class FixedMinimum(minNodes: Int) extends ExecPolicy
//}

trait ExecPolicy
object ExecPolicy {
  case class Pass() extends ExecPolicy
  case class FixedMinimum(minNodes: Int) extends ExecPolicy
  //case class Pass() extends ExecPolicy
}


object ChainStream {
  type Id = UUID
  type Version = Long
  type Hash = String//Array[Byte]
  type Serialized = String//Array[Byte]
  type Proof = String//Array[Byte]
}

import ChainStream._

trait Hashed {
  val hash: Hash
  val bytes: Serialized
}

case class HashedValue[T] (hash: Hash, bytes: Serialized, value: T) extends Hashed


case class ChainRefData(id: Id, name: String, ver: Version)
type ChainRef = HashedValue[ChainRefData]
//case class ChainRef (override val hash: Hash, override val bytes: Serialized, override val value: ChainRefData)
//  extends HashedValue[ChainRefData](hash, bytes, value)
//object ChainRef {
//  def UID = this.hash.toString()
//}

case class ChainDefData(ref: ChainRef, execGroups: Set[ExecGroup])
type ChainDef = HashedValue[ChainDefData]

//val cr = ChainRefData(UUID.randomUUID(), 0, "H1")
//println (cr.id)



//case class Data[T](value: HashedValue[T])

case class MerkledRef(streamRefHash: Hash, pmHash: Hash, pos: Version, ownHash: Hash)
type HashedMR = HashedValue[MerkledRef]

trait StateFrame[T] {
  val mref: HashedMR
  val value: HashedValue[T]
  def pos = mref.value.pos
}
object StateFrame {
  case class InitialFrame[T](override val mref: HashedMR, override val value: HashedValue[T]) extends StateFrame[T]
  case class Frame[T](override val mref: HashedMR, override val value: HashedValue[T]) // add proofs?
    extends StateFrame[T]
}

type Hasher[T] = T => HashedValue[T]
type Serializer[T] = T => Serialized
type Signer[T] = T => Proof

trait Node {
  def hasher[T]: Hasher[T]
  def serializer[T]: Serializer[T]
  def signer[T]: Signer[T]
}


///////
val config =
  """
    |akka {
    |
    |  # Loggers to register at boot time (akka.event.Logging$DefaultLogger logs
    |  # to STDOUT)
    |  loggers = ["akka.event.slf4j.Slf4jLogger"]
    |
    |  # Log level used by the configured loggers (see "loggers") as soon
    |  # as they have been started; before that, see "stdout-loglevel"
    |  # Options: OFF, ERROR, WARNING, INFO, DEBUG
    |  loglevel = "DEBUG"
    |
    |  # Log level for the very basic logger activated during ActorSystem startup.
    |  # This logger prints the log messages to stdout (System.out).
    |  # Options: OFF, ERROR, WARNING, INFO, DEBUG
    |  stdout-loglevel = "DEBUG"
    |
    |  # Filter of log events that is used by the LoggingAdapter before
    |  # publishing log events to the eventStream.
    |  logging-filter = "akka.event.slf4j.Slf4jLoggingFilter"
    |
    |  persistence.journal.plugin = "akka.persistence.journal.leveldb"
    |  persistence.snapshot-store.plugin = "akka.persistence.snapshot-store.local"
    |
    |  persistence.journal.leveldb.dir = "target/state/journal"
    |  persistence.snapshot-store.local.dir = "target/state/snapshots"
    |
    |# DO NOT USE THIS IN PRODUCTION !!!
    |# See also https://github.com/typesafehub/activator/issues/287
    |  persistence.journal.leveldb.native = false
    |
    |}
  """.stripMargin

implicit val system = ActorSystem("test-akka-sys", ConfigFactory.parseString(config))
implicit val materializer = ActorMaterializer()


//import com.roundeights.hasher.Hasher
import scala.language.postfixOps

class SimpleNode extends Node {
  override def hasher[T]: Hasher[T] = data => {
    val bytes = this.serializer[T](data)
    val hash = com.roundeights.hasher.Hasher(bytes).sha256
    HashedValue(hash, bytes, data)
  }

  override def signer[T]: Signer[T] = data => "Proof: {" + this.serializer[T](data) + "}"

  override def serializer[T]: Serializer[T] = data => data.toString()
}

class ChainPersistentActor[T](node: Node, val chainRef: ChainRef, val snapshotInterval: Int, initial: T) extends PersistentActor {
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

class ChainStreamNode[T](node: Node, val chainRef: ChainRef, val snapshotInterval: Int, initial: T) extends ActorSubscriber {
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

class ChainGroupByNode[T](node: Node, val chainRef: ChainRef, val snapshotInterval: Int, initial: T, keySelector: T => String) extends ActorSubscriber {
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

val simpleNode = new SimpleNode

/*val sourceFromRange = Source(1 to 1000)
val sourceFromIterable = Source(List(1,2,3))
val sourceFromFuture = Source.fromFuture(Future.successful("hello"))
val sourceWithSingleElement = Source.single("just one")
val sourceEmittingTheSameElement = Source.repeat("again and again")
val emptySource = Source.empty
val sourceFromQueue = Source.queue[String](10, OverflowStrategy.backpressure)


val sinkPrintingOutElements = Sink.foreach[Int](println(_))
val sinkCalculatingASumOfElements = Sink.fold[Int, Int](0)(_ + _)
val sinkReturningTheFirstElement = Sink.head
val sinkNoop = Sink.ignore

val a = sourceFromRange.runWith(sinkPrintingOutElements)


val chainRef = simpleNode.hasher(ChainRefData(UUID.randomUUID(), "test", 0))
//val chainRef = ChainRef(chainRef_.hash, chainRef_.bytes, chainRef_.value) // Hack
val sinkActor = Sink.actorSubscriber(Props(new ChainStreamNode[String](simpleNode, chainRef, 10, "")))

val ssa = sourceFromQueue.toMat(sinkActor)(Keep.both).run()
ssa._1.offer("1")*/

//val readSource = Source()

import akka.persistence.query.PersistenceQuery
import akka.persistence.query.journal.leveldb.scaladsl.LeveldbReadJournal


//val queries = PersistenceQuery(system).readJournalFor[LeveldbReadJournal](
//  LeveldbReadJournal.Identifier)
//
//val src: Source[EventEnvelope, NotUsed] =
//  //queries.eventsByPersistenceId("sample-id-1", 0L, Long.MaxValue)
//  //queries.eventsByPersistenceId("__1__", 0L, Long.MaxValue)
//  queries.eventsByPersistenceId(chainRef.hash.toString(), 0L, Long.MaxValue)
//
//val query = src.runWith(Sink.foreach(x => {
//  println(s"query - ${x}")
//  val hashedValue = x.asInstanceOf[HashedValue[String]]
//
//}))

//implicit val system = system1
//implicit val materializer = ActorMaterializer()


//val sourceFromQueue1 = Source.queue[String](10, OverflowStrategy.backpressure).toMat(Sink.foreach(println))(Keep.both).run()
//sourceFromQueue1._1.offer("")
//
//sourceFromQueue1._1.offer("1")

//////////////////

//class ChainFlow[T](source: Source[T, NotUsed], node: Node, chainRef: ChainRef) {
class ChainFlow[T](node: Node, chainRef: ChainRef) {
  private val queries = PersistenceQuery(system).readJournalFor[LeveldbReadJournal](LeveldbReadJournal.Identifier)

  def envelopStream() = {
    val src: Source[EventEnvelope, NotUsed] = queries.eventsByPersistenceId(chainRef.hash.toString(), 0L, Long.MaxValue)
    src
  }

  def frameStream() = envelopStream().map(_.event.asInstanceOf[StateFrame[T]])

  def eventStream() = envelopStream().map(_.event.asInstanceOf[StateFrame[T]].value)

  def map[B](f: T => B, initialValue: B, snapshotInterval: Int = 100): ChainFlow[B] = {
    val childChainRef = simpleNode.hasher(ChainRefData(UUID.randomUUID(), chainRef.value.name + "\\map", 0))
    val sinkActor = Sink.actorSubscriber(Props(new ChainStreamNode(simpleNode, childChainRef, snapshotInterval, initialValue)))

    val stream = eventStream().map(e => node.hasher(f(e.value))).runWith(sinkActor)

    new ChainFlow[B](node, childChainRef)
  }

  def filter(f: T => Boolean, initialValue: T, snapshotInterval: Int = 100): ChainFlow[T] = {
    val childChainRef = simpleNode.hasher(ChainRefData(UUID.randomUUID(), chainRef.value.name + "\\filter", 0))
    val sinkActor = Sink.actorSubscriber(Props(new ChainStreamNode[T](simpleNode, childChainRef, snapshotInterval, initialValue)))

    val stream = eventStream().filter(e => f(e.value)).runWith(sinkActor)

    new ChainFlow[T](node, childChainRef)
  }

  def fold[B](f: B => T => B, initialValue: B, snapshotInterval: Int = 100): ChainFlow[B] = {
    val childChainRef = simpleNode.hasher(ChainRefData(UUID.randomUUID(), chainRef.value.name + "\\fold", 0))
    val sinkActor = Sink.actorSubscriber(Props(new ChainStreamNode(simpleNode, childChainRef, snapshotInterval, initialValue)))

    val stream = eventStream().fold(node.hasher(initialValue))((state, e) => node.hasher(f(state.value) (e.value))).runWith(sinkActor)

    new ChainFlow[B](node, childChainRef)
  }

//  def reduce(f: T => T => T, snapshotInterval: Int = 100): ChainFlow[T] = {
//    fold[T](f, null, snapshotInterval)
//  }

  def groupBy(f: T => String, maxSubStreams: Int, initialValue: T, snapshotInterval: Int = 100): ChainFlow[T] = {
    val childChainRef = simpleNode.hasher(ChainRefData(UUID.randomUUID(), chainRef.value.name + "\\groupBy", 0))
    val sinkActor = Sink.actorSubscriber(Props(new ChainGroupByNode[T](simpleNode, childChainRef, snapshotInterval, initialValue, f)))

    val stream = eventStream().runWith(sinkActor)

    new ChainFlow[T](node, childChainRef)
  }

  def mapFrame[B](f: StateFrame[T] => B, initialValue: B, snapshotInterval: Int = 100): ChainFlow[B] = {
    val childChainRef = simpleNode.hasher(ChainRefData(UUID.randomUUID(), chainRef.value.name + "\\mapFrame", 0))
    val sinkActor = Sink.actorSubscriber(Props(new ChainStreamNode(simpleNode, childChainRef, snapshotInterval, initialValue)))

    val stream = frameStream().map(e => node.hasher(f(e))).runWith(sinkActor)

    new ChainFlow[B](node, childChainRef)
  }

  def filterFrame(f: StateFrame[T] => Boolean, initialValue: T, snapshotInterval: Int = 100): ChainFlow[T] = {
    val childChainRef = simpleNode.hasher(ChainRefData(UUID.randomUUID(), chainRef.value.name + "\\filterFrame", 0))
    val sinkActor = Sink.actorSubscriber(Props(new ChainStreamNode(simpleNode, childChainRef, snapshotInterval, initialValue)))

    val stream = frameStream().filter(e => f(e)).runWith(sinkActor)

    new ChainFlow[T](node, childChainRef)
  }

  def foldFrame[B](f: B => StateFrame[T] => B, initialValue: B, snapshotInterval: Int = 100): ChainFlow[B] = {
    val childChainRef = simpleNode.hasher(ChainRefData(UUID.randomUUID(), chainRef.value.name + "\\fold", 0))
    val sinkActor = Sink.actorSubscriber(Props(new ChainStreamNode(simpleNode, childChainRef, snapshotInterval, initialValue)))

    val stream = frameStream().fold(node.hasher(initialValue))((state, e) => node.hasher(f(state.value) (e))).runWith(sinkActor)

    new ChainFlow[B](node, childChainRef)
  }
}

object ChainFlow {
  def create[T](node: Node, name: String, source: Source[T, NotUsed], initialValue: T, snapshotInterval: Int = 100): ChainFlow[T] = {
    val childChainRef = simpleNode.hasher(ChainRefData(UUID.randomUUID(), name, 0))
    val sinkActor = Sink.actorSubscriber(Props(new ChainStreamNode[T](simpleNode, childChainRef, snapshotInterval, initialValue)))

    val stream = source.map(e => node.hasher(e)).runWith(sinkActor)

    new ChainFlow[T](node, childChainRef)
  }
}

val simpleStream = ChainFlow.create[Int](simpleNode, "strings", Source[Int](1 to 1000), 0)

val filtered = simpleStream.filter(_ % 10 == 0, 0)

val mapped = filtered.map(_ / 10, 0).groupBy(x => (x % 10).toString(), 10, 0)