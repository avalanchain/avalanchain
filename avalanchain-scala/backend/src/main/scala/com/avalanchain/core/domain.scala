package com.avalanchain.core
/**
  * Created by Yuriy Habarov on 19/04/2016.
  */

import java.util.UUID

import akka.util.ByteString
import com.avalanchain.core.domain.Proofed.Signed
import io.circe.generic.JsonCodec

package object domain {

  trait ExecGroup

  //trait NodeSelectionStrategy
  //object NodeSelectionStrategy {
  //  case class FixedMinimum(minNodes: Int) extends ExecutionPolicy
  //}

  sealed trait ExecutionPolicy

  object ExecutionPolicy {
    final case class Pass() extends ExecutionPolicy
    final case class FixedMinimum(minNodes: Int) extends ExecutionPolicy
  }


  type Id = UUID
  type Version = Long

  type ByteWord = ByteString
  object ByteWord {
    def apply(b: Array[Byte]): ByteWord = ByteString(b)
  }

  type TextSerialized = String
  type BytesSerialized = ByteString
  type Serialized = (TextSerialized, BytesSerialized)
  type Hexed = TextSerialized

  type TextSerializer[T] = T => TextSerialized
  type BytesSerializer[T] = T => BytesSerialized
  type String2Bytes = TextSerialized => BytesSerialized

  type TextDeserializer[T] = TextSerialized => T
  type BytesDeserializer[T] = BytesSerialized => T
  type Bytes2String = BytesSerialized => Hexed

  type Hasher = ByteWord => HashedValue
  type Bytes2Hexed = BytesSerialized => Hexed
  type Hexed2Bytes = Hexed => BytesSerialized

//  type KeyBytes = BytesSerialized
//  type HashBytes = BytesSerialized
//  type SignatureBytes = BytesSerialized
//  type ValueBytes = BytesSerialized
  type KeyBytes = ByteWord
  type HashBytes = ByteWord
  type SignatureBytes = ByteWord
  type ValueBytes = ByteWord
  sealed trait SecurityKey {
    def key: KeyBytes
    def toHexed(implicit bytes2Hexed: Bytes2Hexed) = bytes2Hexed(key)
  }
  // TODO: Replace with java.security.PublicKey
  // TODO: Make constructor private and use sealed trait
  final case class PublicKey(key: KeyBytes) extends SecurityKey {
    def this(key: Hexed)(implicit hexed2Bytes: Hexed2Bytes) = this(hexed2Bytes(key))
  }
  object PublicKey {
    def apply(key: Hexed)(implicit hexed2Bytes: Hexed2Bytes) = new PublicKey(key)
  }

  final case class PrivateKey(key: KeyBytes) extends SecurityKey {
    def this(key: Hexed)(implicit hexed2Bytes: Hexed2Bytes) = this(hexed2Bytes(key))
  }
  object PrivateKey {
    def apply(key: Hexed)(implicit hexed2Bytes: Hexed2Bytes) = new PrivateKey(key)
  }

//  object SecurityKeyExtensions {
//    class RichKey(key: SecurityKey, implicit val bytes2Hexed: Bytes2Hexed) {
//      def toHexed = bytes2Hexed(key.bytes)
//      //override def toString = context.bytes2Hexed(key.bytes)
//    }
//    implicit def toRichKey(key: SecurityKey, bytes2Hexed: Bytes2Hexed) = new RichKey(key)
//  }

  type SigningPublicKey = PublicKey
  type EncryptionPublicKey = PublicKey
  type SigningPrivateKey = PrivateKey
  type EncryptionPrivateKey = PrivateKey

  final case class Signature(publicKey: SigningPublicKey, tick: ClockTick, signature: SignatureBytes) // TODO: Remove and live with just Proofs?

  final case class Hash(hash: HashBytes)
  object Hash {
    val Zero = Hash(ByteString.empty)
  }

  final case class Proof(hash: Hash, signature: Signature)
  sealed trait Proofed {
    def value: ValueBytes
  }
  object Proofed {
    final case class Signed(proof: Proof, value: ValueBytes) extends Proofed
    final case class MultiSigned(proofs: Set[Proof], value: ValueBytes) extends Proofed
  }

  trait Hashed {
    val hash: Hash
    val value: ValueBytes
  }

  final case class HashedValue(hash: Hash, value: ValueBytes) extends Hashed

//  final case class ChainRefData(id: Id, name: String, ver: Version)
//  type ChainRef = HashedValue[ChainRefData]
//
//  //case class ChainRef (override val hash: Hash, override val bytes: Serialized, override val value: ChainRefData)
//  //  extends HashedValue[ChainRefData](hash, bytes, value)
//  //object ChainRef {
//  //  def UID = this.hash.toString()
//  //}
//
//  final case class ChainDefData(ref: ChainRef, execGroups: Set[ExecGroup])
//  type ChainDef = HashedValue[ChainDefData]
//
//  //case class Data[T](value: HashedValue[T])
//
//  final case class MerkledRef(streamRefHash: Hash, pmHash: Hash, pos: Version, ownHash: Hash)
//
//  type HashedMR = HashedValue[MerkledRef]
//
//  trait StateFrame[T] {
//    val mref: HashedMR
//    val value: Option[HashedValue[T]]
//
//    def pos = mref.value.pos
//  }
//
//  object StateFrame {
//    case class InitialFrame[T](override val mref: HashedMR, override val value: Option[HashedValue[T]]) extends StateFrame[T]
//    case class Frame[T](override val mref: HashedMR, override val value: Option[HashedValue[T]]) // add proofs?
//      extends StateFrame[T]
//  }

  sealed trait Verified {
    //val value: ValueBytes
  }
  object Verified {
    final case class Passed(value: ValueBytes) extends Verified
    final case class HashCheckFailed(actual: Hash, expected: Hash) extends Verified
    final case class PublicKeyNotValid(key: SigningPublicKey, tick: ClockTick) extends Verified
    object ProofCheckFailed extends Verified
  }

  type Signer = ByteWord => Signed // TODO: Replace with Proofed in order to enable Multisig
  type Verifier = (Proof, ValueBytes) => Verified
  type KeysGenerator = () => (SigningPrivateKey, SigningPublicKey)

//  type ChainRefProvider = () => ChainRef

  trait CryptoContextSettings {
    implicit def hasher: Hasher
    //def serializer[T]: Serializer[T]
    //def deserializer[T]: Deserializer[T]
//    implicit def string2Bytes: string2Bytes
//    implicit def bytes2String: bytes2String
    implicit def bytes2Hexed: Bytes2Hexed
    implicit def hexed2Bytes: Hexed2Bytes
  }

  trait CryptoContext {
    def vectorClock: VectorClock
    def signingPublicKey: SigningPublicKey
    def signer: Signer
    def verifier: Verifier
  }

  trait PublicKeyRing {
    def checkKey(key: SigningPublicKey, tick: ClockTick): Boolean
  }

  case class NodeInfo(val signingPublicKey: SigningPublicKey, val encryptionPublicKey: EncryptionPublicKey)

//  object FrameBuilder {
//    def buildNestedRef(node: CryptoContext, cr: ChainRef, nestedName: String): ChainRef = {
//      val data = cr.value
//      val newData = data.copy(id = UUID.randomUUID(), name = data.name + "/" + nestedName, ver = data.ver)
//      node.hasher(newData)
//    }
//
//    def buildInitialFrame[T](node: CryptoContext, cr: ChainRef, initial: Option[T]): StateFrame[T] = {
//      val hashed = initial.map(node.hasher)
//      val mr = MerkledRef(cr.hash, Hash.Zero, 0, hashed.map(_.hash).getOrElse(Hash.Zero))
//      StateFrame.InitialFrame[T](node.hasher(mr), hashed).asInstanceOf[StateFrame[T]]
//    }
//
//    def buildFrame[T](node: CryptoContext, cr: ChainRef, state: StateFrame[T], data: T): StateFrame[T] = {
//      val hashedData = node.hasher(data)
//      val mr = MerkledRef(cr.hash, state.mref.hash, state.pos + 1, hashedData.hash)
//      StateFrame.Frame[T](node.hasher(mr), Some(hashedData))
//    }
//  }
//
//  object ChainRefFactory {
//    def chainRef(hasher: Hasher[ChainRefData], chainRefData: ChainRefData) = hasher(chainRefData)
//
//    def nestedRef(hasher: Hasher[ChainRefData], chainRef: ChainRef, name: String, chainVersion: Version) = {
//      hasher(ChainRefData(UUID.randomUUID(), s"${chainRef.value.name}#${chainRef.value.ver}\\$name", chainVersion))
//    }
//  }

  type HashedRegistry[T] = Hash => T
  type ClockTick = BigInt // TODO: Rename to VectorClock?
  type VectorClock = () => ClockTick

  trait AcCommand
  // def tick: ClockTick
  case class AcEvent[T <: AcCommand](command: T, tick: ClockTick)

  trait AcRegistryCommand extends AcCommand

  // TODO: Rethink all this
  type SignedCommand[T <: AcCommand] = (Proofed, T)
  type SignedEvent[T <: AcCommand] = (Proofed, SignedCommand[T])

}
