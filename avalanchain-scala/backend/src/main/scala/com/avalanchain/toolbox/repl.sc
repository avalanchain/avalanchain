import akka.actor.{Actor, ActorSystem}
import akka.stream.ActorMaterializer
import akka.stream.scaladsl.{Flow, Source, Tcp}
import akka.stream.scaladsl.Tcp.{IncomingConnection, ServerBinding}
import akka.util.ByteString
import com.avalanchain.core.builders.CryptoContextBuilder
import com.avalanchain.core.domain._
import com.avalanchain.toolbox.REPL
import scorex.crypto.signatures.Curve25519

import scala.concurrent.Future

val curve = new Curve25519

val (context, privKey) = CryptoContextBuilder()

def toHexedK(key: SecurityKey) = context.bytes2Hexed(key.bytes)
def toHexedH(hashedValue: HashedValue) = context.bytes2Hexed(hashedValue.hash.hash)

val pubKey = context.signingPublicKey

println(toHexedK(pubKey))
println(toHexedK(privKey))

val str = "Hi"
val hashed = context.hasher(context.text2Bytes(str))
println(toHexedH(hashed))
val hashed2 = context.hasher(context.text2Bytes(str))
println(toHexedH(hashed2))
hashed.bytes sameElements hashed2.bytes

val signed = context.signer(context.text2Bytes(str))
println(signed.proof)
val verified = context.verifier(signed.proof, signed.value)
println(verified)

//val signed = context.signer(str)
//
//println(signed)

///////
//implicit val system = ActorSystem()
//implicit val materializer = ActorMaterializer()

/////// Signed communication

//val echoS = REPL.echoServer("127.0.0.1", 10888)
//val echoC = REPL.echoClient("127.0.0.1", 10888)
