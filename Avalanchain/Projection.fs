module Avalanchain.Projection

open System
open Microsoft.FSharp.Quotations
open FSharp.Quotations.Evaluator
open Chessie.ErrorHandling

open SecKeys
open RefsAndPathes
open System.Collections.Generic
open System.Linq

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
with member inline this.Hash = this.Proof.ValueHash

type HashableFunction<'T1, 'T2, 'TResult> = { 
    F : Func<'T1, 'T2, 'TResult>
    Expr: Expr<Func<'T1, 'T2, 'TResult>>
    Proof: Proof
}
with member inline this.Hash = this.Proof.ValueHash

type SerializedFunction = Proof * Serialized

type HashableFunctionBuilder<'T, 'TResult> = 
    ProofVerifier -> Deserializer<HashableFunction<'T, 'TResult>> -> SerializedFunction -> Result<HashableFunction<'T, 'TResult>, string> 

type HashableFunctionBuilder<'T1, 'T2, 'TResult> = 
    ProofVerifier -> Deserializer<HashableFunction<'T1, 'T2, 'TResult>> -> SerializedFunction -> Result<HashableFunction<'T1, 'T2, 'TResult>, string> 

type FunctionSerializer<'T, 'TResult> = Signer -> Serializer<Expr<Func<'T, 'TResult>>> -> Hasher -> SerializedFunction

type FunctionSerializer<'T1, 'T2, 'TResult> = Signer -> Serializer<Expr<Func<'T1, 'T2, 'TResult>>> -> Hasher -> SerializedFunction

let deserializeFunction (proofVerifier: ProofVerifier) deserializer (serializedFunction: SerializedFunction) =
    let checkProof sf =
        if proofVerifier(fst sf) then ok sf else fail ("Proof verification failed") 

    let deserialize sf = 
        try
            ok (fst sf, deserializer(snd sf))
        with
            | ex -> fail (sprintf "Error during deserialization : '%s'" (ex.ToString()))

    let compile hf =
        let proof, expr = hf
        try
            ok (proof, expr, (QuotationEvaluator.Evaluate expr))
        with
            | ex -> fail (sprintf "Error function compilation : '%s'" (ex.ToString()))

    let validate = 
        checkProof
        >> bind deserialize 
        >> bind compile 
        >> bind (fun (proof, expr, f) -> 
                    ok {
                        F = f
                        Expr = expr
                        Proof = proof
                    })

    validate serializedFunction


let serializeFunction signer serializer hasher expr =
    let serialized = serializer expr
    let hash = hasher serialized
    let proof = {
        Signature = signer hash
        ValueHash = hash
    }
    proof, serialized

// NOTE: Projection takes just event data and not the whole event
//type Projection<'TState, 'TEventData> = 'TState -> 'TEventData -> ProjectionResult<'TState>
type Projection<'TState, 'TData> = HashableFunction<'TState, 'TData, ProjectionResult<'TState>>
and ProjectionResult<'TState> = Result<'TState, string>


type ProjectionStorage<'TState, 'TData> (serializer, deserializer) = 
    //Projection<'TState, 'TData> list
    let projections = new Dictionary<Hash, Projection<'TState, 'TData>>()
    member inline this.Add (projection: Projection<'TState, 'TData>) = projections.[projection.Hash] <- deserializer projection
    member inline this.AddAll (projs: Projection<'TState, 'TData> seq) = projs |> Seq.iter (fun p -> projections.[p.Hash] <- deserializer p)
    member inline this.Item hash = projections.TryGetValue(hash) |> (fun (b, res) -> if b then Some res else None)
    member inline this.Projections = projections.Values
    member inline this.Export = projections.Values |> Seq.map serializer
    