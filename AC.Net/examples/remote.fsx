#r "../src/Akkling/bin/Release/Akka.dll"
#r "../src/Akkling/bin/Release/Hyperion.dll"
#r "../src/Akkling/bin/Release/Newtonsoft.Json.dll"
#r "../src/Akkling/bin/Release/FSharp.PowerPack.dll"
#r "../src/Akkling/bin/Release/FSharp.PowerPack.Linq.dll"
#r "../src/Akkling/bin/Release/Akkling.dll"
#r "../src/Akkling/bin/Release/System.Collections.Immutable.dll"
#r "../packages/DotNetty.Common/lib/net45/DotNetty.Common.dll"
#r "../packages/DotNetty.Buffers/lib/net45/DotNetty.Buffers.dll"
#r "../packages/DotNetty.Codecs/lib/net45/DotNetty.Codecs.dll"
#r "../packages/DotNetty.Handlers/lib/net45/DotNetty.Handlers.dll"
#r "../packages/DotNetty.Transport/lib/net45/DotNetty.Transport.dll"
#r "../packages/FsPickler/lib/net45/FsPickler.dll"
#r "../packages/Google.ProtocolBuffers/lib/net40/Google.ProtocolBuffers.dll"
#r "../packages/Google.ProtocolBuffers/lib/net40/Google.ProtocolBuffers.Serialization.dll"
#r "../packages/Akka.Remote/lib/net45/Akka.Remote.dll"

open System
open Akkling
open Akka.Actor

let server = System.create "server" <| Configuration.parse """
    akka {
        actor.provider = "Akka.Remote.RemoteActorRefProvider, Akka.Remote"
        remote.helios.tcp {
            hostname = localhost
            port = 4500
        }
    }
"""

let client = System.create "client" <| Configuration.parse """
    akka {
        actor.provider = "Akka.Remote.RemoteActorRefProvider, Akka.Remote"
        remote.helios.tcp {
            hostname = localhost
            port = 0
        }
    }
"""

let remoteProps addr actor = { propse actor with Deploy = Some (Deploy(RemoteScope(Address.Parse addr))) }

let printer =
    spawn client "remote-actor" (remoteProps "akka.tcp://server@localhost:4500" <@ actorOf2 (fun ctx msg -> printfn "%A received: %s" ctx.Self msg |> ignored) @>)

printer <! "hello"
printer <! "world"