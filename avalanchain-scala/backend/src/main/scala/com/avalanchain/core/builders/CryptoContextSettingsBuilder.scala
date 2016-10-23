package com.avalanchain.core.builders

import java.nio.charset.StandardCharsets

import com.avalanchain.core.domain.{ByteWord, _}
import scorex.crypto.encode.{Base16, Base58, Base64}
import com.avalanchain.toolbox.Pipe._
import scorex.crypto.hash.{CryptographicHash, Sha512}

/**
  * Created by Yuriy Habarov on 23/10/2016.
  */
object CryptoContextSettingsBuilder {
  //  Supported hash algorithms are:
  //
  //  Blake
  //  Blake2b
  //  BMW
  //  CubeHash
  //  Echo
  //  Fugue
  //  Groestl
  //  Hamsi
  //  JH
  //  Keccak
  //  Luffa
  //  Sha
  //  SHAvite
  //  SIMD
  //  Skein
  //  Whirlpool

  object Hexing {
    object Base58Hexing {
      def bytes2Hexed: Bytes2Hexed = _ |> (_.toArray) |> Base58.encode
      def hexed2Bytes: Hexed2Bytes = _ |> (Base58.decode(_).getOrElse(Array[Byte]())) |> (ByteWord(_))
    }
    object Base64Hexing {
      def bytes2Hexed: Bytes2Hexed = _ |> (_.toArray) |> Base64.encode
      def hexed2Bytes: Hexed2Bytes = _ |> (Base64.decode(_)) |> (ByteWord(_))
    }
    object Base16Hexing {
      def bytes2Hexed: Bytes2Hexed = _ |> (_.toArray) |> Base16.encode
      def hexed2Bytes: Hexed2Bytes = _ |> (Base16.decode(_)) |> (ByteWord(_))
    }
  }

  def scorexHasher(hasher: CryptographicHash): Hasher = (value: ByteWord) => {
    val hash = hasher(value.toArray) |> (ByteWord(_)) |> (Hash(_))
    HashedValue(hash, value)
  }

  implicit object CryptoContextSettings extends CryptoContextSettings {
    implicit def hasher: Hasher = scorexHasher(Sha512)

    implicit def hexed2Bytes: Hexed2Bytes = Hexing.Base58Hexing.hexed2Bytes
    implicit def bytes2Hexed: Bytes2Hexed = Hexing.Base58Hexing.bytes2Hexed

    //      override def serializer[T]: Serializer[T] = ??? //PicklingSerializer.serializer[T] // SprayJsonSerializer.serializer[T]
    //      override def deserializer[T]: ((TextSerialized) => T, (BytesSerialized) => T) = ??? //PicklingSerializer.deserializer[T] // SprayJsonSerializer.deserializer[T]

  }
}
