package com.avalanchain.core
/**
  * Created by Yuriy Habarov on 19/04/2016.
  */

import java.util.UUID

package object domain {
  type PublicKey = String
  // TODO: Replace with java.security.PublicKey
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
    type Hash = String
    //Array[Byte]
    type Serialized = String
    //Array[Byte]
    type Signature = String
    type SigningPublicKey = String

    case class Proof(signature: Signature, hash: Hash)

    case class Signed[T](proof: Proof, value: T)

  }

  import ChainStream._

  trait Hashed {
    val hash: Hash
    val bytes: Serialized
  }

  case class HashedValue[T](hash: Hash, bytes: Serialized, value: T) extends Hashed

  case class ChainRefData(id: Id, name: String, ver: Version)
  type ChainRef = HashedValue[ChainRefData]

  //case class ChainRef (override val hash: Hash, override val bytes: Serialized, override val value: ChainRefData)
  //  extends HashedValue[ChainRefData](hash, bytes, value)
  //object ChainRef {
  //  def UID = this.hash.toString()
  //}

  case class ChainDefData(ref: ChainRef, execGroups: Set[ExecGroup])
  type ChainDef = HashedValue[ChainDefData]

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

  sealed trait Verified[T] {
    val value: T
  }
  object Verified {
    final case class Passed[T](value: T) extends Verified[T]
    final case class HashCheckFailed[T](value: T, actual: Hash, expected: Hash) extends Verified[T]
    final case class ProofCheckFailed[T](override val value: T, actual: Hash, expected: Hash) extends Verified[T]
  }


  type Hasher[T] = T => HashedValue[T]
  type Serializer[T] = T => Serialized
  type Signer[T] = T => Signed[T]
  type Verifier[T] = (Proof, T) => Verified[T]

  trait CryptoContext {
    def hasher[T]: Hasher[T]
    def serializer[T]: Serializer[T]
    def signer[T]: Signer[T]
    def signingPublicKey: SigningPublicKey
  }
}
