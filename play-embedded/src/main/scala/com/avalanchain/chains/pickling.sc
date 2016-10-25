import akka.NotUsed
import akka.actor.{Actor, ActorSystem}
import akka.stream.ActorMaterializer
import akka.stream.scaladsl.{Flow, Sink, Source}

import scala.pickling._
import scala.pickling.Defaults._
import scala.pickling.json._

import scala.spores._
import SporePicklers._

case class Person(name: String, age: Int)

val pkl = Person("foo", 20).pickle

val person = pkl.unpickle[Person]


//import scala.pickling._
//import binary._
//
//object Foo {
//  def bar[T: Pickler: FastTypeTag](t: T) = t.pickle
//  def unbar[T: Unpickler: FastTypeTag](bytes: Array[Byte]) = bytes.unpickle[T]
//}

//Foo.bar(42)

import scala.pickling.json._

object Foo {
  def bar[T: Pickler: FastTypeTag](t: T) = t.pickle
  def unbar[T: Unpickler: FastTypeTag](json: JSONPickle) = json.unpickle[T]
}


val barPickled = Foo.bar("42").value
val barUnPickled = barPickled.unpickle[String]

///////////////////////////

val outer1 = 0
val outer2 = Person("Jim", 35)
val s = spore {
  val inner = outer2
  (x: Int) => {
    s"The result is: ${x + inner.age + outer1}"
  }
}

def pupJsonSpore1(): Unit = {
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

pupJsonSpore1()

val sPickled = s.pickle.value
val sUnPickled = sPickled.unpickle[Spore[Int,String]]

///////////////////////////

val logicLogic = Flow[String].map(_ + 1)

implicit val system = ActorSystem()
implicit val materializer = ActorMaterializer()

//Source(1 to 10).map(_.toString).via(logicLogic).runForeach(println)

import scala.collection.immutable.Map._

//val flowPickled = logicLogic.pickle.value
//val flowUnpickled = flowPickled.unpickle[Flow[String, String, NotUsed]]

Source(1 to 10).map(_.toString).via(logicLogic).runForeach(println)
