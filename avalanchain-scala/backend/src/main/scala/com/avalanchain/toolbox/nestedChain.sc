import akka.NotUsed
import akka.actor.ActorSystem
import akka.stream.{ActorMaterializer, FlowShape, SourceShape}
import akka.stream.scaladsl.{BroadcastHub, Flow, GraphDSL, MergeHub, RunnableGraph, Sink, Source, Unzip}
import com.avalanchain.core.domain._

implicit val system = ActorSystem()
implicit val materializer = ActorMaterializer()

// sid - sessionId
case class StreamMessage[T](pos: Version, msg: T)
sealed trait StreamResponse { def pos: Version }
object StreamResponse {
  case class Ack(pos: Version) extends StreamResponse
  case class Failed(pos: Version) extends StreamResponse
  case class Ignored(pos: Version) extends StreamResponse
  case class Replay(pos: Version) extends StreamResponse
  //case class FailedReplay(sid: Id, pos: Long) extends StreamResponse // TODO: add?
}

import StreamResponse._

def sinkFlow[T](initPos: Version): Flow[StreamMessage[T], StreamResponse, Source[StreamMessage[T], NotUsed]] = {
  var pos = initPos

  def process(msg: StreamMessage[T]): (StreamResponse, Option[StreamMessage[T]]) = {
    if (msg.pos < pos) (Ignored(msg.pos), None)
    else if (msg.pos == pos + 1) {
      pos += 1
      (Ack(pos), Some(msg))
    }
    else (Replay(pos), None)
  }

  val flow = Flow.fromFunction(process)
  //Flow.

  val

  // Simple way to create a graph backed Source
  val flow1: Flow[StreamMessage[T], StreamResponse, Source[StreamMessage[T], NotUsed]] =
    Flow.fromGraph( GraphDSL.create() { implicit builder =>
      val unzip = builder.add(Unzip())
      //Source.single(0)      ~> unzip
      //Source(List(2, 3, 4)) ~> unzip
      unzip.out0 ~> Sink.foreach(println)

      // Exposing exactly one output port
      FlowShape(unzip.in, unzip.out1)
    })

//  val runnableGraph: RunnableGraph[Sink[Int, NotUsed]] =
//    MergeHub.source[Int](perProducerBufferSize = 16).to(consumer)

//  val runnableGraph: RunnableGraph[(Source[Int, NotUsed], Source[Int, NotUsed])] =
//    (BroadcastHub.sink[Int](bufferSize = 16),
//      BroadcastHub.sink[Int](bufferSize = 16))

  val unzipGraph: RunnableGraph[Sink[Int, NotUsed]] =
    MergeHub.source[Int](perProducerBufferSize = 16).to(consumer)


  flow1
}

//def sinkFlow:  =
//  Flow.fromFunction()

// A simple consumer that will print to the console for now
val consumer = Sink.foreach(println)

// Attach a MergeHub Source to the consumer. This will materialize to a
// corresponding Sink.
val runnableGraph: RunnableGraph[Sink[Int, NotUsed]] =
  MergeHub.source[Int](perProducerBufferSize = 16).to(consumer)

// By running/materializing the consumer we get back a Sink, and hence
// now have access to feed elements into it. This Sink can be materialized
// any number of times, and every element that enters the Sink will
// be consumed by our consumer.
val toConsumer: Sink[Int, NotUsed] = runnableGraph.run()

// Feeding two independent sources into the hub.
Source.single(-1).runWith(toConsumer)
Source.single(-2).runWith(toConsumer)

Source(0 until 20).runWith(toConsumer)

//def cons()

