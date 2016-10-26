package com.avalanchain.toolbox

import akka.actor.ActorSystem
import akka.stream.ActorMaterializer
import akka.stream.scaladsl.{Flow, Framing, Source, Tcp}
import akka.stream.scaladsl.Tcp.{IncomingConnection, ServerBinding}
import akka.util.ByteString

import scala.concurrent.Future
import scala.io.StdIn

/**
  * Created by Yuriy Habarov on 21/10/2016.
  */
object REPL {
  implicit val system = ActorSystem()
  implicit val materializer = ActorMaterializer()

  def echoServer(host: String, port: Int) = {
    val connections: Source[IncomingConnection, Future[ServerBinding]] =
      Tcp().bind(host, port)
    connections runForeach { connection =>
      println(s"New connection from: ${connection.remoteAddress}")

      // server logic, parses incoming commands
      val commandParser = Flow[String]
        .map(text => { println(s"${connection.remoteAddress}: '$text'"); text })
        .takeWhile(_ != "BYE")
        .map(_ + "!")

      import connection._
      val welcomeMsg = s"Welcome to: $localAddress, you are: $remoteAddress!"
      val welcome = Source.single(welcomeMsg)

      val serverLogic = Flow[ByteString]
        .via(Framing.delimiter(
          ByteString("\n"),
          maximumFrameLength = 256,
          allowTruncation = true))
        .map(_.utf8String)
        .via(commandParser)
        // merge in the initial banner after parser
        .merge(welcome)
        .map(_ + "\n")
        .map(ByteString(_))

      connection.handleWith(serverLogic)
    }
    println(s"Server started listerning host: '$host' port '$port'")
  }

  def echoClient(host: String, port: Int) = {
    val connection = Tcp().outgoingConnection(host, port)

    val replParser =
      Flow[String].takeWhile(_ != "q")
        .concat(Source.single("BYE"))
        .map(elem => ByteString(s"$elem\n"))

    val repl = Flow[ByteString]
      .via(Framing.delimiter(
        ByteString("\n"),
        maximumFrameLength = 256,
        allowTruncation = true))
      .map(_.utf8String)
      .map(text => println("Server: " + text))
      .map(_ => StdIn.readLine("> "))
      .via(replParser)

    connection.join(repl).run()
    println(s"Client connected to Server host: '$host' port '$port'")
  }
}

object EchoServer extends App {
  REPL.echoServer("127.0.0.1", 9888)
}

object EchoClient extends App {
  REPL.echoClient("127.0.0.1", 9888)
}
