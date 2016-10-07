package com.avalanchain.core.builders

import java.nio.charset.StandardCharsets

import com.avalanchain.core.domain.ChainStream.{SigningPublicKey => _, _}
import com.avalanchain.core.domain.Verified.{HashCheckFailed, Passed, ProofCheckFailed}
import com.avalanchain.core.domain._
import scorex.crypto.encode.{Base16, Base58, Base64}
import scorex.crypto.hash.{CryptographicHash, Sha256, Sha512}
import scorex.crypto.signatures.Curve25519
import scorex.crypto.signatures.SigningFunctions.{PublicKey => _, _}

import scala.pickling._
import scala.pickling.json._
import scala.pickling.static._  // Avoid runtime pickler

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
      def bytes2Hexed = (bytes: BytesSerialized) => Base58.encode(bytes)
      def hexed2Bytes = (hexed: Hexed) => Base58.decode(hexed)
    }
    object Base64Hexing {
      def bytes2Hexed = (bytes: BytesSerialized) => Base64.encode(bytes)
      def hexed2Bytes = (hexed: Hexed) => Base64.decode(hexed)
    }
    object Base16Hexing {
      def bytes2Hexed = (bytes: BytesSerialized) => Base16.encode(bytes)
      def hexed2Bytes = (hexed: Hexed) => Base16.decode(hexed)
    }
  }

  def scorexHasher[T](hasher: CryptographicHash, serializer: Serializer[T], bytes2Hexed: Bytes2Hexed): Hasher[T] = (value: T) => {
    val serialized = serializer(value)
    val hash = hasher(serialized._2)
    val b2h = bytes2Hexed(hash)
    HashedValue(Hash(b2h), serialized, value)
  }

  trait Signing[T] {
    def signer: Signer[T]
    def verifier: Verifier[T]
  }
  object Signing {
    private val curve = new Curve25519
    private val keyPair: (PrivateKey, PublicKey) = curve.createKeyPair

    def signingPublicKey = keyPair._2

    class ECC25519[T](serializer: Serializer[T], hasher: Hasher[T]) extends Signing[T] {
      override def signer: Signer[T] = (value: T) => {
        val signature = curve.sign(keyPair._1, serializer(value)._2)
        val hashedValue = hasher(value)
        val proof = Proof((signingPublicKey, signature), hashedValue.hash)
        Signed(proof, value)
      }
      override def verifier: Verifier[T] = (proof: Proof, value: T) => {
        val serialized = serializer(value)
        val expectedHash = hasher(value).hash
        if (expectedHash != proof.hash) HashCheckFailed(value, proof.hash, expectedHash)
        else if (curve.verify(proof.signature._2, serialized._2, proof.signature._1)) Passed(value)
        else ProofCheckFailed(value)
      }
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

  def apply(hash: CryptographicHash = Sha512): CryptoContext = {
    new CryptoContext {
      override def hasher[T]: Hasher[T] = hasher

      override def hexed2Bytes: Hexed2Bytes = Hexing.Base58Hexing.hexed2Bytes
      override def bytes2Hexed: Bytes2Hexed = Hexing.Base58Hexing.bytes2Hexed

      override def serializer[T]: Serializer[T] = serializer
      override def deserializer[T]: ((TextSerialized) => T, (BytesSerialized) => T) = deserializer

      override def signingPublicKey: SigningPublicKey = Signing.signingPublicKey
      override def signer[T]: Signer[T] = (new Signing.ECC25519[T](serializer, hasher)).signer
      override def verifier[T]: Verifier[T] = (new Signing.ECC25519[T](serializer, hasher)).verifier
    }
  }
}
