import java.security.PublicKey
import java.time.Instant
import java.util.UUID

import akka.NotUsed
import akka.actor.{Actor, ActorLogging, ActorRef, ActorSelection, ActorSystem, Inbox, Props}
import akka.persistence.fsm.PersistentFSM
import akka.persistence.fsm.PersistentFSM.FSMState
import akka.persistence.query.EventEnvelope
import akka.persistence.{PersistentActor, SnapshotOffer}
import akka.stream.{ActorMaterializer, OverflowStrategy}
import akka.stream.actor.{ActorSubscriber, ActorSubscriberMessage, MaxInFlightRequestStrategy}
import akka.stream.scaladsl.{Keep, Sink, Source}
import com.typesafe.config.ConfigFactory

import scala.annotation.tailrec
import scala.concurrent.Future
import scala.reflect.ClassTag
import scala.util.Try

type PublicKey = String // TODO: Replace with java.security.PublicKey
type SigningPublicKey = PublicKey
type EncryptionPublicKey = PublicKey

trait ExecGroup

//trait NodeSelectionStrategy
//object NodeSelectionStrategy {
//  case class FixedMinimum(minNodes: Int) extends ExecutionPolicy
//}

trait ExecutionPolicy
object ExecPolicy {
  case class Pass() extends ExecutionPolicy
  case class FixedMinimum(minNodes: Int) extends ExecutionPolicy
  //case class Pass() extends ExecutionPolicy
}


object ChainStream {
  type Id = UUID
  type Version = Long
  type Hash = String//Array[Byte]
  type Serialized = String//Array[Byte]
  type Signature = String
  type SigningPublicKey = String
  case class Proof (signature: Signature, hash: Hash)
  case class Signed[T] (proof: Proof, value: T)
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
type Verifier[T] = Proof => T => Boolean

trait CryptoContext {
  def hasher[T]: Hasher[T]
  def serializer[T]: Serializer[T]
  def signer[T]: Signer[T]
  def signingPublicKey: SigningPublicKey
}

type ValueId = String
type ConfirmationStateChangedNotifier = Hash => Unit
case class Confirmation (nodeId: String, valueId: ValueId, value: Hash, notifier: ConfirmationStateChangedNotifier)

trait ConfirmationResult {
  val confirmation: Confirmation
}
object ConfirmationResult {
  case class InvalidConfirmation(confirmation: Confirmation) extends ConfirmationResult
  case class ConfirmedSame(confirmation: Confirmation) extends ConfirmationResult
  case class ConfirmedDifferent(confirmation: Confirmation, expectedHash: Hash) extends ConfirmationResult
  case class NotConfirmedYet(confirmation: Confirmation) extends ConfirmationResult
}

type PolicyChecker = ExecutionPolicy => List[Confirmation] => Option[Hash]
class ConfirmationCounter (val policy: ExecutionPolicy, validator: Confirmation => Boolean, policyChecker: PolicyChecker) {
  private var _confirmations = List.empty[Confirmation]
  private var _invalidConfirmations = List.empty[Confirmation]
  private var _pendingConfirmations = List.empty[Confirmation]
  private var _confirmedValue: Option[Hash] = None

  private def notifyDependents(): Unit = {
    _confirmedValue match {
      case Some(v) => _confirmations.foreach(c => c.notifier(v))
    }
  }

  def confirmations = _confirmations
  def invalidConfirmations = _invalidConfirmations
  def pendingConfirmations = _pendingConfirmations
  def confirmedValue = _confirmedValue

