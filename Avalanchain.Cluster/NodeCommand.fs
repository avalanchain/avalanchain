module Avalanchain.Cluster.NodeCommand

open Avalanchain
open Avalanchain.RefsAndPathes
open Avalanchain.EventStream
open Avalanchain.SecKeys

type NodeCommand<'TD when 'TD: equality> = 
    | Post of Transaction<'TD>
    | Admin of NodeAdminCommand
    | Confirmation of ConfirmationPacket
    | Monitor of NodeMonitorQuery
and Transaction<'TD> = Hashed<EventStreamRef> * 'TD
and NodeAdminCommand =
    | AddNode of NodeRef
    | RemoveNode of NodeRef
    //| AllNodes // expects Set<NodeRef>
and StreamAdminCommand<'TS, 'TD when 'TS: equality and 'TD: equality> =
    | AddStream of Hashed<EventStreamDef<'TS, 'TD>>
and ConfirmationPacket = {
    StreamRef: Hashed<EventStreamRef>
    EventHash: Hash
    StateHash: Hash
    NodeProof: Proof // eventHash*stateHash signed
}
and NodeMonitorQuery =
    | Streams                          // Set<StreamStatusData>
    | Stream of Hashed<EventStreamRef> // StreamStatusData option
    | KnownNodeRefs
and StreamStatusData<'TS when 'TS: equality> = {
    Ref: Hashed<EventStreamRef>
    State: Hashed<'TS>
}

