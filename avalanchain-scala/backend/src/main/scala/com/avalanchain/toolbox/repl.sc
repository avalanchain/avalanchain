import java.nio.charset.StandardCharsets

import akka.actor.{Actor, ActorSystem}
import akka.stream.ActorMaterializer
import akka.stream.scaladsl.{Flow, Source, Tcp}
import akka.stream.scaladsl.Tcp.{IncomingConnection, ServerBinding}
import akka.util.ByteString
import cats.data.Xor
import com.avalanchain.core.builders.{CryptoContextBuilder, CryptoContextSettingsBuilder}
import com.avalanchain.core.domain.Proofed.Signed
import com.avalanchain.core.domain._
import com.avalanchain.core.toolbox.{CirceEncoders, Pipe}
import com.avalanchain.toolbox.REPL
import CirceEncoders._
import scorex.crypto.signatures.Curve25519
import Pipe._
import io.circe._
import io.circe.generic.auto._
import io.circe.parser._
import io.circe.syntax._
import sun.nio.cs.StandardCharsets

import scala.concurrent.Future


implicit val ccs = CryptoContextSettingsBuilder.CryptoContextSettings
val (priv, pub) = CryptoContextBuilder.keysGenerator()()
priv.key |> ccs.bytes2Hexed
pub.key |> ccs.bytes2Hexed


//
//val curve = new Curve25519
//
//val (context, privKey) = CryptoContextBuilder()
//
//def toHexedK(key: SecurityKey) = context.bytes2Hexed(key.key)
//def toHexedH(hashedValue: HashedValue) = context.bytes2Hexed(hashedValue.hash.hash)
//
//val pubKey = context.signingPublicKey
//
//println(toHexedK(pubKey))
//println(toHexedK(privKey))
//
//util.Properties.versionString
//
//val a = pubKey |> toHexedK |> (context.hexed2Bytes) |> context.bytes2Hexed
//
//val b = false |> not
//
//val a1 = pubKey |> toHexedK
//println(a1)
//
//val str = "Hi"
//val hashed = context.hasher(context.string2Bytes(str))
//println(toHexedH(hashed))
//val hashed2 = context.hasher(context.string2Bytes(str))
//println(toHexedH(hashed2))
//hashed.value sameElements hashed2.value
//
//val signed = context.signer(context.string2Bytes(str))
//println(signed.proof)
//val verified = context.verifier(signed.proof, signed.value)
//println(verified)
//
//implicit def bytes2Hexed = context.bytes2Hexed
//implicit def hexed2Bytes = context.hexed2Bytes
//
//final case class SignedMessage(message: Signed)
//
//val json = signed.asJson.spaces2
//
//val s = decode[Signed](json).toOption.get
//
//val same = signed.equals(s)
//
//val sm = SignedMessage(signed)
//val json1 = sm.asJson.spaces2
//val s1 = decode[SignedMessage](json1).toOption.get
//val same1 = sm.equals(s1)
//
////val json = pubKey.asJson.noSpaces
//
////val signed = context.signer(str)
////
////println(signed)
//
/////////
////implicit val system = ActorSystem()
////implicit val materializer = ActorMaterializer()
//
///////// Signed communication
//
////val echoS = REPL.echoServer("127.0.0.1", 10888)
////val echoC = REPL.echoClient("127.0.0.1", 10888)