  def addConfirmation(confirmation: Confirmation) = {
    if (!validator(confirmation)) {
      _invalidConfirmations = confirmation :: _invalidConfirmations
      ConfirmationResult.InvalidConfirmation(confirmation)
    }
    else {
      _confirmations = confirmation :: _confirmations
      _confirmedValue match {
        case Some(v) if v.equals(confirmation.value) => ConfirmationResult.ConfirmedSame(confirmation)
        case Some(v) => ConfirmationResult.ConfirmedDifferent(confirmation, v)
        case None => {
          _confirmedValue = policyChecker(policy) (_confirmations)
          _confirmedValue match {
            case Some(v) =>
              _confirmations = _pendingConfirmations
              _pendingConfirmations = List.empty[Confirmation]
              notifyDependents()
              if (v.equals(confirmation.value)) ConfirmationResult.ConfirmedSame(confirmation)
              else ConfirmationResult.ConfirmedDifferent(confirmation, v) // Shouldn't ever happen really because of policyChecker call
            case None =>
              _pendingConfirmations = confirmation :: _pendingConfirmations
              ConfirmationResult.NotConfirmedYet(confirmation)
          }
        }
      }
    }
  }
}

//////////////////

//final case class SetNumber(num: Integer)
//final case class Reset()

sealed trait ConfirmationState extends FSMState
case object Unconfirmed extends ConfirmationState {
  override def identifier: String = "Unconfirmed"
}
case object Confirmed extends ConfirmationState {
  override def identifier: String = "Confirmed"
}

sealed trait Data {
  def add(number: Integer): Data
  def empty(): Data
}
case object Empty extends Data {
  def add(number: Integer) = Numbers(Vector(number))
  def empty() = this
}
final case class Numbers(queue: Seq[Integer]) extends Data {
  def add(number: Integer) = Numbers(queue :+ number)
  def empty() = Empty
}

sealed trait DomainEvt
case class SetNumberEvt(num: Integer) extends DomainEvt
case class ResetEvt() extends DomainEvt

class ConfirmationActor() extends PersistentFSM[ConfirmationState, Data, DomainEvt] {

  override def applyEvent(domainEvent: DomainEvt, currentData: Data): Data = {
    domainEvent match {
      case SetNumberEvt(num) =>
        val data = currentData.add(num)
        println(data)
        data
      case ResetEvt() =>
        deleteMessages(1000)
        println("RESET")
        currentData.empty()
    }
  }

  override def persistenceId: String = "generator"

  override def domainEventClassTag: ClassTag[DomainEvt] = classTag[DomainEvt]

  startWith(Idle, Empty)

  when(Idle) {
    case Event(SetNumber(num), _) =>
      println("STARTING IDLE")
      goto(Active) applying SetNumberEvt(num)
    case Event(Reset, _) => goto(Active) applying ResetEvt()
  }

  when(Active) {
    case Event(SetNumber(num), numbers: Data) => stay applying SetNumberEvt(num)
    case Event(Reset, _) => goto(Idle) applying ResetEvt() replying "RESET COMPLETED"
  }

  initialize()

}

//class ConfirmationActor() extends Actor with ActorLogging {
//
//  def receive = {
//    case c: Confirmation =>
//    case a => println(a)
//  }
//}

case class CoordinationKey(pos: Int, )

class CoordinatorActor() extends Actor with ActorLogging {
  var

