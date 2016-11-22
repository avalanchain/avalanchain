package com.avalanchain.jwt.jwt

import java.security._
import java.time.Instant

import io.circe.generic.auto._
import io.circe.syntax._
import org.bouncycastle.crypto.ec.CustomNamedCurves
import org.bouncycastle.jce.provider.BouncyCastleProvider
import org.bouncycastle.jce.spec.{ECParameterSpec, ECPrivateKeySpec, ECPublicKeySpec}
import org.bouncycastle.math.ec.custom.djb.Curve25519
import pdi.jwt.{JwtAlgorithm, JwtCirce, JwtClaim}

import scala.util.Try

trait CurveContext {
  def newKeys(): KeyPair
  def savedKeys(S: BigInt, X: BigInt, Y: BigInt): KeyPair
  val currentKeys: KeyPair
}

/**
  * Created by Yuriy Habarov on 26/04/2016.
  */
object CurveContext extends CurveContext {
  Security.addProvider(new BouncyCastleProvider())

  private val curve25519 = new Curve25519()

  private val ecP = CustomNamedCurves.getByName("curve25519")

  private val ecSpec = new ECParameterSpec(ecP.getCurve(), ecP.getG(), ecP.getN(), ecP.getH(), ecP.getSeed())

  def newKeys() = {
    val bcProvider = new BouncyCastleProvider();
    val g = KeyPairGenerator.getInstance("ECDSA", bcProvider);
    g.initialize(ecSpec, new SecureRandom());
    val keyPair = g.generateKeyPair();

    keyPair
  }

  def savedKeys(
               // private key
    S: BigInt = BigInt("9e37dad4c2c1aef0e11d61a7ed3ab5d98a8d12e0c8f992c9b629c254f334822", 16),
               // public key
    X: BigInt = BigInt("5824f0cdb85c16a002a9eca35477be0a11623d047dfb7d0ddab99e49e7ca8efe", 16),
    Y: BigInt = BigInt("3b5c55ed06531ffa993d0f855fa9a53d9683dca3dbe870c489bdafc160bd568c", 16)
  ) = {
    val privateSpec = new ECPrivateKeySpec(S.underlying(), ecSpec)
    val publicSpec = new ECPublicKeySpec(curve25519.createPoint(X.underlying(), Y.underlying()), ecSpec)

    new KeyPair(KeyFactory.getInstance("ECDSA", "BC").generatePublic(publicSpec),
      KeyFactory.getInstance("ECDSA", "BC").generatePrivate(privateSpec))
  }

  val currentKeys = savedKeys()

  def encodeUser(privateKey: PrivateKey, userInfo: UserInfo) = {
    val content = userInfo.asJson.spaces2
    val claim = JwtClaim(
      content = content,
      issuer = Some("AAA"),
      expiration = Some(Instant.now.plusSeconds(157784760).getEpochSecond),
      issuedAt = Some(Instant.now.getEpochSecond))

    val token = JwtCirce.encode(claim, privateKey, JwtAlgorithm.ES512)
    (content, token)
  }

  def decodeUser(publicKey: PublicKey)(token: String): Try[UserInfo] = {
    val decoded = JwtCirce.decodeJson(token, publicKey, Seq(JwtAlgorithm.ES512))
    decoded.flatMap(_.as[UserInfo].toTry)
  }
}
