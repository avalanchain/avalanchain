package com.avalanchain.jwt.jwt.demo

import java.security.PrivateKey
import java.time.OffsetDateTime

import com.avalanchain.jwt.KeysDto.PubKey
import com.avalanchain.jwt.basicChain.{JwtPayload, NodeIdToken, TypedJwtToken}
import com.avalanchain.jwt.utils.CirceCodecs
import io.circe._
import io.circe.syntax._
import io.circe.parser._
import io.circe.generic.JsonCodec
import io.circe.generic.auto._


/**
  * Created by Yuriy Habarov on 28/11/2016.
  */
object Demo extends CirceCodecs {
  case class ChatMsg(nodeName: String, message: String, dt: OffsetDateTime, pub: PubKey) extends JwtPayload.Asym
  type ChatMsgToken = TypedJwtToken[ChatMsg]
  object ChatMsgToken {
    def apply(nodeId: NodeIdToken, message: String, pub: PubKey, privateKey: PrivateKey): ChatMsgToken =
      TypedJwtToken[ChatMsg](ChatMsg(nodeId.payload.get.name, message, OffsetDateTime.now(), pub), privateKey)
  }

}
