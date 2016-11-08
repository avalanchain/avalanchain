import java.io.{File, PrintWriter}
import java.net.URL

import org.bouncycastle.jce.spec.{ECPrivateKeySpec, ECPublicKeySpec}
import java.security.{KeyFactory, KeyPairGenerator, SecureRandom, Security}

import org.bouncycastle.crypto.ec.CustomNamedCurves
import org.bouncycastle.jce.provider.BouncyCastleProvider
import org.bouncycastle.jce.spec.ECParameterSpec
import org.bouncycastle.math.ec.custom.djb.Curve25519
import pdi.jwt.{Jwt, JwtAlgorithm}
import java.time.Instant

import pdi.jwt.{JwtAlgorithm, JwtCirce, JwtClaim}
import io.circe._
import io.circe.generic.auto._
import io.circe.parser._
import io.circe.syntax._
import kantan.csv.{RowEncoder, _}
import kantan.csv.ops._

import scala.collection.immutable._
import scala.util.Try
import collection.JavaConverters._
import scala.util.matching.Regex
import java.nio.file.{Files, Path, Paths}
import com.avalanchain.jwt.jwt.{CurveContext, UserInfo}
import com.avalanchain.jwt.jwt.CurveContext._

val rawDataFile = Paths.get(sys.env("HOME") + "/Documents/Copy of Blockfest_1stNov_contacts.csv")

val rawData: java.net.URL = rawDataFile.toUri.toURL

def parseName(str: String) = {
  val pattern = """([a-zA-Z- ']*), ([a-zA-Z\- ']*) \(.*\)""".r

  val pattern(ln, fn) = str

  (fn, ln)
}

parseName("Da Gama-Rose, John (UK - London)")

def toUserInfo(name: String, email: String, phone: String) = {
  val (fn, ln) =
    if (!name.isEmpty()) parseName(name)
    else ("", "")
  UserInfo.apply(fn, ln, email, phone.replace(" ", ""))
}

implicit val userDecoder: RowDecoder[UserInfo] =
  RowDecoder.decoder[String, String, String, UserInfo](0, 1, 2)(toUserInfo)
val reader = rawData.asCsvReader[UserInfo](',', true)
val userInfos = reader.map(_.get).filter(!_.firstName.isEmpty).toArray
println(userInfos.length)

val (privateKeyEC, publicKeyEC) = savedKeys()

def encoder(u: UserInfo) = encodeUser(privateKeyEC, u)
val contentsAndJwts = userInfos.map(encoder).toList

val contents = contentsAndJwts.map(_._1)
val jwts = contentsAndJwts.map(_._2)

val allContents = contents.foldRight("")((agg, c) => s"$c\n$agg")
val allJwts = jwts.foldRight("")((agg, c) => s"$c\n$agg")

//new PrintWriter(sys.env("HOME") + "/Documents/contents.txt") { write(allContents); close }
//new PrintWriter(sys.env("HOME") + "/Documents/jwts.txt") { write(allJwts); close }

def saveUserInfos(users: Array[UserInfo], outFile: Path): Unit = {
  implicit val userEncoder: RowEncoder[UserInfo] = RowEncoder.caseEncoder(0, 1, 2, 3)(UserInfo.unapply)
  new File(rawDataFile.toAbsolutePath().toString()).delete()
  val out = new java.io.File(outFile.toAbsolutePath().toString())
  val writer = out.asCsvWriter[UserInfo](',', List("first_name", "last_name", "email", "phone"))
  writer.write(users).close()
}

val path = Paths.get(sys.env("HOME") + "/Documents/out.csv")
//saveUserInfos(userInfos, path)