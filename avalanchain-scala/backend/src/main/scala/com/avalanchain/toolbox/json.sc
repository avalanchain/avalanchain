import io.circe._, io.circe.generic.auto._, io.circe.parser._, io.circe.syntax._
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

trait CirceEncoders {
  implicit object ClientErrorEncoder extends Encoder[Foo] {
    override def apply(a: Foo): Json = a.asJson
  }
}



def f[T <: Foo](v: T): String = {
  (v.asJson.noSpaces)
}

val a = f(Qux(13, Some(14.0)))
println(a)
