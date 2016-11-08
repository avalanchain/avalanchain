//import com.roundeights.hasher.Hasher
import akka.actor.{ActorSystem, Props}
import akka.stream.{ActorMaterializer, OverflowStrategy}
import akka.stream.scaladsl.{Sink, Source}
import akka.util.ByteString
import com.avalanchain.core.builders.CryptoContextSettingsBuilder.CryptoContextSettings
import com.avalanchain.core.builders.CryptoContextSettingsBuilder.CryptoContextSettings._
import com.avalanchain.core.chain.{ChainRefData, MerkledRef}
import com.avalanchain.core.chainFlow.{ChainFlow, ChainPersistentActor}
import com.avalanchain.core.domain.{BytesSerializer, Hash, HashedValue}
import com.typesafe.config.ConfigFactory

import scala.collection.immutable
import scala.language.postfixOps


val config =
  """
    |akka {
    |
    |  # Loggers to register at boot time (akka.event.Logging$DefaultLogger logs
    |  # to STDOUT)
    |  loggers = ["akka.event.slf4j.Slf4jLogger"]
    |
    |  # Log level used by the configured loggers (see "loggers") as soon
    |  # as they have been started; before that, see "stdout-loglevel"
    |  # Options: OFF, ERROR, WARNING, INFO, DEBUG
    |  loglevel = "DEBUG"
    |
    |  # Log level for the very basic logger activated during ActorSystem startup.
    |  # This logger prints the log messages to stdout (System.out).
    |  # Options: OFF, ERROR, WARNING, INFO, DEBUG
    |  stdout-loglevel = "DEBUG"
    |
    |  # Filter of log events that is used by the LoggingAdapter before
    |  # publishing log events to the eventStream.
    |  logging-filter = "akka.event.slf4j.Slf4jLoggingFilter"
    |
    |  persistence.journal.plugin = "akka.persistence.journal.leveldb"
    |  persistence.snapshot-store.plugin = "akka.persistence.snapshot-store.local"
    |
    |  persistence.journal.leveldb.dir = "target/state/journal"
    |  persistence.snapshot-store.local.dir = "target/state/snapshots"
    |
    |# DO NOT USE THIS IN PRODUCTION !!!
    |# See also https://github.com/typesafehub/activator/issues/287
    |  persistence.journal.leveldb.native = false
    |
    |}
  """.stripMargin

implicit val system: ActorSystem = ActorSystem("test-akka-sys", ConfigFactory.parseString(config))
implicit val materializer = ActorMaterializer()

implicit val serializerI: BytesSerializer[Int] = i => ByteString(i.toString)
implicit val serializerMR: BytesSerializer[MerkledRef] = i => ByteString(i.toString)
implicit val serializerH: BytesSerializer[ChainRefData] = i => ByteString(i.toString)

val simpleStream = ChainFlow.create[Int]("ints", Source(1 until 1000), Some(0))

//val filtered = simpleStream.filter(_ % 10 == 0, 0)

//val mapped = filtered.map(_ / 10, 0).groupBy(x => (x % 10).toString(), 10, None)

val ss = simpleStream.eventStream().runForeach(e => println("DONE:" + e))

val aa = simpleStream.eventStream().to(Sink.foreach(e => println("DONE1:" + e)))
aa.run()
ss.isCompleted
//Source(1 until 1000).runForeach(println(_))

def hasher[T](v: T)(implicit serializerI: BytesSerializer[T], serializerMR: BytesSerializer[MerkledRef]) = HashedValue[T](Hash.Zero, v)

val bytes2Hexed = CryptoContextSettings.bytes2Hexed

val pa = Props(new ChainPersistentActor[Int](simpleStream.chainRef, None, 10, 10)(hasher[Int], hasher[MerkledRef], bytes2Hexed))
val ar = system.actorOf(pa, "ar")

ar ! "print"

ar ! HashedValue[Int](Hash.Zero, 3)

val h3: HashedValue[Int] = 3

//val sinkActor = Sink.foreach(println)

val stream = Source(1 until 1000).map(e => HashedValue[Int](Hash.Zero, e)).runWith(Sink.actorSubscriber(pa))


val q = Source.queue[Int](10, OverflowStrategy.backpressure).map(e => HashedValue[Int](Hash.Zero, e)).to(Sink.actorSubscriber(pa)).run()

q.offer(1)
