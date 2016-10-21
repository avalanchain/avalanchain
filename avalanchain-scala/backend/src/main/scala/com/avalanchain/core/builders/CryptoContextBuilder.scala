package com.avalanchain.core.builders

import java.nio.charset.StandardCharsets
import java.util.concurrent.atomic.AtomicInteger

import com.avalanchain.core.domain.Proofed.Signed
import com.avalanchain.core.domain._
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

import spray.json._
import fommil.sjs.FamilyFormats._

/**
  * Created by Yuriy Habarov on 10/05/2016.
  */
object CryptoContextBuilder {
  // sealed trait Serializer
//  object PicklingSerializer {
//    def serializer[T: FastTypeTag : Pickler]: Serializer[T] = (value: T) => {
//      val pickled = value.pickle
//      val text = pickled.toString
//      (text, text.getBytes(StandardCharsets.UTF_8))
//    }
//
//    def deserializer[T: FastTypeTag : Unpickler]: Deserializer[T] = {
//      def textDeserializer = (text: TextSerialized) => {
//        text.unpickle[T]
//      }
//      def bytesDeserializer = (bytes: BytesSerialized) => {
//        val text = new String(bytes.map(_.toChar))
//        textDeserializer(text)
//      }
//      (textDeserializer, bytesDeserializer)
//    }
//  }

//  object SprayJsonSerializer {
//    def serializer[T]: Serializer[T] = (value: T) => {
//      val pickled = value.toJson
//      val text = pickled.toString
//      (text, text.getBytes(StandardCharsets.UTF_8))
//    }
//
//    def deserializer[T]: Deserializer[T] = {
//      def textDeserializer = (text: TextSerialized) => {
//        text.parseJson.convertTo[T]
//      }
//      def bytesDeserializer = (bytes: BytesSerialized) => {
//        val text = new String(bytes.map(_.toChar))
//        textDeserializer(text)
//      }
//      (textDeserializer, bytesDeserializer)
//    }
//  }

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

  def scorexHasher(hasher: CryptographicHash): Hasher = (value: BytesSerialized) => {
    val hash = hasher(value)
    HashedValue(Hash(hash), value)
  }

  private class SigningECC25519(keyPairOpt: Option[(SigningPrivateKey, SigningPublicKey)] = None) {
    private val curve = new Curve25519
    private val keyPair: (SigningPrivateKey, SigningPublicKey) = keyPairOpt match {
      case Some(kp) => kp
      case None =>
        val pair: (scorex.crypto.signatures.SigningFunctions.PrivateKey, scorex.crypto.signatures.SigningFunctions.PublicKey) = curve.createKeyPair
        (PrivateKey(pair._1), PublicKey(pair._2))
    }

    def signingPrivateKey = keyPair._1
    def signingPublicKey = keyPair._2

    def signer[T](hasher: Hasher, vectorClock: VectorClock): Signer = (value: BytesSerialized) => {
      val signature = curve.sign(keyPair._1.bytes, value)
      val hashedValue = hasher(value)
      val proof = Proof((signingPublicKey, vectorClock(), signature), hashedValue.hash)
      Signed(proof, value)
    }
    def verifier[T](hasher: Hasher, vectorClock: VectorClock): Verifier = (proof: Proof, value: BytesSerialized) => {
      val expectedHash = hasher(value).hash
      if (!(expectedHash.hash sameElements proof.hash.hash)) HashCheckFailed(value, proof.hash, expectedHash)
      else if (curve.verify(proof.signature._3, value, proof.signature._1.bytes)) Passed(value)
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

  def apply(keyPairOpt: Option[(SigningPrivateKey, SigningPublicKey)] = None, hash: CryptographicHash = Sha512): (CryptoContext, SigningPrivateKey) = {
    val signing = new SigningECC25519(keyPairOpt)

    (new CryptoContext {
      private val ai = new AtomicInteger(0)

      def vectorClock: VectorClock = () => ai.getAndAdd(1)
      def hasher: Hasher = scorexHasher(hash)

      def text2Bytes: Text2Bytes = _.getBytes(StandardCharsets.UTF_8)
      def bytes2Text: Bytes2Text = bytes => new String(bytes.map(_.toChar))

      def hexed2Bytes: Hexed2Bytes = Hexing.Base58Hexing.hexed2Bytes
      def bytes2Hexed: Bytes2Hexed = Hexing.Base58Hexing.bytes2Hexed

//      override def serializer[T]: Serializer[T] = ??? //PicklingSerializer.serializer[T] // SprayJsonSerializer.serializer[T]
//      override def deserializer[T]: ((TextSerialized) => T, (BytesSerialized) => T) = ??? //PicklingSerializer.deserializer[T] // SprayJsonSerializer.deserializer[T]

      def signingPublicKey: SigningPublicKey = signing.signingPublicKey
      def signer: Signer = signing.signer(hasher, vectorClock)
      def verifier: Verifier = signing.verifier(hasher, vectorClock)
    }, signing.signingPrivateKey)
  }
}
