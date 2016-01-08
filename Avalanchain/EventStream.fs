module Avalanchain.EventStream

open System

open FSharp.Interop.Dynamic
open FSharp.Core.Fluent
open Chessie.ErrorHandling

open SecKeys
open SecPrimitives
open RefsAndPathes
open StreamEvent
open Projection
open Quorum

type EventStreamStatus<'TData> =
    | Offline
    | Online
    | Paused
    | BlockedByEvent of string * MerkledEvent<'TData> * Exception option

type EventStreamDef<'TState, 'TData> = {
    Ref: EventStreamRef
    Projection: Projection<StreamState<'TState>, 'TData>
    EmitsTo: EventStreamRef list
    ExecutionPolicy: ExecutionPolicy 
}
and StreamState<'TState> = { // TODO: Change to dynamic?
    Value: 'TState 
    StreamRef: EventStreamRef
    Nonce: uint32
    Proofs: Set<ExecutionProof>
}
and HashedAggregate<'TState> = Hashed<StreamState<'TState>> * EventSpine
and Snapshot<'TState> = HashedAggregate<'TState>

type EventStreamStep<'TState, 'TData> = {
    Def: Hashed<EventStreamDef<'TState, 'TData>>
    TimeStamp: DateTimeOffset
    Event: MerkledEvent<'TData>
    State: Merkled<StreamState<'TState>>
    Nonce: Nonce
    //StreamStatus: EventStreamStatus<'TData>
} with 
    member inline this.Path = this.Def.Value.Ref.Path
    member inline this.Version = this.Def.Value.Ref.Version
//    member inline this.Hash = this.Def.Value.Ref.Hash

type EventStream<'TState, 'TData> = {
    Def: Hashed<EventStreamDef<'TState, 'TData>>
    //TimeStamp: DateTimeOffset
    Steps: Merkled<EventStreamStep<'TState, 'TData>> list
    Events: MerkledEvent<'TData>
    States: Hashed<StreamState<'TState>>
    LatestNonce: Nonce
    StreamStatus: EventStreamStatus<'TData>
} with 
    member inline this.Path = this.Def.Value.Ref.Path
    member inline this.Version = this.Def.Value.Ref.Version
//    member inline this.Hash = this.Def.Value.Ref.Hash




type StreamEventProcessor<'TState, 'TData> = 
    CryptoContext -> EventStreamStep<'TState, 'TData> -> Event<'TData> -> Result<EventStreamStep<'TState, 'TData>, string>

type Serializers<'TData, 'TState> = {
    data: Serializer<'TData>
    event: Serializer<Event<'TData>>
    state: Serializer<StreamState<'TState>>
    epd: Serializer<ExecutionProofData>
}

let streamEventProcessor serializers (cryptoContext: CryptoContext) streamStep (event: Event<'TData>) =
//    let verifyEvent he =
//        cryptoContext.Verify 
    let merkler (event: Event<'TData>) nonce hashedState : MerkledEvent<'TData> = 
        let merkleTree = toMerkle serializers.data cryptoContext.Hash (Some (streamStep.Event |> fst).Merkle) [event.Data]
        let merkled = { Merkle = merkleTree; Value = event }
        let epd = {
            StreamRef = streamStep.Def.Value.Ref
            Nonce = nonce
            EventHash = cryptoContext.Hash (serializers.event event) // TODO: After fixing merkleTree remove hashing from there
            StateHash = hashedState.Hash
        }
        let signature = cryptoContext.Sign (Unsigned (serializers.epd epd)) 
        let proof = { Data = epd; Signature = signature }
        merkled, proof
    
    let project event =
        let projection = streamStep.Def.Value.Projection.F // TODO: Rethink projection invoking on state
        try
            projection.Invoke(streamStep.State.Value, event.Data)
        with
            | ex -> fail (sprintf "Error projection execution : '%s'" (ex.ToString()))

    let buildNewStream state =
        let hashedState = dataHasher serializers.state cryptoContext state
        let nonce = streamStep.Nonce + 1u
        let newStreamStep = {
                            Def = streamStep.Def
                            TimeStamp = DateTimeOffset.UtcNow
                            Event = merkler event nonce hashedState // MerkledEvent<'TData> // TODO
                            State = toMerkled serializers.state cryptoContext.Hash (Some streamStep.State.Merkle) hashedState.Value // TODO: optimize redundant serializations
                            Nonce = nonce
                            // StreamStatus = streamStep.StreamStatus
                        }
        ok newStreamStep

    let run = 
        project 
        >> bind buildNewStream

    run event