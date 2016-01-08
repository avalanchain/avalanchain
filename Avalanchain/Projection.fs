module Avalanchain.Projection

open System
open Microsoft.FSharp.Quotations
open FSharp.Quotations.Evaluator
open Chessie.ErrorHandling

open SecKeys
open RefsAndPathes

//type HashableFunction<'T, 'TResult> = { 
//    verifier: Verifier, func: Expr<Func<'T, 'TResult>>, proof: Proof) = 
//    member private this.Func = QuotationEvaluator.Evaluate func
//    member this.Expr = func
//    member this.Invoke = 
//}

type HashableFunction<'T, 'TResult> = { 
    F : Func<'T, 'TResult>
    Expr: Expr<Func<'T, 'TResult>>
    Proof: Proof
}

type HashableFunction<'T1, 'T2, 'TResult> = { 
    F : Func<'T1, 'T2, 'TResult>
    Expr: Expr<Func<'T1, 'T2, 'TResult>>
    Proof: Proof
}

type SerializedFunction = Proof * Serialized

type HashableFunctionBuilder<'T, 'TResult> = 
    ProofVerifier -> Deserializer<HashableFunction<'T, 'TResult>> -> SerializedFunction -> Result<HashableFunction<'T, 'TResult>, string> 

let deserializeFunction (proofVerifier: ProofVerifier) deserializer (serializedFunction: Serialized) =
    let deserialize sf = 
        try
            ok (deserializer(sf))
        with
            | ex -> fail (sprintf "Error during deserialization : '%s'" (ex.ToString()))

    let checkProof hf =
        if proofVerifier(fst hf) then ok hf else fail ("Proof verification failed") 

    let compile hf =
        let proof, expr = hf
        try
            ok (proof, expr, (QuotationEvaluator.Evaluate expr))
        with
            | ex -> fail (sprintf "Error function compilation : '%s'" (ex.ToString()))

    let validate = 
        deserialize 
        >> bind checkProof
        >> bind compile 
        >> bind (fun (proof, expr, f) -> 
                    ok {
                        F = f
                        Expr = expr
                        Proof = proof
                    })

    validate serializedFunction

//type HashedFunction<'T> = Hashed<SerializableFunction<'T>>

// NOTE: Projection takes just event data and not the whole event
//type Projection<'TState, 'TEventData> = 'TState -> 'TEventData -> ProjectionResult<'TState>
type Projection<'TState, 'TEventData> = HashableFunction<'TState, 'TEventData, ProjectionResult<'TState>>
and ProjectionResult<'TState> = Result<'TState, string>
