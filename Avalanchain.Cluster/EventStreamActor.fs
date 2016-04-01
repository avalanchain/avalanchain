module Avalanchain.Cluster.EventStreamActor

open System
open Akka.Actor
open Akka.Persistence
open Akkling
open Akkling.Persistence

open Avalanchain.RefsAndPathes

open NodeCommand


type CommandWrapper = {
    Command: NodeAdminCommand
    TimeStamp: DateTimeOffset
}

type NodeRefEvent = 
    | Command of CommandWrapper
    | SnapshotOffer of SnapshotOffer

type NodeRefQuery = 
    | All 
    | IsKnown of NodeRef

type NodeMessage = 
    | Command of NodeAdminCommand
    | Event of NodeRefEvent
    | Query of NodeRefQuery

let toEvent cmd = 
    NodeRefEvent.Command {
        Command = cmd
        TimeStamp = DateTimeOffset.UtcNow
    } |> Event

let createActor<'TD> (system: IActorRefFactory) name = 
    spawn system name <| propsPersist(fun mailbox -> 
        let rec loop state = 
            actor { 
                let! msg = mailbox.Receive()
                match msg with 
                    | Command cmd -> return Persist (cmd |> toEvent)
                    | Event e -> 
                        match e with
                        | NodeRefEvent.Command c -> 
                            match c.Command with 
                                | AddNode nr -> return! loop (Set.add nr state)
                                | RemoveNode nr -> return! loop (Set.remove nr state)
                        //| SnapshotOffer so -> mailbox.s
                        | _ -> return! loop state 
                    | Query q ->
                        match q with
                        | All -> 
                            mailbox.Sender() <! state
                            return! loop state
                        | IsKnown nr -> 
                            mailbox.Sender() <! state.Contains nr
                            return! loop state
            }
        loop (set[]))

