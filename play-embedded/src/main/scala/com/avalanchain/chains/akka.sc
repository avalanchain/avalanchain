import akka.actor.Status.{Failure, Success}
import akka.actor.{Actor, ActorRef, ActorSystem, Inbox, Props}
import akka.stream.{ActorMaterializer, FlowShape, SinkShape}
import akka.stream.scaladsl._
import com.typesafe.config.ConfigFactory

import scala.concurrent._
import ExecutionContext.Implicits.global
import scala.collection.concurrent.TrieMap
import scala.collection.mutable.{ArrayBuffer, ListBuffer}
import scala.concurrent.Future
import scala.concurrent.duration._
import scala.language.reflectiveCalls

def time[A](f: => A) = {
  val s = System.nanoTime
  val ret = f
  println("time: "+(System.nanoTime-s)/1e6+"ms")
  ret
}

def st() = System.nanoTime
def et(st: Long) = println("time: "+(System.nanoTime-st)/1e6+"ms")



implicit val system = ActorSystem("test-akka-sys")
implicit val materializer = ActorMaterializer()


val sourceFromIterable = Source(List(1,2,3))
val sourceFromFuture = Source.fromFuture(Future.successful("hello"))
val sourceWithSingleElement = Source.single("just one")
val sourceEmittingTheSameElement = Source.repeat("again and again")
val emptySource = Source.empty


val sinkPrintingOutElements = Sink.foreach[Int](println(_))

val sinkCalculatingASumOfElements = Sink.fold[Int, Int](0)(_ + _)
val sinkReturningTheFirstElement = Sink.head
val sinkNoop = Sink.ignore

val sourceFromRange = Source(1 to 1000)
def run[LT <: { def length: Int }] (sink: Sink[Int, Future[LT]]) = {
  val s = st()
  val b = sourceFromRange.runWith(sink)

  b onComplete {
    case scala.util.Success(posts) =>
      println(posts.length)
      et(s)
    case scala.util.Failure(t) =>
      println("An error has occured: " + t.getMessage)
      et(s)
  }
}

val sinkToList = Sink.fold[List[Int], Int](List.empty)((acc, x) => x :: acc)
val sinkToArrayBuffer = Sink.fold[ArrayBuffer[Int], Int](ArrayBuffer.empty)(_ += _)
val sinkToListBuffer = Sink.fold[ListBuffer[Int], Int](ListBuffer.empty)(_ += _)
val sinkToVector = Sink.fold[Vector[Int], Int](Vector.empty)(_ :+ _)

run(sinkToList)
run(sinkToArrayBuffer)
run(sinkToListBuffer)
run(sinkToVector)


val a = sourceFromRange.
  filter(x => x % 2 == 0).
  map(x => x * 2).
  runWith(sinkToList)

println(a.value)

val sourceFromRange1 = Source(1 to 1000)

//val cm = TrieMap[Int, Int]()
//val sinkToMap = Sink.fold[TrieMap[Int, Int], Int](cm)((m, v) => {
//  val vv = m.getOrElseUpdate(v, 0); m(v) = vv + 1; m })
//
//val b = sourceFromRange1.
//  map(x => x % 10).
//  groupBy(100, x => x).
//  to(sinkToMap)
//
//b.run()
//
//cm.size

//Flow.fromSinkAndSource()

import GraphDSL.Implicits._
def broadcastSink[T, Mat](sinks: List[Sink[T, Mat]]) = GraphDSL.create() { implicit builder =>
  val B = builder.add(Broadcast[T](sinks.length))

  sinks.foreach(sink => B ~> builder.add(sink))

  SinkShape(B.in)
}.named("broadcastSink")

val sinks =
  (0 until 100).map(i => Sink.foreach[Int](v => println(s"Sink $i - $v"))).toList

val b1 = sourceFromRange1.
  map(x => x % 100).
  //groupBy(100, x => x).
  to(broadcastSink(sinks))

b1.run()

////////////////////

import akka.persistence._

case class Cmd(data: String)
case class Evt(data: String)

case class ExampleState(events: List[String] = Nil) {
  def updated(evt: Evt): ExampleState = copy(evt.data :: events)
  def size: Int = events.length
  override def toString: String = events.reverse.toString
}

class ExamplePersistentActor(id: String) extends PersistentActor {
  override def persistenceId = id

  var state = ExampleState()

  def updateState(event: Evt): Unit =
    state = state.updated(event)

  def numEvents =
    state.size

  val receiveRecover: Receive = {
    case evt: Evt                                 => updateState(evt)
    case SnapshotOffer(_, snapshot: ExampleState) => state = snapshot
  }

  val receiveCommand: Receive = {
    case Cmd(data) =>
      persist(Evt(s"${data}-${numEvents}"))(updateState)
      persist(Evt(s"${data}-${numEvents + 1}")) { event =>
        updateState(event)
        context.system.eventStream.publish(event)
      }
    case "snap"  => saveSnapshot(state)
    case "print" => println(state)
  }

}

val epa = system.actorOf(Props(new ExamplePersistentActor("")))