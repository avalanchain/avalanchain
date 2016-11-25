package com.avalanchain.jwt.utils

import akka.NotUsed
import akka.actor.ActorSystem
import akka.stream.{UniformFanInShape, UniformFanOutShape, Graph, ActorMaterializer}
import akka.stream.scaladsl._


/**
  * Created by Yuriy Habarov on 25/11/2016.
  */
object StreamStages {

//  def bsink(idx: Int) = Sink.foreach[Int](e => println(s"$idx - $e"))

  def FanOutSink[T](n: Int, foSink: Int => Sink[T, _])(strategy: Int => Graph[UniformFanOutShape[T, T], NotUsed]): Sink[T, NotUsed] = {
    if (n < 1) throw new RuntimeException("FanOut n should be > 1")
    if (n == 1) foSink(0).mapMaterializedValue(_ => NotUsed)
    else Sink.combine(foSink(0), foSink(1), (2 until n).map(foSink(_)).toSeq: _*)(strategy)
  }

  def BroadcastSink[T](n: Int, foSink: Int => Sink[T, _]): Sink[T, NotUsed] = {
    FanOutSink[T](n, foSink)(Broadcast[T](_))
  }

  //Source(0 until 10).runWith(BroadcastSink[Int](5, bsink))




  def FanInSource[T](n: Int, fiSource: Int => Source[T, _])(strategy: Int => Graph[UniformFanInShape[T, T], NotUsed]): Source[T, NotUsed] = {
    if (n < 1) throw new RuntimeException("FanIn n should be > 1")
    if (n == 1) fiSource(0).mapMaterializedValue(_ => NotUsed)
    else Source.combine(fiSource(0), fiSource(1), (2 until n).map(fiSource(_)).toSeq: _*)(strategy)
  }

  def MergeSource[T](n: Int, fiSource: Int => Source[T, _]): Source[T, NotUsed] = {
    FanInSource[T](n, fiSource)(Merge[T](_))
  }


  def ConcatSource[T](n: Int, fiSource: Int => Source[T, _]): Source[T, NotUsed] = {
    FanInSource[T](n, fiSource)(Concat[T](_))
  }


  //def ZipSource[T](n: Int, fiSource: Int => Source[T, _]): Source[T, NotUsed] = {
  //  FanInSource[T](n, fiSource)(ZipN[T](_))
  //}

}
