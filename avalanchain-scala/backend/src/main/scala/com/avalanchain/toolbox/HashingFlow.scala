package com.avalanchain.toolbox

import akka.NotUsed
import akka.stream.SinkShape
import akka.stream.scaladsl.{Broadcast, Flow, Framing, GraphDSL}
import com.avalanchain.core.domain.ChainStream.{Proof, Signed}
import com.avalanchain.core.domain._

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

//  def persist[T](node: CryptoContext, chainRef: ChainRef, snapshotInterval: Int, initial: Option[T]) : Flow[T, T, NotUsed] =
//  = GraphDSL.create() { implicit builder =>
//    val B = builder.add(Broadcast[T](sinks.length))
//
//    sinks.foreach(sink => B ~> builder.add(sink))
//
//    SinkShape(B.in)
//  }.named("broadcastSink")
}
