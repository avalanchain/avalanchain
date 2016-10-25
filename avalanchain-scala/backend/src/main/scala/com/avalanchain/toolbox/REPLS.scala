package com.avalanchain.toolbox

import java.net.InetSocketAddress
import java.nio.charset.StandardCharsets

import akka.actor.ActorSystem
import akka.stream.ActorMaterializer
import akka.stream.scaladsl.{Flow, Framing, Source, Tcp}
import akka.stream.scaladsl.Tcp.{IncomingConnection, OutgoingConnection, ServerBinding}
import akka.util.ByteString
import com.avalanchain.core.builders.{CryptoContextBuilder, CryptoContextSettingsBuilder}
import com.avalanchain.core.domain._
import com.avalanchain.core.domain.Proofed.Signed
import com.avalanchain.core.toolbox.Pipe._

import scala.concurrent.Future
import org.joda.time.DateTime
import io.circe._
import io.circe.generic.auto._
import io.circe.parser._
import io.circe.syntax._
import cats._
import cats.data.Xor
import cats.syntax.flatMap._
import com.avalanchain.core.domain.Verified.{HashCheckFailed, Passed, ProofCheckFailed, PublicKeyNotValid}
import com.avalanchain.core.toolbox.{CirceEncoders, Pipe}
import com.avalanchain.core.toolbox.CirceEncoders._

import scala.io.StdIn
import scala.util.{Failure, Success}
import scala.concurrent.ExecutionContext.Implicits.global

/**
  * Created by Yuriy Habarov on 21/10/2016.
  * REPL Signed
  */
class REPLS(cryptoContext: CryptoContext, implicit val ccs: CryptoContextSettings, implicit val system: ActorSystem, implicit val materializer: ActorMaterializer) {

  case class ChatMessage(message: String, time: DateTime)
  case class SignedMessage(signed: Signed)

  implicit private val bytes2Hexed = ccs.bytes2Hexed
  implicit private val hexed2Bytes = ccs.hexed2Bytes

  private val string2Bytes: String2Bytes = _.getBytes(StandardCharsets.UTF_8) |> (ByteWord(_))
  private val bytes2String: Bytes2String = _.utf8String

  val signMessage =
    Flow[String].map(_ |> string2Bytes |> (cryptoContext.signer(_)) |> (SignedMessage(_)) |> (_.asJson.toString))
      .map(text => { println(s"Sending: '$text'"); text })
      .map(ByteString(_))
  def unwrapMessage(remoteAddress: String) =
    Flow[ByteString].map(_.utf8String)
      .map(text => { println(s"${remoteAddress}: Message received: '$text'"); text })
      .map(decode[SignedMessage](_).leftMap(e => s"SignedMessage parsing failed: '$e'")
        .flatMap(sm => cryptoContext.verifier(sm.signed.proof, sm.signed.value) match {
          case Passed(value) => Xor.right(sm)
          case HashCheckFailed(actual, expected) => Xor.left(s"Signature hash verification failed. Actual: '$actual'. Expected: '$expected'")
          case PublicKeyNotValid(key, tick) => Xor.left(s"Signature public key '${key.toHexed}' unknown or invalid at tick '$tick'")
          case ProofCheckFailed => Xor.left("Signature proof verification failed")
        })
        .map(_.signed.value |> bytes2String)
      )

  def serialize(chatMessage: ChatMessage) = chatMessage.asJson.spaces2
  def deserialize(msg: Xor[String, String]): Xor[String, ChatMessage] =
    msg.flatMap(decode[ChatMessage](_)).leftMap(e => s"ChatMessage parsing failed: '$e'")

  def serializeXor(chatMessage: Xor[String, ChatMessage]) = chatMessage.asJson.spaces2
  def deserializeXor(msg: Xor[String, String]): Xor[String, Xor[String, ChatMessage]] =
    msg.flatMap(decode[Xor[String, ChatMessage]](_)).leftMap(e => s"Xor[String, ChatMessage] parsing failed: '$e'")

