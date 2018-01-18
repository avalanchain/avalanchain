import akka.actor.ActorSystem
import akka.stream.scaladsl.Source
import com.avalanchain.jwt.api.MainCmd.ActorNode
import com.avalanchain.jwt.jwt.actors.ActorNode
import com.rbmhtechnology.eventuate.adapter.stream.{DurableEventSource, DurableEventWriter}
import com.rbmhtechnology.eventuate.{DurableEvent, ReplicationConnection, ReplicationEndpoint}
import com.rbmhtechnology.eventuate.log.leveldb.LeveldbEventLog

case class Node(id: String, port: Int, connectPort: Int) extends ActorNode {
  //val log = system.actorOf(LeveldbEventLog.props(logId = "L1", prefix = id + "-log"))

  val endpoint = new ReplicationEndpoint(id = id, logNames = Set("L", "M"),
    logFactory = logId => LeveldbEventLog.props(logId),
    connections = Set(ReplicationConnection("127.0.0.1", connectPort)))
    //applicationName = ActorNode.SystemName)

  endpoint.activate()
}

val node1 = Node("1", 8811, 8812)
val node2 = Node("2", 8812, 8811)
val node3 = Node("3", 8813, 8811)


val logsNames = node2.endpoint.logNames
val logs = node1.endpoint.logs

implicit val system = node1.system
implicit val materializer = node1.materializer

val source1 = Source.fromGraph(DurableEventSource(logs("M"))).runForeach(println)

Source(List("a", "b", "c")).map(DurableEvent(_)).via(DurableEventWriter("writerId1", logs("M"))).map(event => (event.payload, event.localSequenceNr)).runForeach(println)

val source2 = Source.fromGraph(DurableEventSource(node2.endpoint.logs("M"))).runForeach(println)