  def receive = {
    case c: Confirmation =>
    case a => println(a)
  }
}


////////////////// Payment

case class PaymentAccountRef (address: String) // TODO: Change to proper ref

type PaymentAmount = BigDecimal

trait PaymentAttempt
case class PaymentTransaction (from: PaymentAccountRef, to: List[(PaymentAccountRef, PaymentAmount)]) extends PaymentAttempt

//val pt = PaymentTransaction(PaymentAccountRef(""), List((PaymentAccountRef(""), BigDecimal(22))))

//type HashedPT = SignedProof<PaymentTransaction>

type PaymentBalances = Map[PaymentAccountRef, PaymentAmount]

trait PaymentRejection extends PaymentAttempt
object PaymentRejection {
  case class WrongHash (hash: Hash) extends PaymentRejection
  case class WrongSignature (signature: Signature) extends PaymentRejection
  case class FromAccountNotExists (account: PaymentAccountRef) extends PaymentRejection
  case class ToAccountsMissing() extends PaymentRejection
  case class UnexpectedNonPositiveAmount (amount: PaymentAmount) extends PaymentRejection
  case class UnexpectedNonPositiveTotal (amount: PaymentAmount) extends PaymentRejection
  case class NotEnoughFunds (available: PaymentAmount, expected: PaymentAmount) extends PaymentRejection
}

case class StoredTransaction (payment: PaymentAttempt, balances: PaymentBalances, timeStamp: Instant)

case class PaymentAccount (ref: PaymentAccountRef, publicKey: SigningPublicKey, name: String, cryptoContext: CryptoContext)

trait TransactionStorage {
  def all: (PaymentBalances, List[StoredTransaction]) // Initial balances + transactions
  def submit: PaymentTransaction => StoredTransaction
  def accountState: PaymentAccountRef => (Option[PaymentAmount], Seq[StoredTransaction]) // Initial balances + account transactions
  def paymentBalances: PaymentBalances
  def accounts: List[PaymentAccount]
  def newAccount: PaymentAccount
}

def signatureChecker(transaction: PaymentTransaction) = transaction // TODO: Add check

def applyTransaction (balances: PaymentBalances) (transaction: PaymentTransaction) : StoredTransaction = {
  def reject(reason: PaymentRejection) = StoredTransaction(reason, balances, Instant.now())
  val total = transaction.to.map(_._2).sum
  balances.get(transaction.from) match {
    case None => reject(PaymentRejection.FromAccountNotExists(transaction.from))
    case Some (_) if total <= 0 => reject(PaymentRejection.UnexpectedNonPositiveTotal(total))
    case Some (value) =>
      value match {
        case v if v < total => reject(PaymentRejection.NotEnoughFunds(total, v))
        case v =>
          @tailrec def applyTos(blns: PaymentBalances, tos: List[(PaymentAccountRef, PaymentAmount)]): StoredTransaction = {
            tos match {
              case Nil => StoredTransaction(transaction, blns, Instant.now())
              case t :: _ if t._2 < 0 => reject(PaymentRejection.UnexpectedNonPositiveAmount(t._2))
              case t :: ts =>
                val accountRef = t._1
                val existingBalance = blns.get(accountRef)
                val newToBlns = existingBalance match {
                  case None => blns.updated(accountRef, t._2)
                  case Some(eb) => blns.updated(accountRef, t._2 + eb)
                }
                val newFromBlns = newToBlns.updated(transaction.from, value - total)
                applyTos(newFromBlns, ts)
            }
          }
          applyTos(balances, transaction.to)
      }
  }

def newAccount(name: String, cryptoContext: CryptoContext) = {
  val accountRef = PaymentAccountRef(name)
  PaymentAccount(accountRef, cryptoContext.signingPublicKey, name, cryptoContext)
}

//  let rec applyTos (blns: PaymentBalancesData) tos: StoredTransaction =
//  match tos with
//  |[] -> {Result = ok (transaction);
//  Balances = {Balances = blns};
//  TimeStamp = DateTimeOffset.UtcNow}
//  | t :: _ when snd t < 0 m -> {Result = fail (UnexpectedNegativeAmount (snd t) );
//  Balances = balances;
//  TimeStamp = DateTimeOffset.UtcNow}
//  |
//  applyTos (balances.Balances) (transaction.To |> List.ofArray)
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

class SimpleNode extends CryptoContext {
  override def hasher[T]: Hasher[T] = data => {
    val bytes = this.serializer[T](data)
    val hash = com.roundeights.hasher.Hasher(bytes).sha256
    HashedValue(hash, bytes, data)
  }

  override def signer[T]: Signer[T] = data => Proof("Proof: {" + this.serializer[T](data) + "}", this.hasher[T](data).hash)

  override def serializer[T]: Serializer[T] = data => data.toString()

  override def signingPublicKey = "SigningPublicKey"
}

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
class ChainFlow[T](node: CryptoContext, chainRef: ChainRef) {
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
  def create[T](node: CryptoContext, name: String, source: Source[T, NotUsed], initialValue: T, snapshotInterval: Int = 100): ChainFlow[T] = {
    val childChainRef = simpleNode.hasher(ChainRefData(UUID.randomUUID(), name, 0))
    val sinkActor = Sink.actorSubscriber(Props(new ChainStreamNode[T](simpleNode, childChainRef, snapshotInterval, initialValue)))

    val stream = source.map(e => node.hasher(e)).runWith(sinkActor)

    new ChainFlow[T](node, childChainRef)
  }
}

val simpleStream = ChainFlow.create[Int](simpleNode, "strings", Source[Int](1 to 1000), 0)

val filtered = simpleStream.filter(_ % 10 == 0, 0)

val mapped = filtered.map(_ / 10, 0).groupBy(x => (x % 10).toString(), 10, 0)