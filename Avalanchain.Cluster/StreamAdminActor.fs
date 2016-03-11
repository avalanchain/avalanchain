module Avalanchain.Cluster.StreamAdminActor

//open System
//open Akka.Actor
//open Akka.Persistence
//open Akkling
//open Akkling.Persistence
//
//open Avalanchain.RefsAndPathes
//
//open NodeCommand
//
//
//type CommandWrapper<'TS, 'TD when 'TS: equality and 'TD: equality> = {
//    Command: StreamAdminCommand<'TS, 'TD>
//    TimeStamp: DateTimeOffset
//}
//
//type StreamAdminEvent<'TS, 'TD when 'TS: equality and 'TD: equality> = 
//    | Command of CommandWrapper<'TS, 'TD>
//    | SnapshotOffer of SnapshotOffer
//
//type StreamAdminQuery = 
//    | All 
//    | IsKnown of NodeRef
//
//type StreamAdminMessage<'TS, 'TD when 'TS: equality and 'TD: equality> = 
//    | Command of StreamAdminCommand<'TS, 'TD>
//    | Event of StreamAdminEvent<'TS, 'TD>
//    | Query of StreamAdminQuery
//
//let toEvent cmd = 
//    StreamAdminEvent.Command {
//        Command = cmd
//        TimeStamp = DateTimeOffset.UtcNow
//    } |> Event
//
//let createActor<'TS, 'TD> (system: IActorRefFactory) = 
//    spawn system "node-ref-store" <| propsPersist(fun mailbox -> 
//        let rec loop state = 
//            actor { 
//                let! msg = mailbox.Receive()
//                match msg with 
//                    | Command cmd -> return Persist ((cmd |> toEvent))
//                    | Event e -> 
//                        match e with
//                        | StreamAdminEvent.Command c -> 
//                            match c.Command with 
//                                | AddStream sd -> return! loop (Set.add nr state)
//                                | AddNestedStream (parent, sd) -> return! loop (Set.add nr state)
//                                | RemoveStream nr -> return! loop (Set.remove nr state)
//                        //| SnapshotOffer so -> mailbox.s
//                        | _ -> return! loop state 
//                    | Query q ->
//                        match q with
//                        | All -> 
//                            mailbox.Sender() <! state
//                            return! loop state
//                        | IsKnown nr -> 
//                            mailbox.Sender() <! state.Contains nr
//                            return! loop state
//            }
//        loop (set[]))



