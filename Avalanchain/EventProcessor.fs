module Avalanchain.EventProcessor

open System

open FSharp.Interop.Dynamic
open FSharp.Core.Fluent
open Chessie.ErrorHandling

open SecKeys
open SecPrimitives
open RefsAndPathes
open StreamEvent
open Projection
open EventStream

type CheckResult = Chessie.ErrorHandling.Result<unit, string>


type EventProcessingFailure = 
    | IntegrityFailure
    | PermissionsFailure of string list
    | ProcessingFailure of string list
    | SecurityWarning of string
    | ExecutionWarning of string
    
type EventProcessingResult<'TState> = Result<'TState, EventProcessingFailure>

type ProofIt<'TState, 'TData> = Hashed<EventStreamRef> -> Nonce -> Hashed<StreamState<'TState>> -> HashedEvent<'TData> -> Hashed<ExecutionProof>

let proofIt 
    signer dataHasher 
    (streamRef: Hashed<EventStreamRef>) 
    nonce 
    (hashedState: Hashed<StreamState<'TState>>)
    (hashedEvent: HashedEvent<'TData>) : Hashed<ExecutionProof> =
    let sd = {
        StreamRefHash = streamRef.Hash
        Nonce = nonce
        EventHash = hashedEvent.Hash
        StateHash = hashedState.Hash
    }
    let signature = signer sd//(Unsigned (sdSerializer sd)) 
    let proof = { Data = sd; Signature = signature }
    dataHasher(proof)    

let processEvent 
    (cryptoContext: CryptoContext) 
    serializers 
    (dataHasher: DataHasher<Event<'TData>>) 
    (permissionsChecker: HashedEvent<'TData> -> CheckResult) 
    (proofIt: ProofIt<'TState, 'TData>)
    (streamStep: EventStreamStep<'TState, 'TData>) 
    (event: HashedEvent<'TData>) : EventProcessingResult<EventStreamStep<'TState, 'TData>> =
    // TODO: optimize redundant serializations

    let checkIntegrity (event: HashedEvent<'TData>) = 
        let rehashed = dataHasher event.Value
        if (rehashed.Hash = event.Hash) 
        then ok event
        else fail IntegrityFailure 
    
    let checkPermissions event = 
        match permissionsChecker event with 
        | Ok (_, msgs) -> Ok (event, msgs |> List.map SecurityWarning)
        | Bad msgs -> fail (PermissionsFailure msgs)

    let project (streamStep: EventStreamStep<'TState, 'TData>) (event: HashedEvent<'TData>) =
        let projection = streamStep.Def.Value.Projection.F 
        try
            let res = projection.Invoke(streamStep.State.Value.Value, event.Value.Data)
            match res with
            | Ok (newState, msgs) -> 
                let ns = { 
                    Value = newState 
                    StreamRef = streamStep.Def.Value.Ref
                    Nonce = streamStep.Nonce + 1UL
                }
                Ok (ns, msgs |> List.map ExecutionWarning)
            | Bad msgs -> fail (ProcessingFailure msgs)
        with
            | ex -> fail (ProcessingFailure([sprintf "Error projection execution : '%s'" (ex.ToString())]))

    let buildNewStream (state: StreamState<'TState>) =
        let nonce = state.Nonce
        let merkledEvent = toMerkled serializers.event cryptoContext.Hash (Some streamStep.Event.Merkle) event.Value
        let merkledState = toMerkled serializers.state cryptoContext.Hash (Some streamStep.State.Merkle) state
        let hashedEvent = event
        let hashedState = { Hash = merkledState.Merkle.OwnHash; Value = merkledState.Value } 
        let newStreamStep = {
                                Def = streamStep.Def
                                TimeStamp = DateTimeOffset.UtcNow
                                Event = merkledEvent
                                State = merkledState 
                                Nonce = nonce
                                Proofs = [proofIt streamStep.Def.Value.Ref nonce hashedState hashedEvent] |> Set.ofList
                                // StreamStatus = streamStep.StreamStatus
                            }
        ok newStreamStep

    let run =
        checkIntegrity
        >> bind checkPermissions
        >> bind (project streamStep)
        >> bind buildNewStream

    run event 
 



let addToMerkle hashier initMerkle hashedEvent =
    SecPrimitives.hashToMerkle hashier initMerkle [hashedEvent.Hash]