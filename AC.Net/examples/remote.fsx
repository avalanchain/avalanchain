#r "../bin/Avalanchain/Akka.dll"
#r "../bin/Avalanchain/Hyperion.dll"
#r "../bin/Avalanchain/Newtonsoft.Json.dll"
#r "../bin/Avalanchain/FSharp.PowerPack.dll"
#r "../bin/Avalanchain/FSharp.PowerPack.Linq.dll"
#r "../bin/Avalanchain/Akkling.dll"
#r "../bin/Avalanchain/System.Collections.Immutable.dll"
#r "../bin/Avalanchain/DotNetty.Common.dll"
#r "../bin/Avalanchain/DotNetty.Buffers.dll"
#r "../bin/Avalanchain/DotNetty.Codecs.dll"
#r "../bin/Avalanchain/DotNetty.Handlers.dll"
#r "../bin/Avalanchain/DotNetty.Transport.dll"
#r "../bin/Avalanchain/FsPickler.dll"
#r "../bin/Avalanchain/Google.ProtocolBuffers.dll"
#r "../bin/Avalanchain/Google.ProtocolBuffers.Serialization.dll"
#r "../bin/Avalanchain/Akka.Remote.dll"

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