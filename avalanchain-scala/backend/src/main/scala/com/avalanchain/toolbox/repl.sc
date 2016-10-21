import com.avalanchain.core.builders.CryptoContextBuilder

val context = CryptoContextBuilder()

val pubKey = context.signingPublicKey

println(pubKey)
