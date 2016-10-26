package scala.spores.run.pickling

import scala.pickling.Defaults._
import scala.pickling._
import scala.pickling.binary._
import scala.spores._
import scala.spores.SporePicklers._

/**
  * Created by Yuriy on 26/10/2016.
  */
object BApp extends App {
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

object BApp1 extends App {
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

object BApp2 extends App {
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
