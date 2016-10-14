package com.avalanchain.toolbox

import java.io.File

import akka.NotUsed
import akka.actor.Props
import akka.stream.{FlowShape, IOResult}
import akka.stream.scaladsl.{Broadcast, FileIO, Flow, GraphDSL, Sink, Source}
import com.avalanchain.core.chainFlow.ChainPersistentActor
import com.avalanchain.core.domain.ChainStream.Proof
import com.avalanchain.core.domain._
import GraphDSL.Implicits._
import akka.util.ByteString
import com.avalanchain.core.domain.ChainStream.Proofed.Signed

import scala.concurrent.Future

/**
  * Created by Yuriy Habarov on 27/04/2016.
  */

object HashingFlow {

  def hash[In](implicit hasher: Hasher[In]) : Flow[In, HashedValue[In], NotUsed] =
    Flow[In].
      map(hasher(_))

  def sign[In](implicit signer: Signer[In]) : Flow[In, Signed[In], NotUsed] =
    Flow[In].
      //.via(Framing.simpleFramingProtocolDecoder()
      map(signer(_))

  def verify[In](implicit verifier: Verifier[In]) : Flow[(Proof, In), Verified[In], NotUsed] =
    Flow[(Proof, In)].
      map(x => verifier(x._1, x._2))

  def persistSink[In](chainRefProvider: ChainRefProvider, snapshotInterval: Int = 1000, maxInFlight: Int = 1000)(implicit node: CryptoContext) : Sink[In, Any] =
    Sink.actorSubscriber(Props(new ChainPersistentActor(node, chainRefProvider(), None, snapshotInterval, maxInFlight)))

  def persist[In](chainRefProvider: ChainRefProvider, snapshotInterval: Int = 1000, maxInFlight: Int = 1000)(implicit node: CryptoContext) : Flow[In, In, NotUsed] =
    Flow.fromGraph(
      GraphDSL.create() { implicit builder =>

        val broadcast = builder.add(Broadcast[In](2))
        val sink = persistSink[In](chainRefProvider, snapshotInterval, maxInFlight)

        broadcast.out(0) ~> builder.add(sink)

        // expose ports
        FlowShape(broadcast.in, broadcast.out(1))
      }.named("persistFlow"))

  def toByteString[T](implicit serializer: Serializer[T]) : Flow[T, ByteString, NotUsed] =
    Flow[T].
      map(serializer(_)._2).
      map(ByteString(_))

  def fromFile(file: File, chunkSize: Int = 8192): Source[ByteString, Future[IOResult]] = FileIO.fromFile(file, chunkSize)

  def fromFolder(folder: File): Array[Source[ByteString, Future[IOResult]]] = {
    def getRecursiveListOfFiles(dir: File): Array[File] = {
      val these = dir.listFiles
      these ++ these.filter(_.isDirectory).flatMap(getRecursiveListOfFiles)
    }

    getRecursiveListOfFiles(folder).map(fromFile(_))
  }
}
