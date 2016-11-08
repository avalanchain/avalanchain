package com.avalanchain.jwt

import java.security.{KeyPair, PrivateKey}
import java.util.UUID

import com.avalanchain.jwt.KeysDto.PubKey
import io.circe.{Decoder, Encoder, Json}
import io.circe.syntax._
import io.circe.parser._
import io.circe.generic.JsonCodec
import io.circe.generic.auto._
import pdi.jwt.{Jwt, JwtAlgorithm, JwtBase64}
import pdi.jwt.exceptions.JwtLengthException

import scala.collection.immutable.Map
import scala.util.{Success, Try}

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
    trait Asym extends JwtPayload { val puk: PubKey }
  }

  sealed trait ChainDef extends JwtPayload.Asym { val id: Id }
  object ChainDef {
    case class New(id: Id, puk: PubKey) extends ChainDef
    case class Nested(id: Id, puk: PubKey, parent: ChainRef, pos: Position) extends ChainDef
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

  sealed trait TypedJwtToken[T <: JwtPayload] extends JwtToken {
    val payload: Option[T]
  }

  case class JwtTokenSym[T <: JwtPayload.Sym](token: String)(implicit decoder: Decoder[T]) extends TypedJwtToken[T] {
    val payload = decode[T](payloadJson).toOption
  }
  object JwtTokenSym {
    def apply[T <: JwtPayload.Sym](payload: T, secret: String)(implicit encoder: Encoder[T], decoder: Decoder[T]): JwtTokenSym[T] =
      new JwtTokenSym[T](Jwt.encode(payload.asJson.noSpaces, secret, JwtAlgorithm.HS512))
  }
  case class JwtTokenAsym[T <: JwtPayload.Asym](token: String)(implicit decoder: Decoder[T]) extends TypedJwtToken[T] {
    val payload = decode[T](payloadJson).toOption
  }
  object JwtTokenAsym {
    def apply[T <: JwtPayload.Asym](payload: T, privateKey: PrivateKey)(implicit encoder: Encoder[T], decoder: Decoder[T]): JwtTokenAsym[T] =
      new JwtTokenAsym[T](Jwt.encode(payload.asJson.noSpaces, privateKey, JwtAlgorithm.ES512))
  }

  type ChainDefToken = JwtTokenAsym[ChainDef]
  case class ChainRef(sig: String)
  object ChainRef {
    def apply[T <: ChainDef](chainDef: JwtTokenAsym[T]): ChainRef = new ChainRef(chainDef.sig)
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
  case class Frame(cr: ChainRef, pos: Position, pref: FrameRef, v: Json) extends JwtPayload.Sym {
    if (pos < 0) throw new RuntimeException(s"Frame pos cannot be negative, but found $pos")
  }
  type FrameToken = JwtTokenSym[Frame]


  sealed trait ChainStatus
  object ChainStatus {
    object Created extends ChainStatus
    object Active extends ChainStatus
    //object Passive extends ChainStatus
    case class Failed(reason: String) extends ChainStatus
  }

  trait FrameTokenStorage {
    def add(frameToken: FrameToken): Try[Unit]
    def get(frameRef: FrameRef): Option[FrameToken]
    //def get(chainRef: ChainRef, pos: Position): Option[FrameToken]
  }
  type FrameSigner = String => Frame => FrameToken
  case class ChainState(frame: Option[FrameToken], lastRef: FrameRef, pos: Position)
  class Chain(val chainDefToken: ChainDefToken, //val publicKey: PublicKey,
              tokenStorage: FrameTokenStorage, currentState: Option[ChainState] = None) {
    val chainRef = ChainRef(chainDefToken)
    def status = ChainStatus.Created

    def pos: Position = state.pos
    def current: Option[FrameToken] = state.frame

    private var state = currentState.getOrElse(ChainState(None, FrameRef(chainRef.sig), -1))

    def add(v: Json): Try[Unit] = {
      val frame = Frame(chainRef, state.pos + 1, state.lastRef, v)
      val frameToken = JwtTokenSym[Frame](frame, state.lastRef.sig)
      tokenStorage.add(frameToken).map(_ => {
        state = ChainState(Some(frameToken), FrameRef(frameToken.sig), frame.pos)
      })
    }
  }

  import scala.collection._
  import scala.collection.convert.decorateAsScala._
  import java.util.concurrent.ConcurrentHashMap

  class MapFrameTokenStorage extends FrameTokenStorage {
    private var tokens = new ConcurrentHashMap[FrameRef, FrameToken].asScala
    def frameTokens = tokens

    override def add(frameToken: FrameToken): Try[Unit] = {
      tokens += (FrameRef(frameToken) -> frameToken)
      Success(())
    }

    override def get(frameRef: FrameRef): Option[FrameToken] = tokens.get(frameRef)
  }

  class ChainRegistry(keyPair: KeyPair, frameTokenStorage: FrameTokenStorage = new MapFrameTokenStorage()) {
    private val privateKey = keyPair.getPrivate
    val publicKey = keyPair.getPublic

    private var chains = new ConcurrentHashMap[ChainRef, Chain].asScala
    def Chains() = chains

    //TODO: nestedChain, addFrame

    def newChain(): Chain = {
      val chainDef = ChainDef.New(UUID.randomUUID(), publicKey)
      val chainDefToken = JwtTokenAsym[ChainDef](chainDef, privateKey)
      val chainRef = ChainRef(chainDefToken)
      val newChain = new Chain(chainDefToken, frameTokenStorage)
      chains += (chainRef -> newChain)
      newChain
    }

    //  def nestedChain(parentChainRef: ChainRef, pos: Position): Chain = {
    //    val chainDef = ChainDef.Nested(UUID.randomUUID(), publicKey, parentChainRef, pos)
    //    val chainDefToken = JwtTokenAsym[ChainDef](chainDef, privateKey)
    //    val chainRef = ChainRef(chainDefToken)
    //    val
    //    val shackle = Shackle.Seed(chainRef, publicKey)
    //    val shackleToken = JwtTokenSym[Shackle](shackle, chainRef.sig)
    //    val newChain = new Chain(chainDefToken, -1, ChainStatus.Created, shackleToken)
    //    chains += (chainRef -> newChain)
    //    newChain
    //  }
  }

}
