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
    Ref: Hashed<EventStreamRef>
    Projection: Projection<StreamState<'TState>, 'TData>
    EmitsTo: EventStreamRef list
    ExecutionPolicy: ExecutionPolicy 
}
and StreamState<'TState> = { // TODO: Change to dynamic?
    Value: 'TState 
    StreamRef: EventStreamRef
    Nonce: uint32
}
and HashedAggregate<'TState> = Hashed<StreamState<'TState>> * EventSpine
and Snapshot<'TState> = HashedAggregate<'TState>

type EventStreamStep<'TState, 'TData> = {
    Def: Hashed<EventStreamDef<'TState, 'TData>>
    TimeStamp: DateTimeOffset
    Event: MerkledEvent<'TData>
    State: Merkled<StreamState<'TState>>
    Nonce: Nonce
    Proofs: Set<ExecutionProof>
    //StreamStatus: EventStreamStatus<'TData>
} with 
    member inline this.Path = this.Def.Value.Ref.Value.Path
    member inline this.Version = this.Def.Value.Ref.Value.Version
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
    member inline this.Path = this.Def.Value.Ref.Value.Path
    member inline this.Version = this.Def.Value.Ref.Value.Version
//    member inline this.Hash = this.Def.Value.Ref.Hash




type StreamEventProcessor<'TState, 'TData> = 
    CryptoContext -> EventStreamStep<'TState, 'TData> -> Event<'TData> -> Result<EventStreamStep<'TState, 'TData>, string>

type Serializers<'TData, 'TState> = {
    data: Serializer<'TData>
    event: Serializer<Event<'TData>>
    state: Serializer<StreamState<'TState>>
    epd: Serializer<ExecutionProofData>
}

//ExecutionProof
let proofIt epdSerializer (cryptoContext: CryptoContext) streamDef nonce (hashedEvent: HashedEvent<'TData>) hashedState = 
    let epd = {
        StreamRefHash = streamDef.Ref.Hash
        Nonce = nonce
        EventHash = hashedEvent.Hash // TODO: After fixing merkleTree remove hashing from there
        StateHash = hashedState.Hash
    }
    let signature = cryptoContext.Sign (Unsigned (epdSerializer epd)) 
    { Data = epd; Signature = signature }

let streamEventProcessor serializers (cryptoContext: CryptoContext) (streamStep: EventStreamStep<'TState, 'TData>) (event: Event<'TData>) =
//    let verifyEvent he =
//        cryptoContext.Verify 
   
    let project event =
        let projection = streamStep.Def.Value.Projection.F // TODO: Rethink projection invoking on state
        try
            projection.Invoke(streamStep.State.Value, event.Data)
        with
            | ex -> fail (sprintf "Error projection execution : '%s'" (ex.ToString()))

    let buildNewStream state =
        let nonce = streamStep.Nonce + 1UL
        let merkledEvent = toMerkled serializers.event cryptoContext.Hash (Some streamStep.Event.Merkle) event
        let merkledState = toMerkled serializers.state cryptoContext.Hash (Some streamStep.State.Merkle) state
        let hashedEvent = { Hash = merkledEvent.Merkle.OwnHash; Value = merkledEvent.Value } 
        let hashedState = { Hash = merkledState.Merkle.OwnHash; Value = merkledState.Value } 
        let newStreamStep = {
                                Def = streamStep.Def
                                TimeStamp = DateTimeOffset.UtcNow
                                Event = merkledEvent
                                State = merkledState // TODO: optimize redundant serializations
                                Nonce = nonce
                                Proofs = [proofIt serializers.epd cryptoContext streamStep.Def.Value nonce hashedEvent hashedState] |> Set.ofList
                                // StreamStatus = streamStep.StreamStatus
                            }
        ok newStreamStep

    let run = 
        project 
        >> bind buildNewStream

    run event