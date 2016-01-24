// Learn more about F# at http://fsharp.org. See the 'F# Tutorial' project
// for more guidance on F# programming.

#load "Library1.fs"
open Avalanchain.Cluster

// Define your library scripting code here

#time "on"
#load "Bootstrap.fsx"

open System
open Akka.Actor
open Akka.Configuration
open Akka.FSharp
open Akka.TestKit

// #Event Bus
// Originally conceived as a way to send messages to groups of actors, the EventBus has been generalized to Event Stream
// #Event Stream
// The event stream is the main event bus of each actor system: it is used for carrying log messages and Dead Letters and may be used by the user code for other purposes as well. It uses Subchannel Classification which enables registering to related sets of channels

let system = ActorSystem.Create("FSharp")

let echoServer = 
    spawn system "EchoServer"
    <| fun mailbox ->
        let rec loop() =
            actor {
                let! message = mailbox.Receive()
                match box message with
                | :? string -> 
                    //printfn "Echo '%s'" message
                    return! loop()
                | _ ->  failwith "unknown message"
            } 
        loop()

let eventStream = system.EventStream

eventStream.Subscribe(echoServer, typedefof<string>)

eventStream.Publish("Anybody home?")
eventStream.Publish("Knock knock")

for i = 0 to 1000000 do eventStream.Publish("Knock knock") 


type ActorMsg =
    | Hello of string
    | Hi

let echoServer1 = 
    spawn system "EchoServer1"
    <| fun mailbox ->
        let rec replyEn() =
            actor {
                let! message = mailbox.Receive()
                match message with
                | Hello name -> printfn "Hi %s" name
                | Hi -> printfn "Hi!"

                return! replySw()
            } 
        and replySw() =
            actor {
                let! message = mailbox.Receive()
                match message with
                | Hello name -> printfn "Hallå %s" name
                | Hi -> printfn "Hallå!"

                return! replyEn()
            } 

        replyEn()

echoServer1 <! Hello "F# group!"
echoServer1 <! Hello "Akka.NET team!"

echoServer1 <! Hello "Akka.NET team!aaa"

system.Shutdown()