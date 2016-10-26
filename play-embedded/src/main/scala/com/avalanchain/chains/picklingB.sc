import akka.NotUsed
import akka.actor.{Actor, ActorSystem}
import akka.stream.ActorMaterializer
import akka.stream.scaladsl.{Flow, Sink, Source}

import scala.pickling._
import scala.pickling.Defaults._
//import binary._
import scala.spores._
import SporePicklers._
import com.avalanchain.chains.picklingB._

val b = Foo.bar("asdf").value
val unb = Foo.unbar[String](b)

val c = Foo.bar(42).value
val unc = Foo.unbar[Int](c)

pupSpore()