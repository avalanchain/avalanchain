package com.avalanchain.jwt.jwt.chat

import java.security.{KeyPair, PrivateKey}
import java.time.OffsetDateTime

import com.avalanchain.jwt.KeysDto.PubKey
import com.avalanchain.jwt.basicChain._
import com.avalanchain.jwt.utils.{CirceDecoders, CirceEncoders}
import io.circe.{Decoder, DecodingFailure, Encoder, Json}
import io.circe.syntax._
import io.circe.parser._
import io.circe.generic.JsonCodec
import io.circe.generic.auto._


/**
  * Created by Yuriy on 28/11/2016.
  */
class ChatNode(nodeId: NodeIdToken, keyPair: KeyPair) extends CirceEncoders with CirceDecoders {
  case class ChatMsg(nodeName: String, message: String, dt: OffsetDateTime, pub: PubKey) extends JwtPayload.Asym
  type ChatMsgToken = TypedJwtToken[ChatMsg]
  object ChatMsgToken {
    def apply(message: String, pub: PubKey, privateKey: PrivateKey): ChatMsgToken =
      TypedJwtToken[ChatMsg](ChatMsg(nodeId.payload.get.name, message, OffsetDateTime.now, pub), privateKey)
  }

  
}
