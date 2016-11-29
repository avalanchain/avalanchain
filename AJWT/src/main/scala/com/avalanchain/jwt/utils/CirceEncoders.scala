package com.avalanchain.jwt.utils

import java.time.OffsetDateTime
import java.time.format.DateTimeFormatter
import java.util.UUID

import akka.http.scaladsl.model.StatusCodes.ClientError
import com.avalanchain.jwt.jwt.demo.StockTick
import com.avalanchain.jwt.basicChain.{ChainDef, ChainDefCodecs, FrameToken, FrameTokenCodecs}
import io.circe.{Decoder, Encoder, Json}
import io.circe.syntax._
import io.circe.generic.semiauto._
import cats.implicits._
import com.avalanchain.jwt.jwt.demo.account.{AccountCommandCodecs, AccountEvent, AccountState, AccountStateCodecs}


/**
  * Created by Yuriy Habarov on 25/11/2016.
  */
trait CirceSimpleCodecs {

  val dateTimeFormat = DateTimeFormatter.ISO_DATE_TIME

  implicit object DateTimeEncoder extends Encoder[OffsetDateTime] {
    override def apply(dt: OffsetDateTime): Json = dateTimeFormat.format(dt).asJson
  }

  implicit val decodeInstant: Decoder[OffsetDateTime] = Decoder.decodeString.emap { str =>
    Either.catchNonFatal(OffsetDateTime.parse(str, DateTimeFormatter.ISO_DATE_TIME)).leftMap(t => "OffsetDateTime")
  }

//  implicit object UuidEncoder extends Encoder[UUID] {
//    override def apply(u: UUID): Json = u.toString.replace("-", "").asJson
//  }

}

trait CirceCodecs extends CirceSimpleCodecs with ChainDefCodecs with FrameTokenCodecs with AccountCommandCodecs with AccountStateCodecs {

  import io.circe.generic.semiauto._

  implicit val encoderAccountEvent: Encoder[AccountEvent] = deriveEncoder
  implicit val decoderAccountEvent: Decoder[AccountEvent] = deriveDecoder
}
object CirceCodecs extends CirceCodecs