import java.util.UUID

import ChainStream._

trait Node

trait ExecGroup

trait ExecPolicy
object ExecPolicy {
  
}


object ChainStream {
  type Id = UUID
  type Version = Long
  type Hash = String//Array[Byte]
  type Serialized = String//Array[Byte]

}



case class ChainRef(id: Id, ver: Version, hash: Hash)

val cr = ChainRef(UUID.randomUUID(), 0, "H1")
//println (cr.id)


trait Hashed {
  val hash: Hash
  val bytes: Serialized
}

case class HashedValue[T] (hash: Hash, bytes: Serialized, value: T)
  extends Hashed


//case class Data[T](value: HashedValue[T])

abstract class StateFrame[T] (val pos: Version, val value: HashedValue[T]) {

}

object StateFrame {
  case class InitialFrame[T](override val value: HashedValue[T]) extends StateFrame(0, value)
  case class Frame[T](
     override val pos: Version,
     override val value: HashedValue[T],
     pmHash: Hash,
     mHash: Hash) // add proofs?
    extends StateFrame(pos, value)
}

