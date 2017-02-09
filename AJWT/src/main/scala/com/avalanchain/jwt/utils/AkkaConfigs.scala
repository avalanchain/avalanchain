package com.avalanchain.jwt.utils

/**
  * Created by Yuriy on 09/05/2016.
  */
object AkkaConfigs {
  val PersLevelDb =
    """
      |akka {
      |  version = 2.5-M1
      |
      |  # Log level used by the configured loggers (see "loggers") as soon
      |  # as they have been started; before that, see "stdout-loglevel"
      |  # Options: OFF, ERROR, WARNING, INFO, DEBUG
      |  loglevel = "DEBUG"
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

  val PersInmem =
    """
      |akka {
      |  version = 2.5-M1
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
      |  actor {
      |    provider = "cluster"
      |
      |    default-dispatcher {
      |      # Throughput for default Dispatcher, set to 1 for as fair as possible
      |      throughput = 10
      |    }
      |  }
      |
      |  persistence.journal.plugin = "akka.persistence.journal.inmem"
      |  persistence.snapshot-store.plugin = "akka.persistence.snapshot-store.local"
      |
      |}
    """.stripMargin

  val PersInmem2 =
    """
      |akka {
      |  version = 2.5-M1
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
      |  persistence.journal.plugin = "inmemory-journal"
      |  persistence.snapshot-store.plugin = "inmemory-snapshot-store"
      |
      |}
    """.stripMargin
}
