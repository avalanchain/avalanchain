import akka.actor.ActorSystem
import com.rbmhtechnology.eventuate.{ReplicationConnection, ReplicationEndpoint}
import com.rbmhtechnology.eventuate.crdt.MVRegisterService
import com.rbmhtechnology.eventuate.log.leveldb.LeveldbEventLog
import com.typesafe.config.ConfigFactory

import scala.util.{Failure, Success}

//object MVRegisterExample extends App {
//class MVRegisterExample() {
  def config(port: Int) =
    s"""
       |akka {
       |  actor.provider = "akka.remote.RemoteActorRefProvider"
       |  remote {
       |    enabled-transports = ["akka.remote.netty.tcp"]
       |    netty.tcp {
       |      hostname = "127.0.0.1"
       |      port = $port
       |    }
       |  }
       |  loglevel = "ERROR"
       |}
       |eventuate {
       |  log.replication.retry-delay = 2s
       |  log.replication.remote-read-timeout = 2s
       |  log.leveldb.dir = target/example/logs
       |  snapshot.filesystem.dir = target/example/snapshots
       |}
    """.stripMargin

  case class SignedMessage(message: String, counter: Int)

  def service(locationId: String, port: Int, connectTo: Set[(String, Int)]): MVRegisterService[SignedMessage] = {
    implicit val system: ActorSystem =
      ActorSystem(ReplicationConnection.DefaultRemoteSystemName, ConfigFactory.parseString(config(port)))

    val logName = "L"

    val endpoint = new ReplicationEndpoint(id = locationId, logNames = Set(logName, "asa"),
      logFactory = logId => LeveldbEventLog.props(logId),
      connections = connectTo.map(ep => ReplicationConnection(ep._1, ep._2)))

    endpoint.activate()

    new MVRegisterService[SignedMessage](s"service-$locationId", endpoint.logs(logName))
  }

  val serviceA = service("A", 2552, Set(("127.0.0.1", 2553), ("127.0.0.1", 2554))) // at location A
  val serviceB = service("B", 2553, Set(("127.0.0.1", 2552), ("127.0.0.1", 2554))) // at location B
  val serviceC = service("C", 2554, Set(("127.0.0.1", 2552), ("127.0.0.1", 2553))) // at location C

  val crdtId = "chains"

  import serviceA.system.dispatcher

  serviceA.assign(crdtId, SignedMessage("abc", 1)).onComplete {
    case Success(r) => println(s"assign result to replica at location A: $r")
    case Failure(t) => println("An error has occured: " + t.getMessage)
  }

  serviceB.assign(crdtId, SignedMessage("xyz", 1)).onComplete {
    case Success(r) => println(s"assign result to replica at location B: $r")
    case Failure(t) => println("An error has occured: " + t.getMessage)
  }

  // wait a bit ...
  Thread.sleep(1000)

  serviceC.value(crdtId).onComplete {
    case Success(r) => println(s"read result from replica at location C: $r")
    case Failure(t) => println("An error has occured: " + t.getMessage)
  }
//}
//
//val example = new MVRegisterExample()