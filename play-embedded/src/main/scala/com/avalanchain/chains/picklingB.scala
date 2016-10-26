package com.avalanchain.chains

import scala.spores.{Spore, _}
import SporePicklers._

/**
  * Created by Yuriy on 26/10/2016.
  */
object picklingB {
  import akka.NotUsed
  import akka.actor.{Actor, ActorSystem}
  import akka.stream.ActorMaterializer
  import akka.stream.scaladsl.{Flow, Sink, Source}

  import scala.pickling._
  import scala.pickling.Defaults._
  import binary._

  case class Person(name: String, age: Int)

  object Foo {
    def bar[T: Pickler: FastTypeTag](t: T) = t.pickle
    def unbar[T: Unpickler: FastTypeTag](bytes: Array[Byte]) = bytes.unpickle[T]
  }

  def pupSpore(): Unit = {
    val v1 = 10
    val s = spore {
      val c1 = v1
      (x: Int) => s"arg: $x, c1: $c1"
    }

    val res = s.pickle
    println("pickled: " + res.value)

    val format  = implicitly[PickleFormat]

    //val up = res.value.unpickle[Spore[Int, String]]
    //val res2 = up(5)
    val reader = format.createReader(res.asInstanceOf[format.PickleType])
    val unpickler = SporePicklers.genSporeUnpickler[Int, String]
    val up = unpickler.unpickle("", reader).asInstanceOf[Spore[Int, String]]
    val res2 = up(5)
    println(res2)
  }
}
