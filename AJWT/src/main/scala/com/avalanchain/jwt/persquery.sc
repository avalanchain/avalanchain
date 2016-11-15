
import java.security.PrivateKey
import java.util.UUID

import akka.NotUsed
import akka.actor.{Actor, ActorLogging, ActorSystem, Props}
import akka.event.Logging
import akka.persistence.{PersistentActor, SnapshotOffer}
import akka.stream.{ActorMaterializer, Materializer, OverflowStrategy}
import akka.stream.scaladsl.{Flow, Keep, Sink, Source, SourceQueueWithComplete}
import akka.persistence.query.{EventEnvelope, PersistenceQuery}
import akka.persistence.inmemory.query.scaladsl.InMemoryReadJournal
import akka.persistence.journal.writer.JournalWriter
import akka.persistence.query.journal.leveldb.scaladsl.LeveldbReadJournal
import akka.stream.actor.{ActorSubscriber, MaxInFlightRequestStrategy}
import akka.stream.actor.ActorSubscriberMessage.OnNext
import com.avalanchain.jwt.actors.{ChainPersistentActor, PersistentSink}
import com.avalanchain.jwt.basicChain._
import com.avalanchain.jwt.utils._
import com.typesafe.config.ConfigFactory
import io.circe.generic.JsonCodec
import io.circe.generic.auto._
import akka.persistence.inmemory.query.InMemoryReadJournalProvider
import com.avalanchain.jwt.actors
import com.avalanchain.jwt.jwt.CurveContext._
import com.avalanchain.jwt.KeysDto._

import scala.concurrent.Future

//implicit val system: ActorSystem = ActorSystem("ac", ConfigFactory.parseString(AkkaConfigs.PersLevelDb))
implicit val system: ActorSystem = ActorSystem("ac", ConfigFactory.parseString(AkkaConfigs.PersInmem2))
implicit val mat: Materializer = ActorMaterializer()

import akka.actor.ActorDSL._

//val a = actor(new Act {
//  become {
//    case "hello" ⇒ sender() ! "hi"
//  }
//})


val privateKey: PrivateKey = savedKeys().getPrivate

val chainDef = ChainDef.New(JwtAlgo.HS512, UUID.randomUUID(), savedKeys().getPublic, None)
val chainDefToken = time { TypedJwtToken[ChainDef](chainDef, privateKey) }

val chainDef2 = ChainDef.New(JwtAlgo.ES512, UUID.randomUUID(), savedKeys().getPublic, None)
val chainDefToken2 = time { TypedJwtToken[ChainDef](chainDef2, privateKey) }


//val a = Source(0 until 10).map(e => new FrameToken(e.toString)).runWith(Sink.actorSubscriber(ChainPersistentActor.props("pid")))
val a = Source(0 until 100).map(e => new FrameToken("eyJ0eXAiOiJKV1QiLCJhbGciOiJIUzUxMiJ9.eyJjciI6eyJzaWciOiJBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBUWRpQ1lpS1kyQ1VBY2RUaVA0SjFRMlg2cHFmV0FSZF9QSzVxcVBKenl3QUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQTBJekFKaWZfdHg2QWhEVC01MklBRVNEMk9lNFZsam91cXNtazAwOWFvQyJ9LCJwb3MiOjg4NDE3LCJwcmVmIjp7InNpZyI6IlJiOW1FNS1lWXJ1MHNvQ0NRblZHS2pZQ0dqMFZEZFBqcEZ5YnMzaDVuZnI1cGI2MlBYRXM0ZUd5TEN1cFBaX3hpVVBYZWdjZ2FhXy14UWZReFV4aWZBIn0sInYiOnsiaW50Ijo4ODQxNywic3RyaW5nIjoiTnVtYmVyIDg4NDE3IiwiZG91YmxlIjoyNzc3NzAuMTk3NjUyNDQ4N319.ASEh6ElsFaunmc-Z0Dv8rxxrrLpLUd93VvVCvA1lcLGRhxZhnETFZ9obxIS1WEqsSFhiL6TntOyRHbcEJugAAQ")).
  to(PersistentSink(ChainRef(chainDefToken)).toList.head).run()

