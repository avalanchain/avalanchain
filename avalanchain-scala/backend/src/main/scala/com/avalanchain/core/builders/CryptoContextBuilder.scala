package com.avalanchain.core.builders

import java.nio.charset.StandardCharsets
import java.util.concurrent.atomic.AtomicInteger

import akka.util.ByteString
import com.avalanchain.core.domain.Proofed.Signed
import com.avalanchain.core.domain.{CryptoContextSettings, PrivateKey, _}
import com.avalanchain.core.domain.Verified.{HashCheckFailed, Passed, ProofCheckFailed, PublicKeyNotValid}
import com.avalanchain.core.toolbox.Pipe
import Pipe._
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

  private final case class ECC25519KeysGenerator() {
    private val curve = new Curve25519
    def generate(): (SigningPrivateKey, SigningPublicKey) = {
      val pair: (scorex.crypto.signatures.SigningFunctions.PrivateKey, scorex.crypto.signatures.SigningFunctions.PublicKey) = curve.createKeyPair
      val priv = pair._1 |> (ByteWord(_)) |> (new PrivateKey(_))
      val pub = pair._2 |> (ByteWord(_)) |> (new PublicKey(_))
      (priv, pub)
    }
  }

  def keysGenerator(): KeysGenerator = new ECC25519KeysGenerator().generate

  private final case class ECC25519Signer(signingPrivateKey: SigningPrivateKey, signingPublicKey: SigningPublicKey) {
    private val curve = new Curve25519

    def signer(hasher: BytesHasher, vectorClock: VectorClock): Signer = (value: ByteWord) => {
      val time = vectorClock()
      val signature = curve.sign(signingPrivateKey.key.toArray, (ByteWord(time.toByteArray) concat value).toArray) |> (ByteWord(_))
      val hashedValue = hasher(value)
      val proof = Proof(hashedValue.hash, Signature(signingPublicKey, time, signature))
      Signed(proof, value)
    }
  }

  private final case class ECC25519Verifier(keyRing: PublicKeyRing) {
    private val curve = new Curve25519

    def verifier(hasher: BytesHasher): Verifier = (proof: Proof, value: ValueBytes) => {
      if (!keyRing.checkKey(proof.signature.publicKey, proof.signature.tick)) PublicKeyNotValid(proof.signature.publicKey, proof.signature.tick)
      else {
        val expectedHash = hasher(value).hash
        if (!(expectedHash.hash sameElements proof.hash.hash)) HashCheckFailed(proof.hash, expectedHash)
        else if (curve.verify(
          proof.signature.signature.toArray,
          (ByteWord(proof.signature.tick.toByteArray) concat value).toArray,
          proof.signature.publicKey.key.toArray))
            Passed(value)
        else ProofCheckFailed
      }
    }
  }

  final class PublicKeyRingSet(keys: Set[SigningPublicKey], from: ClockTick, to: ClockTick, implicit val hexed2Bytes: Hexed2Bytes) extends PublicKeyRing {
    def checkKey(key: SigningPublicKey, tick: ClockTick): Boolean = {
      tick >= from &&
        tick <= to &&
        keys.contains(key)
    }
  }

  def createCryptoContext(signingPrivateKey: SigningPrivateKey, signingPublicKey: SigningPublicKey, knownPublicKeys: Set[SigningPublicKey] = Set.empty)
                         (implicit ccs: CryptoContextSettings): CryptoContext = {
    val signerObject = new ECC25519Signer(signingPrivateKey, signingPublicKey)

    new CryptoContext {
      private val ai = new AtomicInteger(0)

      def vectorClock: VectorClock = () => ai.getAndAdd(1)

      def signingPublicKey: SigningPublicKey = signerObject.signingPublicKey
      def signer: Signer = signerObject.signer(ccs.bytesHasher, vectorClock)
      def verifier: Verifier = new ECC25519Verifier(new PublicKeyRingSet(knownPublicKeys, 0, 100000, ccs.hexed2Bytes)).verifier(ccs.bytesHasher) // TODO: Add self public Key?
    }
  }
}
