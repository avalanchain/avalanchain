import pdi.jwt._
import java.time.Instant

import com.avalanchain.jwt.jwt.{CurveContext, UserInfo}
import io.circe._
import io.circe.generic.auto._
import io.circe.parser._
import io.circe.syntax._
import kantan.csv.RowDecoder
import kantan.csv.ops._

import scala.collection.immutable._
import scala.util.Try
import collection.JavaConverters._
import CurveContext._

import scalaj.http.Http

//val (privateKeyEC, publicKeyEC) = newKeys()
val (privateKeyEC, publicKeyEC) = savedKeys()
//val pkstr = publicKeyEC.asJson







val user1 = UserInfo("John", "Smith", "john.smith@e.co.uk", "07711223344")

case class T1(userInfo: UserInfo, str: String)
val t1 = T1(user1, "asdasdasdas,mb,mb,mb,mb,mb,mbdasdasd")

val content = t1.asJson.spaces2

val token1 = Jwt.encode(content, privateKeyEC, JwtAlgorithm.ES512)
//JwtOptions.DEFAULT.

Jwt.decode(token1, publicKeyEC, Seq(JwtAlgorithm.ES512))

val (content2, token2) = encodeUser(privateKeyEC, user1)

//val user2 = decodeUser(publicKeyEC)(token1).get
val userJson = Jwt.decodeRawAll(token1, publicKeyEC, Seq(JwtAlgorithm.ES512)).get
val jsonHeader = userJson._1
val jsonBody = userJson._2
val signature = userJson._3

signature.length