val b = Source.queue[Int](10, OverflowStrategy.backpressure).map(e => new FrameToken("eyJ0eXAiOiJKV1QiLCJhbGciOiJIUzUxMiJ9.eyJjciI6eyJzaWciOiJBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBUWRpQ1lpS1kyQ1VBY2RUaVA0SjFRMlg2cHFmV0FSZF9QSzVxcVBKenl3QUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQTBJekFKaWZfdHg2QWhEVC01MklBRVNEMk9lNFZsam91cXNtazAwOWFvQyJ9LCJwb3MiOjg4NDE3LCJwcmVmIjp7InNpZyI6IlJiOW1FNS1lWXJ1MHNvQ0NRblZHS2pZQ0dqMFZEZFBqcEZ5YnMzaDVuZnI1cGI2MlBYRXM0ZUd5TEN1cFBaX3hpVVBYZWdjZ2FhXy14UWZReFV4aWZBIn0sInYiOnsiaW50Ijo4ODQxNywic3RyaW5nIjoiTnVtYmVyIDg4NDE3IiwiZG91YmxlIjoyNzc3NzAuMTk3NjUyNDQ4N319.ASEh6ElsFaunmc-Z0Dv8rxxrrLpLUd93VvVCvA1lcLGRhxZhnETFZ9obxIS1WEqsSFhiL6TntOyRHbcEJugAAQ")).
  to(Sink.actorSubscriber(ChainPersistentActor.props("pid"))).run()




val queries = PersistenceQuery(system).readJournalFor[LeveldbReadJournal](
  LeveldbReadJournal.Identifier)

val src: Source[EventEnvelope, NotUsed] =
  queries.eventsByPersistenceId("pid", 0L, Long.MaxValue)

src.runForeach(println)

(0 until 100).foreach(b.offer(_))

import scala.concurrent.ExecutionContext.Implicits.global
Source(0 until 100).map(i => EventEnvelope(i, "eetest", i, "Message " + i)).runWith(JournalWriter.sink(InMemoryReadJournal.Identifier))


val readJournal: InMemoryReadJournal = PersistenceQuery(system).readJournalFor[InMemoryReadJournal](InMemoryReadJournal.Identifier)

val willNotCompleteTheStream: Source[String, NotUsed] = readJournal.allPersistenceIds()

val willCompleteTheStream: Source[String, NotUsed] = readJournal.currentPersistenceIds()

val allRefs = willNotCompleteTheStream.runForeach(println)

readJournal.eventsByPersistenceId("eetest", 0, 1000).runForeach(println)

readJournal.eventsByPersistenceId("pid2", 0, 1000).runForeach(println)


class MyActor extends Actor with ActorLogging {
  def receive = {
    case "p" => println("ppppp")
    case "test" => println("received test")
    case e      => println(s"received unknown message '$e'")
  }
}

val props1 = Props[MyActor]

val myActor = system.actorOf(Props[MyActor], "myactor3")

myActor ! "p"

myActor ! ""

//val pa = system.actorOf(paProps, "aaa")
//
//pa ! "print"
//

Source.single(ChainPersistentActor.GetState).runWith(Sink.actorRef(myActor,  "complete"))

Source.single(ChainPersistentActor.PrintState).runWith(Sink.actorRef(myActor,  "complete"))

//Source.single("print").runWith(Sink.actorRef(pa,  "complete"))


import javax.script.ScriptEngineManager
import javax.script.Invocable

val engine = new ScriptEngineManager().getEngineByMimeType("text/javascript")
val result = time {
  (0 until 100000).foreach(_ => engine.eval("1 + 1"))
}
println(result)

val result1 = time {
  (0 until 100000).foreach(_ => 1 + 1)
}


engine.eval("function sum(a, b) { return a + b; }");
val invocable = engine.asInstanceOf[Invocable]

invocable.invokeFunction("sum", new Integer(1), new Integer(2))

val result2 = time {
  (0 until 100000).foreach(i => invocable.invokeFunction("sum", new Integer(i + 1), new Integer(i + 2)))
}

val res = engine.eval("1 + 1")


engine.eval("function jp2(json) { var j = JSON.parse(json); j.val += 1; return JSON.stringify(j); }");
println(invocable.invokeFunction("jp2", """{ "val":  10}"""))

val result3 = time {
  (0 until 10000).foreach(i => invocable.invokeFunction("jp2", s"""{ "val":  $i}"""))
}

engine.eval("var f = function(j){return j.val + 11};")
engine.eval("function jp3(f, json) { var j = JSON.parse(json); var r = f(j); return JSON.stringify(j); }; function jp4(j) { return jp3(f, j); }");
val result4 = time {
  (0 until 10000).foreach(i => invocable.invokeFunction("jp4", s"""{ "val":  $i}"""))
}
