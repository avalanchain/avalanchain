package com.avalanchain.yahoo

import akka.NotUsed
import akka.actor.ActorSystem
import akka.stream.{Materializer, SourceShape, ThrottleMode}
import akka.stream.scaladsl.{Flow, GraphDSL, RunnableGraph, Sink, Source}

import scala.concurrent.duration.{DurationInt, FiniteDuration}
import scala.concurrent.{ExecutionContextExecutor, Future}

/**
  * Created by Yuriy Habarov on 27/04/2016.
  */
object YahooFinSource {
  def apply(maxRequests: Int = Int.MaxValue, duration: FiniteDuration = 1.second) : Source[(Int, Map[String, BigDecimal]), NotUsed] = {
    Source.fromGraph(GraphDSL.create() { implicit b =>
      import GraphDSL.Implicits._

      // prepare graph elements
      val source: Source[Int, NotUsed] = Source(1 to maxRequests)

      // connect the graph
      val fxUpdates = source.
        map(i => (i, YahooStockPriceClient().stocks())).
        throttle(1, duration, 1, ThrottleMode.shaping)

      // expose port
      fxUpdates.s.shape
    })
  }
}
