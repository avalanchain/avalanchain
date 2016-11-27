package com.avalanchain.jwt.utils

import java.time.OffsetDateTime
import java.time.format.DateTimeFormatter
import java.util.UUID

import akka.http.scaladsl.model.StatusCodes.ClientError
import com.avalanchain.jwt.jwt.demo.StockTick
import io.circe.{Encoder, Json}
import io.circe.syntax._

/**
  * Created by Yuriy Habarov on 25/11/2016.
  */
trait CirceEncoders {

  val dateTimeFormat = DateTimeFormatter.ISO_DATE_TIME

  implicit object DateTimeEncoder extends Encoder[OffsetDateTime] {
    override def apply(dt: OffsetDateTime): Json = dateTimeFormat.format(dt).asJson
  }

  implicit object UuidEncoder extends Encoder[UUID] {
    override def apply(u: UUID): Json = u.toString.replace("-", "").asJson
  }

//  implicit object stockTickEncoder extends Encoder[StockTick] {
//    override def apply(st: StockTick): Encoder[StockTick] = Encoder.forProduct3("symbol", "price", "dt")(st => (st.symbol, st.price, st.dt))
//  }
}

trait CirceDecoders {

}