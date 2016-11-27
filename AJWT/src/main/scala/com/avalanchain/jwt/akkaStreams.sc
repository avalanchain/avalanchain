import java.util.UUID

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
import com.avalanchain.jwt.basicChain._
import com.avalanchain.jwt.jwt.CurveContext
import com.avalanchain.jwt.utils._
import com.typesafe.config.ConfigFactory

import scala.concurrent.Future
import io.circe.{Decoder, DecodingFailure, Encoder, Json}
import io.circe.syntax._
import io.circe.parser._
import io.circe.generic.auto._

val keyPair = CurveContext.currentKeys

//import scala.util.matching.Regex
//val str = new String(keyPair.getPublic.toString).toCharArray
//val str1 = new String(keyPair.getPublic.toString.toCharArray.map(_.toByte).filter(b => b != 10 && b != 13).map(_.toChar))
//println(str)
////val pattern = ".+X: ([0-9a-e]).+Y: ([0-9a-e])".r
//val pattern = """.*X: ([0-9a-f]+) +Y: ([0-9a-f]+).*""".r
//val pattern(x, y) = "EC Public Key            X: 5824f0cdb85c Y: 6824f0cdb85c "
//val pattern(x2, y2) = "EC Public Key            X: 5824f0cdb85c16a002a9eca35477be0a11623d047dfb7d0ddab99e49e7ca8efe            Y: 3b5c55ed06531ffa993d0f855fa9a53d9683dca3dbe870c489bdafc160bd568c"
//val pattern(x1, y1) = str1
////val pattern(x3, y3) = str
//println(s"x: $x")
//println(s"x1: $x1")
////println(s"y: $y")

val jsn = Json.fromString("{ \"a\": 5 }")
jsn.toString()
println(jsn.noSpaces)
println(jsn.asString.get)

val s = """a"a" a """


val chainDef: ChainDef = ChainDef.New(JwtAlgo.ES512, UUID.randomUUID(), keyPair.getPublic, Some(Json.fromString("{ \"a\": 5 }")))
val j = chainDef.asJson
println(j.toString())



//val source = Source.single(1)
//val sink = Sink.foreach[Int](e => println(e))
//
//val rg = source.to(sink)
//
//val nu = rg.run()
//
//
//val source2 = source.map(_ + 2)
//
//val flow = Flow[Int].map(_.toString)
//val sink2 = Sink.foreach[String](e => println(e))
//val sink3 = Flow[String].map(_ + " privet").to(sink2)
//
//val rg2 = source2.via(flow).to(sink3).run()
//
//val source4: Source[Int, SourceQueueWithComplete[Int]] = Source.queue[Int](10, OverflowStrategy.backpressure)
//
//val q: SourceQueueWithComplete[Int] = source4.via(flow).alsoTo(Flow[String].map(_ + " 20").to(sink2)).to(sink3).run()
//
//import akka.stream.OverflowStrategy
//import akka.stream.scaladsl.{Flow, Sink, Source, SourceQueueWithComplete}
//
//import scala.concurrent.ExecutionContext.Implicits.global
//import scala.concurrent.Future
//Future {
//  (0 until 10000).foreach(q.offer(_))
//}
//Future {
//  (0 until 10000).foreach(q.offer(_))
//}
//
//
//
//
//import javax.script.ScriptEngineManager
//
//val engine = new ScriptEngineManager().getEngineByMimeType("text/javascript")
//val result = engine.eval("1 + 1")
//println(result)