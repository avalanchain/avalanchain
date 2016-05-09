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

  sealed trait ExecutionPolicy

  object ExecPolicy {
    final case class Pass() extends ExecutionPolicy
    final case class FixedMinimum(minNodes: Int) extends ExecutionPolicy
    //case class Pass() extends ExecutionPolicy
  }


  object ChainStream {
    type Id = UUID
    type Version = Long
    //Array[Byte]
    type Serialized = String
    //Array[Byte]
    type Signature = String
    type SigningPublicKey = String

    final case class Hash(hash: String) {
      override def toString = hash
    }
    object Hash {
      val Zero = Hash("")
    }

    final case class Proof(signature: Signature, hash: Hash)

    final case class Signed[T](proof: Proof, value: T)

  }

  import ChainStream._

  trait Hashed {
    val hash: Hash
    val bytes: Serialized
  }

  final case class HashedValue[T](hash: Hash, bytes: Serialized, value: T) extends Hashed

  final case class ChainRefData(id: Id, name: String, ver: Version)
  type ChainRef = HashedValue[ChainRefData]

  //case class ChainRef (override val hash: Hash, override val bytes: Serialized, override val value: ChainRefData)
  //  extends HashedValue[ChainRefData](hash, bytes, value)
  //object ChainRef {
  //  def UID = this.hash.toString()
  //}

  final case class ChainDefData(ref: ChainRef, execGroups: Set[ExecGroup])
  type ChainDef = HashedValue[ChainDefData]

  //case class Data[T](value: HashedValue[T])

  final case class MerkledRef(streamRefHash: Hash, pmHash: Hash, pos: Version, ownHash: Hash)

  type HashedMR = HashedValue[MerkledRef]

  trait StateFrame[T] {
    val mref: HashedMR
    val value: Option[HashedValue[T]]

    def pos = mref.value.pos
  }

  object StateFrame {
    case class InitialFrame[T](override val mref: HashedMR, override val value: Option[HashedValue[T]]) extends StateFrame[T]
    case class Frame[T](override val mref: HashedMR, override val value: Option[HashedValue[T]]) // add proofs?
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

  object FrameBuilder {
    def buildNestedRef(node: CryptoContext, cr: ChainRef, nestedName: String): ChainRef = {
      val data = cr.value
      val newData = data.copy(id = UUID.randomUUID(), name = data.name + "/" + nestedName, ver = data.ver)
      node.hasher(newData)
    }

    def buildInitialFrame[T](node: CryptoContext, cr: ChainRef, initial: Option[T]): StateFrame[T] = {
      val hashed = initial.map(node.hasher)
      val mr = MerkledRef(cr.hash, Hash.Zero, 0, hashed.map(_.hash).getOrElse(Hash.Zero))
      StateFrame.InitialFrame[T](node.hasher(mr), hashed).asInstanceOf[StateFrame[T]]
    }

    def buildFrame[T](node: CryptoContext, cr: ChainRef, state: StateFrame[T], data: T): StateFrame[T] = {
      val hashedData = node.hasher(data)
      val mr = MerkledRef(cr.hash, state.mref.hash, state.pos + 1, hashedData.hash)
      StateFrame.Frame[T](node.hasher(mr), Some(hashedData))
    }
  }
}
