package com.avalanchain.toolbox

import akka.util.ByteString
import cats.data.Xor
import com.avalanchain.core.domain.{Bytes2Hexed, Hash, Hexed2Bytes}
import io.circe._
import io.circe.syntax._

/**
  * Created by Yuriy Habarov on 22/10/2016.
  */
object CirceEncoders {
  implicit def encodeByteString(implicit bytes2Hexed: Bytes2Hexed) = new Encoder[ByteString] {
    final def apply(a: ByteString): Json = (bytes2Hexed(a)).asJson
  }

  implicit def decodeByteString(implicit hexed2Bytes: Hexed2Bytes): Decoder[ByteString] = Decoder.decodeString.emap { str =>
    Xor.catchNonFatal(hexed2Bytes(str)).leftMap(t => "Error decoding ByteString")
  }

  implicit def encodeHash(implicit bytes2Hexed: Bytes2Hexed): Encoder[Hash] = new Encoder[Hash] {
    final def apply(h: Hash): Json = (bytes2Hexed(h.hash)).asJson
  }

  implicit def decodeHash(implicit hexed2Bytes: Hexed2Bytes): Decoder[Hash] = decodeByteString(hexed2Bytes).emap { str =>
    Xor.catchNonFatal(Hash(str)).leftMap(t => "Error decoding Hash")
  }
}
