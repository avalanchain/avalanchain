import akka.util.ByteString
import cats.data.Xor
import com.avalanchain.core.domain.{ByteWord, PublicKey}
import io.circe._
import io.circe.generic.auto._
import io.circe.parser._
import io.circe.syntax._
// import io.circe._
// import io.circe.generic.auto._
// import io.circe.parser._
// import io.circe.syntax._

sealed trait Foo
// defined trait Foo

case class Bar(xs: List[String]) extends Foo
// defined class Bar

case class Qux(i: Int, d: Option[Double]) extends Foo
// defined class Qux

val foo: Foo = Qux(13, Some(14.0))
// foo: Foo = Qux(13,Some(14.0))

foo.asJson.noSpaces
// res0: String = {"Qux":{"i":13,"d":14.0}}

decode[Foo](foo.asJson.spaces4)

//val pubKey = PublicKey()

//class CirceEncoders {
//  implicit object ClientErrorEncoder extends Encoder[ByteString] {
//    override def apply(a: ByteString): Json = a.mkString.asJson
//  }
//}

implicit val encodeFoo = new Encoder[ByteString] {
  final def apply(a: ByteString): Json = a.utf8String.asJson // your implementation goes here
}

implicit val decodeInstant: Decoder[ByteString] = Decoder.decodeString.emap { str =>
  Xor.catchNonFatal(ByteString(str)).leftMap(t => "Instant")
}

val bs: ByteWord = ByteString("10")
val json = bs.asJson.toString
decode[ByteString](json)

//val j = parse(json.as[ByteString]).getOrElse(JsonObject.empty)

//def f[T <: Foo](v: T): String = {
//  (v.asJson.noSpaces)
//}
//
//val a = f(Qux(13, Some(14.0)))
//println(a)

//implicit class Pipe[T](val v: T) {
//  def |>[U](f: T => U) = f(v)
//}
//
//List(5, 6, 7) |> (_.map( x => s"${x + 1}:" )) |> println
