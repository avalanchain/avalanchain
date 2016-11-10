import akka.NotUsed
import akka.actor.{Actor, ActorLogging, ActorSystem, Props}
import akka.event.Logging
import akka.persistence.{PersistentActor, SnapshotOffer}
import akka.stream.{ActorMaterializer, Materializer, OverflowStrategy}
import akka.stream.scaladsl.{Flow, Keep, Sink, Source, SourceQueueWithComplete}
import akka.persistence.query.PersistenceQuery
import akka.persistence.inmemory.query.scaladsl.InMemoryReadJournal
import akka.stream.actor.{ActorSubscriber, MaxInFlightRequestStrategy}
import akka.stream.actor.ActorSubscriberMessage.OnNext
import com.avalanchain.jwt.basicChain.{ChainState, FrameRef, FrameToken}
import com.avalanchain.jwt.utils._
import com.typesafe.config.ConfigFactory

import scala.concurrent.Future


val source = Source.single(1)
val sink = Sink.foreach[Int](e => println(e))

val rg = source.to(sink)

val nu = rg.run()


val source2 = source.map(_ + 2)

val flow = Flow[Int].map(_.toString)
val sink2 = Sink.foreach[String](e => println(e))
val sink3 = Flow[String].map(_ + " privet").to(sink2)

val rg2 = source2.via(flow).to(sink3).run()

val source4: Source[Int, SourceQueueWithComplete[Int]] = Source.queue[Int](10, OverflowStrategy.backpressure)

val q: SourceQueueWithComplete[Int] = source4.via(flow).alsoTo(Flow[String].map(_ + " 20").to(sink2)).to(sink3).run()

import akka.stream.OverflowStrategy
import akka.stream.scaladsl.{Flow, Sink, Source, SourceQueueWithComplete}

import scala.concurrent.ExecutionContext.Implicits.global
import scala.concurrent.Future
Future {
  (0 until 10000).foreach(q.offer(_))
}
Future {
  (0 until 10000).foreach(q.offer(_))
}

val aa = source4.to(Sink.actorRefWithAck())

val rg2 = source2.via(flow).to(sink3).run()



import javax.script.ScriptEngineManager

val engine = new ScriptEngineManager().getEngineByMimeType("text/javascript")
val result = engine.eval("1 + 1")
println(result)