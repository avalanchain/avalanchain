package com.avalanchain.jwt

import java.security.{KeyPair, PrivateKey, PublicKey}

/**
  * Created by Yuriy on 22/11/2016.
  */
package object KeysDto {
  case class PubKey(X: String, Y: String)
  case class PrivKey(S: String)
  case class Keys(priv: PrivKey, pub: PubKey)

  implicit def toPubKeyDto(key: PublicKey) = {
    val pkstr = new String(key.toString.toCharArray.map(_.toByte).filter(b => b != 10 && b != 13).map(_.toChar))
    val pattern = """.*X: ([0-9a-f]+) +Y: ([0-9a-f]+).*""".r
    val pattern(x, y) = pkstr
    PubKey(x, y)
  }

  def toPrivKeyDto(key: PrivateKey) = {
    val s = key.toString.substring(31).trim
    PrivKey(s)
  }

  def toKeysDto(keys: KeyPair) = {
    Keys(toPrivKeyDto(keys.getPrivate), toPubKeyDto(keys.getPublic))
  }
}
