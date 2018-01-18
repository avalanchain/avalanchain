import java.util.UUID

import com.avalanchain.jwt.basicChain.{ChainDef, JwtAlgo, ResourceGroup, TypedJwtToken}
import com.avalanchain.jwt.jwt.CurveContext
import com.avalanchain.jwt.jwt.actors.ActorNode
import com.avalanchain.jwt.jwt.actors.ChainNode.NewChain
import com.avalanchain.jwt.jwt.actors.ChainRegistryActor.{ChainCreationResult, CreateChain}
import io.circe.{Decoder, DecodingFailure, Encoder, Json}
import io.circe.syntax._
import io.circe.parser._
import io.circe.generic.JsonCodec
import io.circe.generic.auto._



val keyPair = CurveContext.currentKeys

def newChain(jwtAlgo: JwtAlgo = JwtAlgo.HS512, initValue: Option[Json] = Some(Json.fromString("{}"))) = {
  val chainDef: ChainDef = ChainDef.New(jwtAlgo, UUID.randomUUID().toString, keyPair.getPublic, ResourceGroup.ALL, initValue.map(_.asString.getOrElse("{}")))
  val chainDefToken = TypedJwtToken[ChainDef](chainDef, keyPair.getPrivate)
  chainDefToken
}

val chainDefToken = newChain()

object ActorNode extends ActorNode {
  override val port: Int = 2222
}


val nc = new com.avalanchain.jwt.jwt.actors.network.NewChain("ANC", chainDefToken, keyPair)
  (ActorNode.system, ActorNode.materializer)