import java.nio.charset.Charset
import java.util.Calendar

import akka.{Done, NotUsed}
import akka.actor.ActorSystem
import akka.stream.{ActorMaterializer, OverflowStrategy, QueueOfferResult, ThrottleMode}
import akka.stream.scaladsl._
import akka.http.scaladsl.Http
import akka.http.scaladsl.model._
import akka.http.scaladsl.model.ws._
import akka.stream.QueueOfferResult.Enqueued
import akka.util.ByteString
import yahoofinance.YahooFinance
import yahoofinance.histquotes.Interval
import yahoofinance.quotes.fx.FxQuote

import collection.JavaConversions._
import scala.concurrent.{Await, Future, Promise}
import scala.concurrent.duration.DurationInt
import scala.util.{Failure, Success}

implicit val system = ActorSystem()
implicit val materializer = ActorMaterializer()
import system.dispatcher


//val stock = YahooFinance.getFx("EURUSD=X")
//val price = stock.getPrice()

val from = Calendar.getInstance();
val to = Calendar.getInstance();
from.add(Calendar.YEAR, -5); // from 5 years ago

val google = YahooFinance.get("GOOG", from, to, Interval.WEEKLY);

val symbols = Array("EURUSD","USDEUR", "USDJPY", "USDGBP", "USDAUD", "USDCHF", "USDSEK", "USDNOK", "USDRUB", "USDTRY", "USDBRL", "USDCAD", "USDCNY", "USDHKD", "USDINR", "USDKRW", "USDMXN", "USDNZD", "USDSGD", "USDZAR")
def stocks(symbols: Array[String]) = YahooFinance.getFx(symbols.map(_ + "=X")).map(kv => (kv._1.replace("=X", ""), kv._2))

//val source = Source.repeat()

val source: Source[Int, NotUsed] = Source(1 to 5)
def printSink[T] = Sink.foreach[T](println(_))

val fxUpdates = source.
  map(i => (i, stocks(symbols))).
  throttle(1, 1.second, 1, ThrottleMode.shaping)

val printFlow = fxUpdates.toMat(printSink)(Keep.right).run()

val pool = Http().cachedHostConnectionPool[Promise[HttpResponse]](host = "google.com", port = 80)
val queue = Source.queue[(HttpRequest, Promise[HttpResponse])](10, OverflowStrategy.dropNew).
  via(pool).
  toMat(Sink.foreach({
    case ((Success(resp), p)) => p.success(resp)
    case ((Failure(e), p)) => p.failure(e)
  }))(Keep.left).
  run


val promise = Promise[HttpResponse]
val request = HttpRequest(uri = "/") -> promise

val response = queue.offer(request).flatMap(buffered => {
  if (buffered == Enqueued) promise.future
  else Future.failed(new RuntimeException())
})

val res = Await.ready(response, 3 seconds)
res onSuccess {
  case response =>
    val result = response.
      entity.
      dataBytes.
      reduce(_++_).
      map(_.decodeString(Charset.defaultCharset().toString)).
      map(s => { println(s"Text: {$s}"); s }).
      runWith(Sink.ignore)
//      runWith(Sink.onComplete[String]({
//        case Success(data) =>
//        case Failure(e) => throw new RuntimeException(e)
//    }))
    println(s"Result: ${result}")
}

//// Future[Done] is the materialized value of Sink.foreach,
//// emitted when the stream completes
//val incoming2: Sink[Message, Future[Done]] =
//  Sink.foreach[Message] {
//    case message: TextMessage.Strict =>
//      println(message.text)
//  }
//
//val incoming: Sink[HttpResponse, Future[Done]] =
//  Sink.onComplete[HttpResponse] {
//    Done
//    case Success(message) =>
//      message match {
//        case HttpResponse(StatusCodes.OK, headers, entity, _) =>
//          //val str = message.entity.dataBytes.runFold(ByteString(""))(_ ++ _)
//          //println(s"Received: {${str.value.size}}")
//          //message.entity.withoutSizeLimit().dataBytes.runForeach(v => s"Received: {$v}")
//          val bytes = entity.dataBytes.reduce(_ ++ _)
//          bytes.runWith(Sink.onComplete(
//            case Success(s) => println()
//          ))
//        case _ => println(s"Mismatch $message")
//    }
//  }
//
//val responseFuture: Future[HttpResponse] =
//  Http().singleRequest(HttpRequest(uri = "http://akka.io"))
//
//val source: Source[HttpResponse, NotUsed] = Source.fromFuture(responseFuture)
//source.runWith(incoming)
