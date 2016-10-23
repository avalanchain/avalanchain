package com.avalanchain.toolbox

import akka.actor.ActorSystem
import akka.stream.ActorMaterializer
import akka.stream.scaladsl.{Flow, Framing, Source, Tcp}
import akka.stream.scaladsl.Tcp.{IncomingConnection, OutgoingConnection, ServerBinding}
import akka.util.ByteString
import com.avalanchain.core.builders.{CryptoContextBuilder, CryptoContextSettingsBuilder}
import com.avalanchain.core.domain._
import com.avalanchain.core.domain.Proofed.Signed

import scala.concurrent.Future
import com.avalanchain.toolbox.Pipe._
import com.avalanchain.toolbox.CirceEncoders._
import org.joda.time.DateTime
import io.circe._
import io.circe.generic.auto._
import io.circe.parser._
import io.circe.syntax._
import cats._
import cats.data.Xor
import cats.syntax.flatMap._
import com.avalanchain.core.domain.Verified.{HashCheckFailed, Passed, ProofCheckFailed, PublicKeyNotValid}

import scala.io.StdIn
import scala.util.{Failure, Success}

/**
  * Created by Yuriy Habarov on 21/10/2016.
  * REPL Signed
  */
class REPLS(cryptoContext: CryptoContext, implicit val ccs: CryptoContextSettings, implicit val system: ActorSystem, implicit val materializer: ActorMaterializer) {

  implicit private val bytes2Hexed = ccs.bytes2Hexed
  implicit private val hexed2Bytes = ccs.hexed2Bytes

  case class ChatMessage(message: String, time: DateTime)
  case class SignedMessage(signed: Signed)

  def echoServer(host: String, port: Int) = {
    val connections: Source[IncomingConnection, Future[ServerBinding]] =
      Tcp().bind(host, port)
    connections runForeach { connection =>
      println(s"New connection from: ${connection.remoteAddress}")

      // server logic, parses incoming commands
      val commandParser = Flow[String]
        .map(text => { println(s"${connection.remoteAddress}: Message received: '$text'"); text })
        .map(decode[SignedMessage](_).leftMap(e => s"SignedMessage parsing failed: '$e'")
          .flatMap(sm => cryptoContext.verifier(sm.signed.proof, sm.signed.value) match {
            case Passed(value) => Xor.right(sm)
            case HashCheckFailed(actual, expected) => Xor.left(s"Signature hash verification failed. Actual: '$actual'. Expected: '$expected'")
            case PublicKeyNotValid(key, tick) => Xor.left(s"Signature public key '$key' unknown or invalid at tick '$tick'")
            case ProofCheckFailed => Xor.left("Signature proof verification failed")
          })
          .flatMap(sm => decode[ChatMessage](sm.signed.value |> (ccs.bytes2Text))).leftMap(e => s"ChatMessage parsing failed: '$e'").toEither)
        .map(tcm => {
          tcm match {
            case Right(cm) => println(s"${connection.remoteAddress}: Message deserialized: '$cm'")
            case Left(msg) => println(s"${connection.remoteAddress}: $msg")
          }
          tcm
        })
        //.takeWhile(_ != "BYE")
        .takeWhile(_ match {
          case Right(cm) if cm.message == "BYE" => false
          case _ => true
        })
        .map(_ + "!")

      import connection._
      val welcomeMsg = s"Welcome to: $localAddress, you are: $remoteAddress!"
      val welcome = Source.single(welcomeMsg)

      val serverLogic = Flow[ByteString]
//        .via(Framing.delimiter(
//          ByteString("\n"),
//          maximumFrameLength = 256,
//          allowTruncation = true))
        .map(_.utf8String)
        .via(commandParser)
        // merge in the initial banner after parser
        .merge(welcome)
//        .map(_ + "\n")
        .map(ByteString(_))

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
        .map(_.asJson.toString)
        .map(ccs.text2Bytes(_))
        .map(cryptoContext.signer(_))
        .map(SignedMessage(_))
        .map(_.asJson.toString)
        .map(text => { println(s"Sending: '$text'"); text })
        .map(ByteString(_))

    val repl = Flow[ByteString]
      //        .via(Framing.delimiter(
      //          ByteString("\n"),
      //          maximumFrameLength = 256,
      //          allowTruncation = true))
      .map(_.utf8String)
      .map(text => println("Server: " + text))
      .map(_ => StdIn.readLine("> "))
      .via(replParser)

    val outgoingConnection = connection.join(repl).run()
    println(s"Client connected to Server host: '$host' port '$port'")
//    outgoingConnection.onComplete {
//      case Success(s) => println(s"Connection successfully closed with message: $s")
//      case Failure(s) => println(s"Connection failed with message: $s")
//    }
  }
}

object SignedEchoServer extends App {
  implicit val ccs = CryptoContextSettingsBuilder.CryptoContextSettings
  val priv = "BHpiB7Zpanb76Unue5bqFaiVD3atAQY4EBi1CzpBvNns" |> (ccs.hexed2Bytes) |> (PrivateKey(_))
  val pub = "8rAwg7esrUog6UhWJWfrzY91cnhXf4LeaaH3J79aS2ug" |> (ccs.hexed2Bytes) |> (PublicKey(_))
  val ctx = CryptoContextBuilder.createCryptoContext(priv, pub, Set("8rAwg7esrUog6UhWJWfrzY91cnhXf4LeaaH3J79aS2ug"))
  implicit val system = ActorSystem()
  implicit val materializer = ActorMaterializer()
  new REPLS(ctx, ccs, system, materializer).echoServer("127.0.0.1", 9888)
}

object SignedEchoClient extends App {
  implicit val ccs = CryptoContextSettingsBuilder.CryptoContextSettings
  val priv = "BHpiB7Zpanb76Unue5bqFaiVD3atAQY4EBi1CzpBvNns" |> (ccs.hexed2Bytes) |> (PrivateKey(_))
  val pub = "8rAwg7esrUog6UhWJWfrzY91cnhXf4LeaaH3J79aS2ug" |> (ccs.hexed2Bytes) |> (PublicKey(_))
  val ctx = CryptoContextBuilder.createCryptoContext(priv, pub, Set("8rAwg7esrUog6UhWJWfrzY91cnhXf4LeaaH3J79aS2ug"))
  implicit val system = ActorSystem()
  implicit val materializer = ActorMaterializer()
  new REPLS(ctx, ccs, system, materializer).echoClient("127.0.0.1", 9888)
}

