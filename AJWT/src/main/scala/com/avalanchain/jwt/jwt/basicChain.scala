package com.avalanchain.jwt

import java.security.{KeyPair, PrivateKey, PublicKey}
import java.util.UUID

import akka.NotUsed
import akka.actor.ActorRefFactory
import akka.stream.scaladsl.Source
import io.circe.{Decoder, Encoder, Json}
import io.circe.syntax._
import io.circe.parser._
import io.circe.generic.JsonCodec
import io.circe.generic.auto._
import pdi.jwt.{Jwt, JwtAlgorithm, JwtBase64}
import pdi.jwt.exceptions.JwtLengthException
import com.avalanchain.jwt.KeysDto.PubKey
import com.avalanchain.jwt.actors._
import com.avalanchain.jwt.basicChain.JwtAlgo.{ES512, HS512}

import scala.collection.immutable.Map
import scala.util.{Success, Try}
import scala.concurrent.duration._

/**
  * Created by Yuriy Habarov on 08/11/2016.
  */
package object basicChain {
  type Id = UUID
  type Position = Long
  type JsonStr = String

  sealed trait JwtPayload
  object JwtPayload {
    trait Sym extends JwtPayload
    trait Asym extends JwtPayload { val pub: PubKey }
  }

  case class ResourceGroup(name: String)

  type Func1 = String
  type Func2 = String
  sealed trait ChainDerivationFunction
  object ChainDerivationFunction {
//    case class Fork(pos: Position) extends ChainDerivationFunction
    final case class Map(f: Func1) extends ChainDerivationFunction
    final case class Filter(f: Func1) extends ChainDerivationFunction
    final case class Fold(f: Func2, init: JsonStr = "{}") extends ChainDerivationFunction
    final case class GroupBy(f: Func1, max: Int) extends ChainDerivationFunction
    //case class Reduce(f: Func2) extends ChainDerivationFunction
  }

  sealed trait JwtAlgo
  object JwtAlgo {
    final case object HS512 extends JwtAlgo { override def toString = "HS512" }
    final case object ES512 extends JwtAlgo { override def toString = "ES512" }
  }

  sealed trait ChainDef extends JwtPayload.Asym { val algo: JwtAlgo; val id: Id; val pub: PubKey }
  object ChainDef {
    final case class New(algo: JwtAlgo, id: Id, pub: PubKey, init: Option[JsonStr]) extends ChainDef
    final case class Fork(algo: JwtAlgo, id: Id, pub: PubKey, parent: ChainRef, pos: Position) extends ChainDef
    final case class Derived(algo: JwtAlgo, id: Id, pub: PubKey, parent: ChainRef, cdf: ChainDerivationFunction) extends ChainDef
  }

  sealed trait JwtToken {
    val token: String
    private val chunks = splitToken(token)
    val header64 = chunks._1
    val header = chunks._2
    val payload64 = chunks._3
    val payloadJson = chunks._4
    val sig = chunks._5
    override def toString() = token

    /**
      * @return a tuple of (header64, header, claim64, claim, Option(signature as bytes))
      * @throws JwtLengthException if there is not 2 or 3 parts in the token
      */
    private def splitToken(token: String): (String, String, String, String, String) = {
      val parts = token.split("\\.")

      val signature = parts.length match {
        case 2 => ""
        case 3 => parts(2)
        case _ => throw new JwtLengthException(s"Expected token [$token] to be composed of 2 or 3 parts separated by dots.")
      }

      (parts(0), JwtBase64.decodeString(parts(0)), parts(1), JwtBase64.decodeString(parts(1)), signature)
    }
  }

  case class TypedJwtToken[T <: JwtPayload](token: String)(implicit decoder: Decoder[T]) extends JwtToken {
    val payload = decode[T](payloadJson).toOption
  }
  object TypedJwtToken {
    def apply[T <: JwtPayload.Sym](payload: T, secret: String)(implicit encoder: Encoder[T], decoder: Decoder[T]): TypedJwtToken[T] =
      new TypedJwtToken[T](Jwt.encode(payload.asJson.noSpaces, secret, JwtAlgorithm.HS512))
    def apply[T <: JwtPayload.Asym](payload: T, privateKey: PrivateKey)(implicit encoder: Encoder[T], decoder: Decoder[T]): TypedJwtToken[T] =
      new TypedJwtToken[T](Jwt.encode(payload.asJson.noSpaces, privateKey, JwtAlgorithm.ES512))
  }

  type ChainDefToken = TypedJwtToken[ChainDef]
  case class ChainRef(sig: String)
  object ChainRef {
    def apply[T <: ChainDef](chainDef: TypedJwtToken[T]): ChainRef = new ChainRef(chainDef.sig)
  }

  //sealed trait Shackle extends JwtPayload.Sym { val cr: ChainRef; val pos: Long }
  //object Shackle {
  //  case class ShackleRef(sig: String)
  //
  //  case class Seed(cr: ChainRef, puk: PubKey) extends Shackle { override val pos: Position = -1 }
  //  case class Frame(cr: ChainRef, pos: Position, pref: ShackleRef, v: Json) extends Shackle {
  //    if (pos < 0) throw new RuntimeException(s"Frame pos cannot be negative, but found $pos")
  //  }
  //}

  case class FrameRef(sig: String) // ChainRef for first frame and previous Frame signatures for others
  object FrameRef {
    def apply(frameRef: FrameToken): FrameRef = new FrameRef(frameRef.sig)
  }
  sealed trait Frame extends JwtPayload {
    val cr: ChainRef
    val pos: Position
    val prev: FrameRef
    val v: Json
  }
  final case class FSym(cr: ChainRef, pos: Position, prev: FrameRef, v: Json) extends JwtPayload.Sym with Frame {
    if (pos < 0) throw new RuntimeException(s"Frame pos cannot be negative, but found $pos")
  }
  final case class FAsym(cr: ChainRef, pos: Position, prev: FrameRef, v: Json, pub: PubKey) extends JwtPayload.Asym with Frame {
    if (pos < 0) throw new RuntimeException(s"Frame pos cannot be negative, but found $pos")
  }
  type FrameToken = TypedJwtToken[Frame]

  sealed trait ChainStatus
  object ChainStatus {
    case object Created extends ChainStatus
    case object Active extends ChainStatus
    //object Passive extends ChainStatus
    case class Failed(reason: String) extends ChainStatus
  }

  trait FrameTokenStorage {
    def add(frameToken: FrameToken): Try[Unit]
    def get(frameRef: FrameRef): Option[FrameToken]
    //def get(chainRef: ChainRef, pos: Position): Option[FrameToken]
    def getFromSnapshot(fromPosition: Position, toPosition: Position)(implicit decoder: Decoder[Frame]): Source[FrameToken, NotUsed]
    def getFrom(fromPosition: Position, toPosition: Position)(implicit decoder: Decoder[Frame]): Source[FrameToken, NotUsed]
  }
  type FrameSigner = String => Frame => FrameToken
  case class ChainState(frame: Option[FrameToken], lastRef: FrameRef, pos: Position)

  class Chain(val chainDefToken: ChainDefToken, val keyPair: KeyPair,
              tokenStorage: FrameTokenStorage, currentState: Option[ChainState] = None)(implicit actorRefFactory: ActorRefFactory) {
    if (chainDefToken.payload.isEmpty) throw new RuntimeException(s"Inconsistent ChainDefToken: '$chainDefToken'")
    val chainRef = ChainRef(chainDefToken)
    def status = ChainStatus.Created

//    def pos: Position = state.pos
//    def current: Option[FrameToken] = state.frame

    private var state = currentState.getOrElse(ChainState(None, FrameRef(chainRef.sig), -1))

    def add(v: Json): Try[Unit] = {
      val newPos = state.pos + 1
      val frameToken: TypedJwtToken[Frame] = chainDefToken.payload.get.algo match {
        case HS512 => TypedJwtToken[FSym](FSym(chainRef, newPos, state.lastRef, v), state.lastRef.sig).asInstanceOf[TypedJwtToken[Frame]]
        case ES512 => TypedJwtToken[FAsym](FAsym(chainRef, newPos, state.lastRef, v, keyPair.getPublic), keyPair.getPrivate).asInstanceOf[TypedJwtToken[Frame]]
      }
      tokenStorage.add(frameToken).map(_ => {
        state = ChainState(Some(frameToken), FrameRef(frameToken), newPos)
      })
    }

    def sink() =
      PersistentSink(chainRef)(actorRefFactory, 5 seconds)
  }

  import scala.collection._
  import scala.collection.convert.decorateAsScala._
  import java.util.concurrent.ConcurrentHashMap

  class MapFrameTokenStorage extends FrameTokenStorage {
    private var tokens = new ConcurrentHashMap[FrameRef, FrameToken].asScala
    private val buffer = mutable.ArrayBuffer.empty[FrameToken]
    def frameTokens = tokens

    override def add(frameToken: FrameToken): Try[Unit] = {
      tokens += (FrameRef(frameToken) -> frameToken)
      buffer += frameToken
      Success(())
    }

    // Do not use. Don't work properly.
    override def get(frameRef: FrameRef): Option[FrameToken] = tokens.get(frameRef)
    def getFromSnapshot(fromPosition: Position, toPosition: Position)(implicit decoder: Decoder[Frame]): Source[FrameToken, NotUsed] = {
      Source.fromIterator(() => buffer.toIterator)
        .filter(_.payload.get.pos >= fromPosition)
        .takeWhile(_.payload.get.pos <= toPosition)
    }

    def getFrom(fromPosition: Position, toPosition: Position)(implicit decoder: Decoder[Frame]): Source[FrameToken, NotUsed] = {
      getFromSnapshot(fromPosition, toPosition)//.concat(broadcastQueue).takeWhile(_.payload.get.pos <= toPosition)
    }

  }

  class ChainRegistry(keyPair: KeyPair)
                     (implicit actorRefFactory: ActorRefFactory, encoder: Encoder[ChainDef], decoder: Decoder[ChainDef]) {
    private val privateKey = keyPair.getPrivate
    val publicKey = keyPair.getPublic

    private var chains = new ConcurrentHashMap[ChainRef, Chain].asScala
    def Chains() = chains

    //TODO: nestedChain, addFrame

    private def addChainDef(chainDef: ChainDef) = {
      val chainDefToken = TypedJwtToken[ChainDef](chainDef, privateKey)
      val chainRef = ChainRef(chainDefToken)
      val newChain = new Chain(chainDefToken, keyPair, new MapFrameTokenStorage())
      chains += (chainRef -> newChain)
      newChain
    }

    def newChain(jwtAlgo: JwtAlgo, initValue: Option[JsonStr] = Some("{}")): Chain =
      addChainDef(ChainDef.New(jwtAlgo, UUID.randomUUID(), publicKey, initValue))

    def nestedChain(jwtAlgo: JwtAlgo, parentChainRef: ChainRef, pos: Position): Chain =
      addChainDef(ChainDef.Fork(jwtAlgo, UUID.randomUUID(), publicKey, parentChainRef, pos))

    def derivedChain(jwtAlgo: JwtAlgo, parentChainRef: ChainRef, pos: Position): Chain =
      addChainDef(ChainDef.Fork(jwtAlgo, UUID.randomUUID(), publicKey, parentChainRef, pos))

  }

}
