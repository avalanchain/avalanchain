package com.avalanchain.jwt.jwt.chat

import java.security.PrivateKey
import java.time.OffsetDateTime

import com.avalanchain.jwt.KeysDto.PubKey
import com.avalanchain.jwt.basicChain.{Id, JwtPayload, NodeIdToken, TypedJwtToken}
import com.avalanchain.jwt.utils.CirceCodecs

import io.circe.{Decoder, DecodingFailure, Encoder, Json}
import io.circe.syntax._
import io.circe.parser._
import io.circe.generic.JsonCodec
import io.circe.generic.auto._

/**
  * Created by Yuriy Habarov on 28/11/2016.
  */
final case class ChatMsg(channelId: Id, senderId: Id, nodeName: String, message: String, dt: OffsetDateTime, pub: PubKey) extends JwtPayload.Asym
object ChatMsg extends CirceCodecs {
  val ALL = "_ALL_"
  val SYSBOT = "_SYSBOT_"
  type ChatMsgToken = TypedJwtToken[ChatMsg]
  object ChatMsgToken {
    def apply(channelId: Id, senderId: Id, nodeId: NodeIdToken, message: String, pub: PubKey, privateKey: PrivateKey): ChatMsgToken =
      TypedJwtToken[ChatMsg](ChatMsg(channelId, senderId, nodeId.payload.get.name, message, OffsetDateTime.now(), pub), privateKey)
  }

}
