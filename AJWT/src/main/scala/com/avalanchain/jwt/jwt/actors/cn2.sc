import java.security.PublicKey
import java.util.UUID

import com.avalanchain.jwt.basicChain._
import com.avalanchain.jwt.jwt.CurveContext
import com.avalanchain.jwt.jwt.actors.ActorNode
import com.avalanchain.jwt.jwt.actors.ChainNode.NewChain
import com.avalanchain.jwt.jwt.actors._
import com.avalanchain.jwt.jwt.actors.ChainRegistryActor.{ChainCreationResult, CreateChain}
import io.circe.{Decoder, DecodingFailure, Encoder, Json}
import io.circe.syntax._
import io.circe.parser._
import io.circe.generic.JsonCodec
import io.circe.generic.auto._


//val keyPair = CurveContext.currentKeys

def createNode(i: Int, knownPKs: Set[PublicKey], connectTo: Set[NodeIdToken]) = {
  val keyPair = CurveContext.newKeys()
  val nodeId = NodeIdToken("Node" + i, "127.0.0.1", 2550 + i, keyPair.getPublic, keyPair.getPrivate)
  val chainNode = new ChainNode2(nodeId, keyPair, knownPKs, connectTo)
  (chainNode, nodeId, keyPair)
}

val node1 = createNode(1, Set.empty, Set.empty)

val node2 = createNode(2, Set(node1._3.getPublic), Set(node1._2))

val node3 = createNode(3, Set.empty, Set.empty)

val chain1 = node1._1.newChain()
node1._1.chainRefs()

JwtAlgo.ES512.toString


//def newChain(jwtAlgo: JwtAlgo = JwtAlgo.HS512, initValue: Option[Json] = Some(Json.fromString("{}"))) = {
//  val chainDef: ChainDef = ChainDef.New(jwtAlgo, UUID.randomUUID(), keyPair.getPublic, initValue.map(_.asString.getOrElse("{}")))
//  val chainDefToken = TypedJwtToken[ChainDef](chainDef, keyPair.getPrivate)
//  chainDefToken
//}
//
//val chainDefToken = newChain()
//
//object ActorNode extends ActorNode {
//  override val port: Int = 2222
//}
//
//
//val nc = new com.avalanchain.jwt.jwt.actors.network.NewChain("ANC", chainDefToken, keyPair, ActorNode.system, ActorNode.materializer)