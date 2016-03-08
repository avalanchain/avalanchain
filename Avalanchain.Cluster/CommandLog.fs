module Avalanchain.Cluster.CommandLog

open System
open Akka.Actor
open Akka.Persistence
open Akkling
open Akkling.Persistence

open NodeCommand


type CommandWrapper<'TD when 'TD: equality> = {
    Command: NodeCommand<'TD>
    TimeStamp: DateTimeOffset
}

type CommandEvent<'TD when 'TD: equality> = 
    | Command of CommandWrapper<'TD>
    | SnapshotOffer of SnapshotOffer


type CommandLogMessage<'TD when 'TD: equality> = 
    | Command of NodeCommand<'TD>
    | Event of CommandEvent<'TD>

let toEvent (cmd: NodeCommand<'TD>) = 
    CommandEvent.Command {
        Command = cmd
        TimeStamp = DateTimeOffset.UtcNow
    } |> Event

let createActor<'TD> (system: IActorRefFactory) = 
    spawn system "command-log" <| propsPersist(fun mailbox -> 
        let rec loop state = 
            actor { 
                let! msg = mailbox.Receive()
                match msg with 
                    | Command cmd -> return Persist ((cmd |> toEvent))
                    | Event e -> return! loop (e::state)
//                            match e with
//                            | NodeEvent.Command c -> return! loop (e::state)
//                            | SnapshotOffer so -> mailbox.s
            }
        loop [])

