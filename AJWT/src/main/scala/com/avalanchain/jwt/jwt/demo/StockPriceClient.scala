package com.avalanchain.jwt.jwt.demo

/**
  * Created by Yuriy Habarov on 26/04/2016.
  */
import java.time.LocalDate

import akka.NotUsed
import akka.actor.ActorSystem
import akka.event.Logging
import akka.http.scaladsl.Http
import akka.http.scaladsl.client.RequestBuilding
import akka.http.scaladsl.model.StatusCodes._
import akka.http.scaladsl.model.{StatusCode, Uri}
import akka.stream.{Materializer, SourceShape, ThrottleMode}
import akka.stream.scaladsl.{GraphDSL, Source}
import akka.util.ByteString
import com.avalanchain.jwt.basicChain.{JwtPayload, TypedJwtToken}
import com.typesafe.config.ConfigFactory
import org.joda.time.DateTime
import yahoofinance.YahooFinance

import collection.JavaConversions._
import scala.concurrent.{ExecutionContext, ExecutionContextExecutor, Future}
import scala.concurrent.duration.{DurationInt, FiniteDuration}
import scala.concurrent.{ExecutionContextExecutor, Future}

case class StockTick(symbol: String, price: BigDecimal, dt: DateTime) extends JwtPayload.Sym

trait StockPriceClient {
  type Response[T] = Future[Either[(StatusCode, String), T]]

  def stocks(symbols: Array[String]): List[StockTick]
//  def history(symbol: String, begin: LocalDate, end: LocalDate)(implicit ec: ExecutionContext): Response[Source[csv.Row, Any]] =
//    rawHistory(symbol, begin, end).map(_.right.map(_.via(csv.parseCsv())))
//
//  def rawHistory(symbol: String, begin: LocalDate, end: LocalDate): Response[Source[ByteString, Any]]
}

/**
  * Retrieves historical service.stock prices from Yahoo Finance.
  */
class YahooStockPriceClient() extends StockPriceClient
{
  private val defaultStocks = Array("EURUSD","USDEUR", "USDJPY", "USDGBP", "USDAUD", "USDCHF", "USDSEK", "USDNOK",
    "USDRUB", "USDTRY", "USDBRL", "USDCAD", "USDCNY", "USDHKD", "USDINR", "USDKRW", "USDMXN", "USDNZD", "USDSGD", "USDZAR")

  def stocks(symbols: Array[String] = defaultStocks) =
    YahooFinance
      .getFx(symbols.map(_ + "=X"))
      .map(kv => StockTick(kv._2.getSymbol, new BigDecimal(kv._2.getPrice), DateTime.now()))
      .toList
}

object YahooFinSource {
  type StockTickToken = TypedJwtToken[StockTick]

  def apply(maxRequests: Int = Int.MaxValue, duration: FiniteDuration = 1 second) : Source[StockTick, NotUsed] = {
    val client = new YahooStockPriceClient()
    Source(1 to maxRequests)
      .throttle(1, duration, 1, ThrottleMode.shaping)
      .mapConcat(i => client.stocks())
      //.groupBy(25, e => e.symbol)
  }
}