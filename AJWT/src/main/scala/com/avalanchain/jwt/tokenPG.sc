import akka.actor.ActorSystem
import akka.event.{Logging, LoggingAdapter}
import akka.stream.{ActorMaterializer, ThrottleMode}
import akka.stream.scaladsl.{Sink, Source}
import com.avalanchain.jwt.jwt.CurveContext
import com.avalanchain.jwt.jwt.actors.ChainNode
import com.avalanchain.jwt.jwt.demo.DemoNode
import com.avalanchain.jwt.jwt.demo.DemoNode.Tick

import scala.concurrent.ExecutionContext
import io.circe.{Decoder, Encoder}
import io.circe.syntax._
import io.circe.generic.auto._

import scala.concurrent.duration.{DurationInt, FiniteDuration}

implicit val system = ActorSystem()
implicit val executor: ExecutionContext = system.dispatcher
implicit val materializer: ActorMaterializer = ActorMaterializer()

val chainNode: ChainNode = new ChainNode(CurveContext.currentKeys, Set.empty)
val demoNode: DemoNode = new DemoNode(chainNode)

val ref = demoNode.tickerChainRef

val tickerSink = demoNode.tickerSink()

val g = Source(0 to 10).throttle(1, 1 second, 1, ThrottleMode.shaping).map(Tick(_, chainNode.publicKey).asJson).alsoTo(Sink.foreach(println)).to(tickerSink).run()

val g = Source(0 to 10).throttle(1, 1 second, 1, ThrottleMode.shaping).map(Tick(_, chainNode.publicKey).asJson).to(Sink.foreach(println)).run()

val tickerSource = demoNode.tickerSource(0, 100).toOption.get.to(Sink.foreach(println))
