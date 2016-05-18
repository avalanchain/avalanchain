package com.avalanchain.toolbox

import java.util.UUID

import akka.NotUsed
import akka.actor.Props
import akka.stream.{FlowShape, SinkShape}
import akka.stream.scaladsl.{Broadcast, Flow, Framing, GraphDSL, Sink}
import com.avalanchain.core.chainFlow.ChainPersistentActor
import com.avalanchain.core.domain.ChainStream.{Hash, Proof, Signed}
import com.avalanchain.core.domain._
import GraphDSL.Implicits._

/**
  * Created by Yuriy Habarov on 27/04/2016.
  */

object HashingFlow {
  def hash[In](hasher: Hasher[In]) : Flow[In, HashedValue[In], NotUsed] =
    Flow[In].
      map(hasher(_))

  def sign[In](signer: Signer[In]) : Flow[In, Signed[In], NotUsed] =
    Flow[In].
      //.via(Framing.simpleFramingProtocolDecoder()
      map(signer(_))

  def verify[In](verifier: Verifier[In]) : Flow[(Proof, In), Verified[In], NotUsed] =
    Flow[(Proof, In)].
      map(x => verifier(x._1, x._2))

  def persistSink[In](node: CryptoContext, chainRefProvider: ChainRefProvider, snapshotInterval: Int = 1000, maxInFlight: Int = 1000) : Sink[In, Any] =
    Sink.actorSubscriber(Props(new ChainPersistentActor(node, chainRefProvider(), None, snapshotInterval, maxInFlight)))

  def persist[In](node: CryptoContext, chainRefProvider: ChainRefProvider, snapshotInterval: Int = 1000, maxInFlight: Int = 1000) : Flow[In, In, NotUsed] =
    Flow.fromGraph(
      GraphDSL.create() { implicit builder =>

        val broadcast = builder.add(Broadcast[In](2))
        val sink = persistSink[In](node, chainRefProvider, snapshotInterval, maxInFlight)

        broadcast.out(0) ~> builder.add(sink)

        // expose ports
        FlowShape(broadcast.in, broadcast.out(1))
      }.named("persistFlow"))

}
