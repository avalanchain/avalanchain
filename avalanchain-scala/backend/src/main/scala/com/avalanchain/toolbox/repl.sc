import com.avalanchain.core.builders.CryptoContextBuilder
import com.avalanchain.core.domain.{PrivateKey, PublicKey, _}
import scorex.crypto.signatures.Curve25519

val curve = new Curve25519

val (context, privKey) = CryptoContextBuilder()

val pubKey = context.signingPublicKey

println(pubKey)
println(privKey)
