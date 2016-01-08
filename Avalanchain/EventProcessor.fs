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
    

let processEvent 
    (dataHasher: DataHasher<Event<'TData>>) 
    (permissionsChecker: HashedEvent<'TData> -> CheckResult) 
    projection 
    state 
    (event: HashedEvent<'TData>) (*: EventProcessingResult<StreamState<'TData>>*) =

    let checkIntegrity (event: HashedEvent<'TData>) = 
        let rehashed = dataHasher event.Value
        if (rehashed.Hash = event.Hash) 
        then ok event
        else fail IntegrityFailure 
    
    let checkPermissions event = 
        match permissionsChecker event with 
        | Ok (_, msgs) -> ok (event, msgs)
        | Bad msgs -> fail (PermissionsFailure msgs)

    let runProjection state event = 
        match projection state event with
        | Ok (newState, msgs) -> ok (newState, msgs)
        | Bad msgs -> fail (ProcessingFailure msgs)

    let run =
        checkIntegrity
        >> bind checkPermissions
        >> bind (runProjection state)

    run event 
 
let signState signer dataHasher streamRef signerKey hashedState hashedEvent nonce : Hashed<ExecutionProof> =
    let sd = {
        StreamRef = streamRef
        Nonce = nonce
        EventHash = hashedEvent.Hash
        StateHash = hashedState.Hash
    }
    let signature = signer sd//(Unsigned (sdSerializer sd)) 
    let proof = { Data = sd; Signature = signature }

    dataHasher(proof)

//let signEvent signer signerKey event : ExecutionProof * HashedEvent<'TData> =
//    let proof = {
//        Signature = signer(event)
//        Key = signerKey
//        ValueHash = event.Hash
//    }
//    proof, event

let addToMerkle hashier initMerkle hashedEvent =
    SecPrimitives.hashToMerkle hashier initMerkle [hashedEvent.Hash]