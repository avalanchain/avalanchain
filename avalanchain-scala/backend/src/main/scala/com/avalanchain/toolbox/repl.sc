import akka.actor.{Actor, ActorSystem}
import akka.stream.ActorMaterializer
import akka.stream.scaladsl.{Flow, Source, Tcp}
import akka.stream.scaladsl.Tcp.{IncomingConnection, ServerBinding}
import akka.util.ByteString
import com.avalanchain.core.builders.CryptoContextBuilder
import com.avalanchain.toolbox.REPL
import scorex.crypto.signatures.Curve25519

import scala.concurrent.Future

val curve = new Curve25519

val (context, privKey) = CryptoContextBuilder()

val pubKey = context.signingPublicKey

println(pubKey)
println(privKey)


///////
implicit val system = ActorSystem()
implicit val materializer = ActorMaterializer()

/////// Signed communication

//val echoS = REPL.echoServer("127.0.0.1", 10888)
//val echoC = REPL.echoClient("127.0.0.1", 10888)
