package com.avalanchain.toolbox

/**
  * Created by Yuriy Habarov on 27/04/2016.
  */
import java.security.MessageDigest

import akka.stream.{Attributes, FlowShape, Inlet, Outlet}
import akka.stream.stage._
import akka.util.ByteString

class StreamDigestCalculator(algorithm: String) extends GraphStage[FlowShape[ByteString, ByteString]] {
  val in = Inlet[ByteString]("DigestCalculator.in")
  val out = Outlet[ByteString]("DigestCalculator.out")
  override val shape = FlowShape.of(in, out)

  override def createLogic(inheritedAttributes: Attributes): GraphStageLogic = new GraphStageLogic(shape) {
    val digest = MessageDigest.getInstance(algorithm)

    setHandler(out, new OutHandler {
      override def onPull(): Unit = {
        pull(in)
      }
    })

    setHandler(in, new InHandler {
      override def onPush(): Unit = {
        val chunk = grab(in)
        digest.update(chunk.toArray)
        pull(in)
      }

      override def onUpstreamFinish(): Unit = {
        emit(out, ByteString(digest.digest()))
        completeStage()
      }
    })
  }
}
