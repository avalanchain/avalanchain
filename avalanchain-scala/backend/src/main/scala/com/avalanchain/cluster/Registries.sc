import akka.actor.{ActorSelection, ActorSystem}
import akka.pattern.ask
import akka.stream.ActorMaterializer
import akka.util.Timeout
import com.avalanchain.cluster.ReplicatedCache.{GetFromCache, PutInCache}
import com.avalanchain.cluster.{ClusterService, ReplicatedCache}

import scala.concurrent.Await
import scala.concurrent.duration.DurationInt

implicit val timeout = Timeout(5 seconds)

def startNode(port: Int): ActorSystem = {
  Thread.sleep(1000)
  val system = ClusterService.deployNode(port)
  ClusterService.startOn(ReplicatedCache.props, "cache") (system)
  system
}

val systems = Array(2552, 2553, 2554).map(startNode)


val cache0 = systems(0).actorSelection("user/cache")
cache0 ! PutInCache("test", "test value")

val res  = Await.result((cache0 ? GetFromCache("test")), timeout.duration)//.asInstanceOf[String]

val cache1 = systems(1).actorSelection("user/cache")
val res1  = Await.result((cache1 ? GetFromCache("test")), timeout.duration)//.asInstanceOf[String]

val cache2 = systems(2).actorSelection("user/cache")
val res2  = Await.result((cache2 ? GetFromCache("test")), timeout.duration)//.asInstanceOf[String]
cache2 ! PutInCache("test", "test value 2")


//implicit val materializer = ActorMaterializer()

