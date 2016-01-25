# Akkling

This is the experimental fork of Akka.FSharp library, introducing new features such as typed actor refs, and also simplifying existing Akka.FSharp API. The main reason for splitting from official API is to be able to introduce new (also experimental), but possibly breaking changes outside existing Akka release cycle.

**Read wiki pages for more info.**

## Hello world

```fsharp
open Akkling

use system = System.create "my-sys" <| Configuration.defaultConfig()
let aref = spawn system "printer" <| actorOf (printfn "Hello %s")

aref <! "world"
```

## Maintainer(s)

- [@Horusiath](https://github.com/Horusiath)