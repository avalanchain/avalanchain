package com.avalanchain.yahoo

/**
  * Created by Yuriy Habarov on 26/04/2016.
  */
import java.time.LocalDate

import akka.actor.ActorSystem
import akka.event.Logging
import akka.http.scaladsl.Http
import akka.http.scaladsl.client.RequestBuilding
import akka.http.scaladsl.model.StatusCodes._
import akka.http.scaladsl.model.{StatusCode, Uri}
import akka.stream.Materializer
import akka.stream.scaladsl.Source
import akka.util.ByteString
import com.typesafe.config.ConfigFactory
import yahoofinance.YahooFinance
import collection.JavaConversions._

import scala.concurrent.{ExecutionContext, ExecutionContextExecutor, Future}

trait StockPriceClient {
  type Response[T] = Future[Either[(StatusCode, String), T]]

  def stocks(symbols: Array[String]): Map[String, BigDecimal]
//  def history(symbol: String, begin: LocalDate, end: LocalDate)(implicit ec: ExecutionContext): Response[Source[csv.Row, Any]] =
//    rawHistory(symbol, begin, end).map(_.right.map(_.via(csv.parseCsv())))
//
//  def rawHistory(symbol: String, begin: LocalDate, end: LocalDate): Response[Source[ByteString, Any]]
}

/**
  * Retrieves historical service.stock prices from Yahoo Finance.
  */
case class YahooStockPriceClient() extends StockPriceClient
{

  private val defaultStocks = Array("EURUSD","USDEUR", "USDJPY", "USDGBP", "USDAUD", "USDCHF", "USDSEK", "USDNOK",
    "USDRUB", "USDTRY", "USDBRL", "USDCAD", "USDCNY", "USDHKD", "USDINR", "USDKRW", "USDMXN", "USDNZD", "USDSGD", "USDZAR")

  def stocks(symbols: Array[String] = defaultStocks) =
    YahooFinance.getFx(symbols.map(_ + "=X")).map(kv => (new String(kv._1.replace("=X", "")), new BigDecimal(kv._2.getPrice()))).toMap

}

