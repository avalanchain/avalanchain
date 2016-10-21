package com.avalanchain.core.builders

import java.nio.charset.StandardCharsets
import java.util.concurrent.atomic.AtomicInteger

import com.avalanchain.core.domain.Proofed.Signed
import com.avalanchain.core.domain.{SigningPublicKey, _}
import com.avalanchain.core.domain.Verified.{HashCheckFailed, Passed, ProofCheckFailed}
import scorex.crypto.encode.{Base16, Base58, Base64}
import scorex.crypto.hash.{CryptographicHash, Sha256, Sha512}
import scorex.crypto.signatures.Curve25519
import scorex.crypto.signatures.SigningFunctions.{PublicKey => _, _}

import scala.pickling._
import scala.pickling.json._
import scala.pickling.static._
import scala.util.Success  // Avoid runtime pickler

// Import pickle ops
import scala.pickling.Defaults.{ pickleOps, unpickleOps }

// Import picklers for specific types
import scala.pickling.Defaults.{ stringPickler, intPickler, refUnpickler, nullPickler }
import scala.pickling.{Pickler, Unpickler}

/**
  * Created by Yuriy Habarov on 10/05/2016.
  */
object CryptoContextBuilder {
  // sealed trait Serializer
  object PicklingSerializer {
    def serializer[T: FastTypeTag : Pickler]: Serializer[T] = (value: T) => {
      val pickled = value.pickle
      val text = pickled.toString
      (text, text.getBytes(StandardCharsets.UTF_8))
    }

    def deserializer[T: FastTypeTag : Unpickler]: Deserializer[T] = {
      def textDeserializer = (text: TextSerialized) => {
        text.unpickle[T]
      }
      def bytesDeserializer = (bytes: BytesSerialized) => {
        val text = new String(bytes.map(_.toChar))
        textDeserializer(text)
      }
      (textDeserializer, bytesDeserializer)
    }
  }

  object Hexing {
    object Base58Hexing {
      def bytes2Hexed: Bytes2Hexed = Base58.encode(_)
      def hexed2Bytes: Hexed2Bytes = Base58.decode(_)
    }
    object Base64Hexing {
      def bytes2Hexed: Bytes2Hexed = Base64.encode(_)
      def hexed2Bytes: Hexed2Bytes = s => Success(Base64.decode(s))
    }
    object Base16Hexing {
      def bytes2Hexed: Bytes2Hexed = Base16.encode(_)
      def hexed2Bytes: Hexed2Bytes = s => Success(Base16.decode(s))
    }
  }

  def scorexHasher[T](hasher: CryptographicHash, serializer: Serializer[T], bytes2Hexed: Bytes2Hexed): Hasher[T] = (value: T) => {
    val serialized = serializer(value)
    val hash = hasher(serialized._2)
    val b2h = bytes2Hexed(hash)
    HashedValue(Hash(b2h), serialized, value)
  }

  private class SigningECC25519(bytes2Hexed: Bytes2Hexed, keyPairOpt: Option[(SigningPrivateKey, SigningPublicKey)] = None) {
    private val curve = new Curve25519
    private val keyPair: (SigningPrivateKey, SigningPublicKey) = keyPairOpt match {
      case Some(kp) => kp
      case None =>
        val pair: (scorex.crypto.signatures.SigningFunctions.PrivateKey, scorex.crypto.signatures.SigningFunctions.PublicKey) = curve.createKeyPair
        (PrivateKey(pair._1, bytes2Hexed), PublicKey(pair._2, bytes2Hexed))
    }

    def signingPrivateKey = keyPair._1
    def signingPublicKey = keyPair._2

    def signer[T](serializer: Serializer[T], hasher: Hasher[T], vectorClock: VectorClock): Signer[T] = (value: T) => {
      val signature = curve.sign(keyPair._1.bytes, serializer(value)._2)
      val hashedValue = hasher(value)
      val proof = Proof((signingPublicKey, vectorClock(), signature), hashedValue.hash)
      Signed(proof, value)
    }
    def verifier[T](serializer: Serializer[T], hasher: Hasher[T], vectorClock: VectorClock): Verifier[T] = (proof: Proof, value: T) => {
      val serialized = serializer(value)
      val expectedHash = hasher(value).hash
      if (expectedHash != proof.hash) HashCheckFailed(value, proof.hash, expectedHash)
      else if (curve.verify(proof.signature._3, serialized._2, proof.signature._1.bytes)) Passed(value)
      else ProofCheckFailed(value)
    }
  }

//  Supported hash algorithms are:
//
//  Blake
//  Blake2b
//  BMW
//  CubeHash
//  Echo
//  Fugue
//  Groestl
//  Hamsi
//  JH
//  Keccak
//  Luffa
//  Sha
//  SHAvite
//  SIMD
//  Skein
//  Whirlpool

  def apply(hash: CryptographicHash = Sha512): (CryptoContext, SigningPrivateKey) = {
    val b2h = Hexing.Base58Hexing.bytes2Hexed
    val signing = new SigningECC25519(b2h)

    (new CryptoContext {
      private val ai = new AtomicInteger(0)

      override def vectorClock: VectorClock = () => ai.getAndAdd(1)
      override def hasher[T]: Hasher[T] = hasher

      override def hexed2Bytes: Hexed2Bytes = Hexing.Base58Hexing.hexed2Bytes
      override def bytes2Hexed: Bytes2Hexed = b2h

      override def serializer[T]: Serializer[T] = serializer
      override def deserializer[T]: ((TextSerialized) => T, (BytesSerialized) => T) = deserializer

      override def signingPublicKey: SigningPublicKey = signing.signingPublicKey
      override def signer[T]: Signer[T] = signing.signer(serializer, hasher, vectorClock)
      override def verifier[T]: Verifier[T] = signing.verifier(serializer, hasher, vectorClock)
    }, signing.signingPrivateKey)
  }
}
