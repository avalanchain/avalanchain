#r "../packages/Inferno/lib/net452/SecurityDriven.Inferno.dll"
#r "../packages/jose-jwt/lib/net40/jose-jwt.dll"

#time

open System.Security.Cryptography
open SecurityDriven.Inferno
open SecurityDriven.Inferno.Extensions

let dsaKeyPrivate = CngKeyExtensions.CreateNewDsaKey()
let dsaKeyPrivateBlob = dsaKeyPrivate.GetPrivateBlob()
let dsaKeyPublicBlob = dsaKeyPrivate.GetPublicBlob()
let dsaKeyPublic: CngKey = dsaKeyPublicBlob.ToPublicKeyFromBlob()

for i in 0 .. 999 do CngKeyExtensions.CreateNewDsaKey() |> ignore

open Jose


let payload = [ "sub", "mr.x@contoso.com" |> box 
                "exp", 1300819380 |> box
            ]


let token = Jose.JWT.Encode(payload, dsaKeyPrivate, JwsAlgorithm.ES384)

for i in 0 .. 999 do Jose.JWT.Encode(payload, dsaKeyPrivate, JwsAlgorithm.ES384) |> ignore

let pwd = "secret" |> System.Text.Encoding.UTF8.GetBytes
for i in 0 .. 19999 do Jose.JWT.Encode(payload, pwd, JwsAlgorithm.HS384) |> ignore

let sb = X509CertificateBuilder()

X509Certificates.X509Certificate()