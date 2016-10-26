package com.avalanchain.core
/**
  * Created by Yuriy Habarov on 19/04/2016.
  */

import java.util.UUID

import akka.util.ByteString
import com.avalanchain.core.domain.Proofed.Signed
import com.avalanchain.core.toolbox.Pipe._
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

  type TextDeserializer[T] = TextSerialized => Option[T]
  type BytesDeserializer[T] = BytesSerialized => Option[T]
  type Bytes2String = BytesSerialized => Hexed

  type BytesHasher = ByteWord => Hashed
  type Hasher[T] = T => HashedValue[T]
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
    val valueBytes: ValueBytes
  }

  final case class HashedValue[T](hash: Hash, value: T)(implicit serializer: BytesSerializer[T]) extends Hashed {
    val valueBytes: ValueBytes = serializer(value)
  }


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
    implicit def bytesHasher: BytesHasher
    implicit def hasher[T](implicit serializer: BytesSerializer[T]): Hasher[T] = v => v |> (serializer(_)) |> bytesHasher |> (h => HashedValue[T](h.hash, v))
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
