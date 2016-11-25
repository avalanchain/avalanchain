package com.avalanchain.jwt.jwt.demo

/**
  * Created by Yuriy Habarov on 26/04/2016.
  */
import java.time.{LocalDate, OffsetDateTime}
import java.time.format.DateTimeFormatter

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
import com.avalanchain.jwt.utils.CirceEncoders
import com.typesafe.config.ConfigFactory
import org.joda.time.DateTime
import yahoofinance.YahooFinance
import io.circe.{Decoder, DecodingFailure, Encoder, Json}
import io.circe.syntax._
import io.circe.parser._
import io.circe.generic.JsonCodec
import io.circe.generic.auto._

import collection.JavaConversions._
import scala.concurrent.{ExecutionContext, ExecutionContextExecutor, Future}
import scala.concurrent.duration.{DurationInt, FiniteDuration}
import scala.concurrent.{ExecutionContextExecutor, Future}

case class StockTick(symbol: String, bid: BigDecimal, ask: BigDecimal, dt: OffsetDateTime) extends JwtPayload.Sym

trait StockPriceClient {
  type Response[T] = Future[Either[(StatusCode, String), T]]

  def stocks(symbols: Array[String]): List[StockTick]
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
      .map(kv => StockTick(kv._2.getSymbol.replace("=X", ""), new BigDecimal(kv._2.getPrice),
        new BigDecimal(kv._2.getPrice.add(new java.math.BigDecimal("0.01"))), OffsetDateTime.now()))
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