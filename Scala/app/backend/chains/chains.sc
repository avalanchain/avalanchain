import java.security.PublicKey
import java.util.UUID

import ChainStream._
import akka.actor.{Actor, ActorRef, ActorSystem, Inbox, Props}
import akka.stream.ActorMaterializer
import akka.stream.scaladsl.{Sink, Source}

import scala.concurrent.Future

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


trait Hashed {
  val hash: Hash
  val bytes: Serialized
}

case class HashedValue[T] (hash: Hash, bytes: Serialized, value: T)
  extends Hashed


case class ChainRefData(id: Id, ver: Version, hash: Hash)
type ChainRef = HashedValue[ChainRefData]

case class ChainDefData(ref: ChainRef, execGroups: Set[ExecGroup])
type ChainDef = HashedValue[ChainDefData]

//val cr = ChainRefData(UUID.randomUUID(), 0, "H1")
//println (cr.id)



//case class Data[T](value: HashedValue[T])

abstract class StateFrame[T] (val pos: Version, val value: HashedValue[T])

object StateFrame {
  case class InitialFrame[T](override val value: HashedValue[T]) extends StateFrame(0, value)

  case class MerkledRef(streamRefHash: Hash, pmHash: Hash, pos: Version, ownHash: Hash)
  type HashedMR = HashedValue[MerkledRef]

  case class Frame[T](
     override val pos: Version,
     override val value: HashedValue[T],
     mref: HashedMR)
     // add proofs?
    extends StateFrame(pos, value)
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




//implicit val system = ActorSystem("test-akka-sys")
implicit val materializer = ActorMaterializer()


val sourceFromRange = Source(1 to 10)
val sourceFromIterable = Source(List(1,2,3))
val sourceFromFuture = Source(Future.successful("hello"))
val sourceWithSingleElement = Source.single("just one")
val sourceEmittingTheSameElement = Source.repeat("again and again")
val emptySource = Source.empty


val sinkPrintingOutElements = Sink.foreach[Int](println(_))
val sinkCalculatingASumOfElements = Sink.fold[Int, Int](0)(_ + _)
val sinkReturningTheFirstElement = Sink.head
val sinkNoop = Sink.ignore

val a = sourceFromRange.runWith(sinkPrintingOutElements)


