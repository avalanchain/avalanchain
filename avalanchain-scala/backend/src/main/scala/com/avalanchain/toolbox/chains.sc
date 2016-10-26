//import com.roundeights.hasher.Hasher
import akka.actor.ActorSystem
import akka.stream.ActorMaterializer
import akka.stream.scaladsl.Source
import com.avalanchain.core.chainFlow.ChainFlow
import com.typesafe.config.ConfigFactory

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

implicit val system = ActorSystem("test-akka-sys", ConfigFactory.parseString(config))
implicit val materializer = ActorMaterializer()

val simpleStream = ChainFlow.create[Int]("ints", Source[Int](1 to 1000), Some(0))

val filtered = simpleStream.filter(_ % 10 == 0, 0)

val mapped = filtered.map(_ / 10, 0).groupBy(x => (x % 10).toString(), 10, None)
