module ServerTests.Tests

open Expecto
open Avalanchain.Core.Crypto
// open ServerCode
// open ServerCode.Storage

let wishListTests =
  testList "Crypto" [
    testCase "default contains F# mastering book" <| fun _ -> 
      let ctx = CryptoContext.generate()
      let testStr = "Test string"
      let token = sign ctx testStr
      Expect.isSome token "Signing failed"

      let verificationResult = verify ctx token.Value
      Expect.isSome token "Verification failed"
  ]