import akka.NotUsed
import akka.actor.Status.{Failure, Success}
import akka.actor.{Actor, ActorRef, ActorSystem, Inbox, Props}
import akka.stream.ActorMaterializer
import akka.stream.scaladsl.{Sink, Source}
import com.typesafe.config.ConfigFactory

import scala.concurrent._
import ExecutionContext.Implicits.global
import scala.collection.mutable.{ArrayBuffer, ListBuffer}
import akka.persistence._
import akka.persistence.query.EventEnvelope

import scala.language.reflectiveCalls
import scala.language.postfixOps

def time[A](f: => A) = {
  val s = System.nanoTime
  val ret = f
  println("time: "+(System.nanoTime-s)/1e6+"ms")
  ret
}

def st() = System.nanoTime
def et(st: Long) = println("time: "+(System.nanoTime-st)/1e6+"ms")



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
    |  persistence.journal.leveldb.dir = "target/example/journal"
    |  persistence.snapshot-store.local.dir = "target/example/snapshots"
    |
    |# DO NOT USE THIS IN PRODUCTION !!!
    |# See also https://github.com/typesafehub/activator/issues/287
    |  persistence.journal.leveldb.native = false
    |
    |}
  """.stripMargin

val system = ActorSystem("test-akka-sys", ConfigFactory.parseString(config))

case class Cmd(data: String)
case class Evt(data: String)

case class ExampleState(events: List[String] = Nil) {
  def updated(evt: Evt): ExampleState = copy(evt.data :: events)
  def size: Int = events.length
  override def toString: String = events.reverse.toString
}

class ExamplePersistentActor extends PersistentActor {
  override def persistenceId = "sample-id-1"

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
    case "id" => sender() ! persistenceId
  }
}

import akka.pattern.ask

val epa = system.actorOf(Props(new ExamplePersistentActor()))

epa ! Cmd("foo")
epa ! "print"

import scala.concurrent.duration._

val future = epa.ask("id") (5 seconds)

println(future.value)

import akka.persistence.query.PersistenceQuery
import akka.persistence.query.journal.leveldb.scaladsl.LeveldbReadJournal

val queries = PersistenceQuery(system).readJournalFor[LeveldbReadJournal](
  LeveldbReadJournal.Identifier)

val src: Source[EventEnvelope, NotUsed] =
  queries.eventsByPersistenceId("sample-id-1", 0L, Long.MaxValue)

val events: Source[Any, NotUsed] = src.map(_.event)