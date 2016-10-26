package scala.spores.run.pickling

import akka.NotUsed
import akka.stream.scaladsl.{Flow, Source}

import scala.pickling.Defaults._
import scala.pickling._
import scala.pickling.json._
import scala.spores._
import scala.spores.SporePicklers._


/**
  * Created by Yuriy on 26/10/2016.
  */
object JApp extends App {
  implicit val staticOnly = static.StaticOnly

  def pickleUnpickle(): Unit = {
    val v1 = 10
    val s = spore {
      val c1 = v1
      (x: Int) => s"arg: $x, c1: $c1"
    }

    val res = s.pickle
    println(res.value)

    val up = res.value.unpickle[Spore[Int, String]]
    val res2 = up(5)
    println(s"result: $res2")
  }

  pickleUnpickle()
}

object JApp1 extends App {
  implicit val staticOnly = static.StaticOnly

  def pickleUnpickle(f: Spore[Int, String]): Unit = {
    val v1 = 10
    val s = f

    val res = s.pickle
    println(res.value)

    val up = res.value.unpickle[Spore[Int, String]]
    val res2 = up(5)
    println(s"result: $res2")
  }

  pickleUnpickle((x: Int) => s"arg: $x")
}

object JApp2 extends App {
  implicit val staticOnly = static.StaticOnly

  def pickleUnpickle[T: Pickler: FastTypeTag, R: Pickler: FastTypeTag](f: Spore[T, R]): Function1[T, R] = {
    val v1 = 10
    val s = f

    val res = s.pickle
    println(res.value)

    val up = res.value.unpickle[Spore[T, R]]
    up
  }

  val up = pickleUnpickle((x: Int) => s"arg: $x")
  val res2 = up(5)
  println(s"result: $res2")

}

//object JApp3 extends App {
//  implicit val staticOnly = static.StaticOnly
//
//  def pickleUnpickle[T: Pickler: FastTypeTag, R: Pickler: FastTypeTag](f: Spore[T, R]): Function1[T, R] = {
//    val v1 = 10
//    val s = f
//
//    val res = s.pickle
//    println(res.value)
//
//    val up = res.value.unpickle[Spore[T, R]]
//    up
//  }
//
//  val flow = Flow.fromFunction((x: Int) => s"arg: $x")
//
//  val up = pickleUnpickle((x: Int) => Source.single(x).via(flow))
//  val res2 = up(5)
//  println(s"result: $res2")
//
//}
