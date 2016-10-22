package com.avalanchain.core.builders

import java.nio.charset.StandardCharsets
import java.util.concurrent.atomic.AtomicInteger

import akka.util.ByteString
import com.avalanchain.core.domain.Proofed.Signed
import com.avalanchain.core.domain.{PrivateKey, _}
import com.avalanchain.core.domain.Verified.{HashCheckFailed, Passed, ProofCheckFailed}
import com.avalanchain.toolbox.Pipe._
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
      def bytes2Hexed: Bytes2Hexed = _ |> (_.toArray) |> Base58.encode
      def hexed2Bytes: Hexed2Bytes = _ |> (Base58.decode(_).getOrElse(Array[Byte]())) |> (ByteWord(_))
    }
    object Base64Hexing {
      def bytes2Hexed: Bytes2Hexed = _ |> (_.toArray) |> Base64.encode
      def hexed2Bytes: Hexed2Bytes = _ |> (Base64.decode(_)) |> (ByteWord(_))
    }
    object Base16Hexing {
      def bytes2Hexed: Bytes2Hexed = _ |> (_.toArray) |> Base16.encode
      def hexed2Bytes: Hexed2Bytes = _ |> (Base16.decode(_)) |> (ByteWord(_))
    }
  }

  def scorexHasher(hasher: CryptographicHash): Hasher = (value: ByteWord) => {
    val hash = hasher(value.toArray) |> (ByteWord(_)) |> (Hash(_))
    HashedValue(hash, value)
  }

  private class SigningECC25519(keyPairOpt: Option[(SigningPrivateKey, SigningPublicKey)] = None) {
    private val curve = new Curve25519
    private val keyPair: (SigningPrivateKey, SigningPublicKey) = keyPairOpt match {
      case Some(kp) => kp
      case None => {
        val pair: (scorex.crypto.signatures.SigningFunctions.PrivateKey, scorex.crypto.signatures.SigningFunctions.PublicKey) = curve.createKeyPair
        val priv = pair |> (_._1) |> (ByteWord(_)) |> PrivateKey
        val pub = pair |> (_._2) |> (ByteWord(_)) |> PublicKey
        (priv, pub)
      }
    }

    def signingPrivateKey = keyPair._1
    def signingPublicKey = keyPair._2

    def signer(hasher: Hasher, vectorClock: VectorClock): Signer = (value: ByteWord) => {
      val time = vectorClock()
      val signature = curve.sign(keyPair._1.key.toArray, (ByteWord(time.toByteArray) concat value).toArray) |> (ByteWord(_))
      val hashedValue = hasher(value)
      val proof = Proof((signingPublicKey, time, signature), hashedValue.hash)
      Signed(proof, value)
    }
    def verifier(hasher: Hasher): Verifier = (proof: Proof, value: ValueBytes) => {
      val expectedHash = hasher(value).hash
      if (!(expectedHash.hash sameElements proof.hash.hash)) HashCheckFailed(value, proof.hash, expectedHash)
      else if (curve.verify(proof.signature._3.toArray, value.toArray, proof.signature._1.key.toArray)) Passed(value)
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

      def text2Bytes: Text2Bytes = _.getBytes(StandardCharsets.UTF_8) |> (ByteWord(_))
      def bytes2Text: Bytes2Text = _.mkString

      def hexed2Bytes: Hexed2Bytes = Hexing.Base58Hexing.hexed2Bytes
      def bytes2Hexed: Bytes2Hexed = Hexing.Base58Hexing.bytes2Hexed

//      override def serializer[T]: Serializer[T] = ??? //PicklingSerializer.serializer[T] // SprayJsonSerializer.serializer[T]
//      override def deserializer[T]: ((TextSerialized) => T, (BytesSerialized) => T) = ??? //PicklingSerializer.deserializer[T] // SprayJsonSerializer.deserializer[T]

      def signingPublicKey: SigningPublicKey = signing.signingPublicKey
      def signer: Signer = signing.signer(hasher, vectorClock)
      def verifier: Verifier = signing.verifier(hasher)
    }, signing.signingPrivateKey)
  }
}