  def echoServer(host: String, port: Int) = {
    val connections: Source[IncomingConnection, Future[ServerBinding]] =
      Tcp().bind(host, port)
    connections runForeach { connection =>
      println(s"New connection from: ${connection.remoteAddress}")

      // server logic, parses incoming commands
      val commandParser = Flow[ByteString]
        .via(unwrapMessage(connection.remoteAddress.toString))
        .map(deserialize(_))
        .map(tcm => {
          tcm match {
            case Xor.Right(cm) => println(s"${connection.remoteAddress}: Message deserialized: '$cm'")
            case Xor.Left(msg) => println(s"${connection.remoteAddress}: $msg")
          }
          tcm
        })
        //.takeWhile(_ != "BYE")
        .takeWhile {
          case Xor.Right(cm) if cm.message == "BYE" =>
            println(s"${connection.remoteAddress}: Client sent a disconnection message. Closing connection.")
            false
          case _ => true
        }

      import connection._
      val welcomeMsg = s"Welcome to: $localAddress, you are: $remoteAddress!"
      val welcome = Source.single(welcomeMsg).map(ChatMessage(_, DateTime.now())).map(Xor.right(_))

      val serverLogic = Flow[ByteString]
//        .via(Framing.delimiter(
//          ByteString("\n"),
//          maximumFrameLength = 256,
//          allowTruncation = true))
        .via(commandParser)
        // merge in the initial banner after parser
        .merge(welcome)
//        .map(_ + "\n")
        //.map(ByteString(_))
        .map(m => serializeXor(m))
        .via(signMessage)

      connection.handleWith(serverLogic)
    }
    println(s"Server started listerning host: '$host' port '$port'")
  }

  def echoClient(host: String, port: Int) = {
    val connection = Tcp().outgoingConnection(host, port)

    val replParser =
      Flow[String].takeWhile(e => e != "q" && e != "BYE")
        .concat(Source.single("BYE"))
        .map(ChatMessage(_, DateTime.now()))
        .map(serialize)
        .via(signMessage)


    val repl = Flow[ByteString]
      //        .via(Framing.delimiter(
      //          ByteString("\n"),
      //          maximumFrameLength = 256,
      //          allowTruncation = true))
      //.map(_.utf8String)
      .via(unwrapMessage("Server"))
      .map(deserializeXor)
      .map(xcm => {
        xcm match {
          case Xor.Right(cm) => println(s"Message deserialized: '$cm'")
          case Xor.Left(msg) => println(s"$msg")
        }
        xcm
      })
      .map(_ => StdIn.readLine("> "))
      .via(replParser)

    val outgoingConnection = connection.join(repl).run()
    println(s"Client connected to Server host: '$host' port '$port'")
    outgoingConnection.onComplete { // TODO: Doesn't work. Fix it.
      case Success(s) => println(s"Connection successfully closed with message: $s")
      case Failure(s) => println(s"Connection failed with message: $s")
    }
  }
}

object SignedEchoServer extends App {
  import CryptoContextSettingsBuilder.CryptoContextSettings._
  implicit val ccs = CryptoContextSettingsBuilder.CryptoContextSettings
  val priv = "BHpiB7Zpanb76Unue5bqFaiVD3atAQY4EBi1CzpBvNns" |> (PrivateKey(_))
  val pub = "8rAwg7esrUog6UhWJWfrzY91cnhXf4LeaaH3J79aS2ug" |> (PublicKey(_))
  val ctx = CryptoContextBuilder.createCryptoContext(priv, pub, Set("2g9rtvxp3FugrRjawtk8DeuHGsDq3CfnjasnTrbwi95X").map(PublicKey(_)))
  implicit val system = ActorSystem()
  implicit val materializer = ActorMaterializer()
  new REPLS(ctx, ccs, system, materializer).echoServer("127.0.0.1", 9888)
}

object SignedEchoClient extends App {
  import CryptoContextSettingsBuilder.CryptoContextSettings._
  implicit val ccs = CryptoContextSettingsBuilder.CryptoContextSettings
  val priv = "8ZDAKa2B1YCL6qTnqFEBcwTSUaN7yfihXJpJ3Tr1Fg7e" |> (PrivateKey(_))
  val pub = "2g9rtvxp3FugrRjawtk8DeuHGsDq3CfnjasnTrbwi95X" |> (PublicKey(_))
  val ctx = CryptoContextBuilder.createCryptoContext(priv, pub, Set("8rAwg7esrUog6UhWJWfrzY91cnhXf4LeaaH3J79aS2ug").map(PublicKey(_)))
  implicit val system = ActorSystem()
  implicit val materializer = ActorMaterializer()
  new REPLS(ctx, ccs, system, materializer).echoClient("127.0.0.1", 9888)
}

