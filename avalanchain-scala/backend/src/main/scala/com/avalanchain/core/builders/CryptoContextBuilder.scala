package com.avalanchain.core.builders

import com.avalanchain.core.domain.ChainStream.{BytesSerialized, TextSerialized}
import com.avalanchain.core.domain.{Deserializer, Serializer}
import scorex.crypto.hash.CryptographicHash

import scala.pickling._
import scala.pickling.json._
import scala.pickling.static._  // Avoid runtime pickler

// Import pickle ops
import scala.pickling.Defaults.{ pickleOps, unpickleOps }

// Import picklers for specific types
import scala.pickling.Defaults.{ stringPickler, intPickler, refUnpickler, nullPickler }
import scala.pickling.{Pickler, Unpickler}

/**
  * Created by Yuriy Habarov on 10/05/2016.
  */
object CryptoContextBuilder {
  // sealed trait Serializer
  object PicklingSerializer {
    //implicit val pickler = Pickler.generate[T]
    //implicit val unpickler = Unpickler.generate[T]

    def serializer[T: FastTypeTag : Pickler]: Serializer[T] = (value: T) => {
      val pickled = value.pickle
      val text = pickled.toString
      (text, text.toCharArray.map(_.toByte))
    }

    def deserializer[T: FastTypeTag : Unpickler]: Deserializer[T] = {
      def textDeserializer = (text: TextSerialized) => {
        text.unpickle[T]
      }
      def bytesDeserializer = (bytes: BytesSerialized) => {
        val text = new String(bytes.map(_.toChar))
        textDeserializer(text)
      }
      (textDeserializer, bytesDeserializer)
    }
  }


  //def apply(hasher: CryptographicHash, )
}
