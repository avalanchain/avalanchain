import java.io.PrintWriter
import java.net.URL

import org.bouncycastle.jce.spec.{ECPrivateKeySpec, ECPublicKeySpec}
import java.security.{KeyFactory, KeyPairGenerator, SecureRandom, Security}

import org.bouncycastle.crypto.ec.CustomNamedCurves
import org.bouncycastle.jce.provider.BouncyCastleProvider
import org.bouncycastle.jce.spec.ECParameterSpec
import org.bouncycastle.math.ec.custom.djb.Curve25519
import pdi.jwt.{Jwt, JwtAlgorithm}
import java.time.Instant

import com.avalanchain.jwt.jwt.{CurveContext, UserInfo}
import pdi.jwt.{JwtAlgorithm, JwtCirce, JwtClaim}
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

//val (privateKeyEC, publicKeyEC) = newKeys()
val (privateKeyEC, publicKeyEC) = savedKeys()
//val pkstr = publicKeyEC.asJson

val user1 = UserInfo("John", "Smith", "john.smith@a.uk", "07711223344")

val content = user1.asJson.spaces2

val token1 = Jwt.encode(content, privateKeyEC, JwtAlgorithm.ES512)

Jwt.decode(token1, publicKeyEC, Seq(JwtAlgorithm.ES512))

val (content2, token2) = encodeUser(privateKeyEC, user1)

val user2 = decodeUser(publicKeyEC)(token2).get
val userJson = Jwt.decodeRawAll(token2, publicKeyEC, Seq(JwtAlgorithm.ES512)).get
val jsonHeader = userJson._1
val jsonBody = userJson._2
val signature = userJson._3

import java.nio.file.{Paths, Files}
val rawDataFile = Paths.get(sys.env("HOME") + "/Documents/users.csv")
if (!Files.exists(rawDataFile)) {
  Files.copy(getClass.getResourceAsStream("/public/users.csv"), rawDataFile)
}

val rawData: java.net.URL = rawDataFile.toUri.toURL

implicit val userDecoder: RowDecoder[UserInfo] = RowDecoder.decoder(0, 1, 2, 3)(UserInfo.apply)
val reader = rawData.asCsvReader[UserInfo](',', true)
val userInfos = reader.map(_.get)

rawDataFile.toAbsolutePath().toString()

def encoder(u: UserInfo) = encodeUser(privateKeyEC, u)
val contentsAndJwts = userInfos.map(encoder).toList

val contents = contentsAndJwts.map(_._1)
val jwts = contentsAndJwts.map(_._2)

val allContents = contents.foldRight("")((agg, c) => s"$c\n$agg")
val allJwts = jwts.foldRight("")((agg, c) => s"$c\n$agg")

//new PrintWriter(sys.env("HOME") + "/Documents/contents.txt") { write(allContents); close }
//new PrintWriter(sys.env("HOME") + "/Documents/jwts.txt") { write(allJwts); close }

