module Avalanchain.Projection

open System
open Microsoft.FSharp.Quotations
open FSharp.Quotations.Evaluator
open Chessie.ErrorHandling

open SecKeys
open RefsAndPathes
open System.Collections.Generic
open System.Linq
open System.Collections.Concurrent

//type HashableFunction<'T, 'TResult> = { 
//    verifier: Verifier, func: Expr<Func<'T, 'TResult>>, proof: Proof) = 
//    member private this.Func = QuotationEvaluator.Evaluate func
//    member this.Expr = func
//    member this.Invoke = 
//}

type HashableFunction<'T, 'TResult> = { 
    F : 'T -> 'TResult
    Expr: Expr<'T -> 'TResult>
    Proof: Proof
}
with member inline this.Hash = this.Proof.ValueHash

type HashableFunction<'T1, 'T2, 'TResult> = { 
    F : 'T1 -> 'T2 -> 'TResult
    Expr: Expr<'T1 -> 'T2 -> 'TResult>
    Proof: Proof
}
with 
    member inline this.Hash = this.Proof.ValueHash
    override this.Equals(yobj) =
        match yobj with
        | :? HashableFunction<'T1, 'T2, 'TResult> as y -> (this.Hash = y.Hash)
        | _ -> false
 
    override x.GetHashCode() = hash x.Hash

type SerializedFunction = Proof * Serialized

type HashableFunctionBuilder<'T, 'TResult> = 
    (*ProofVerifier -> Deserializer<HashableFunction<'T, 'TResult>> -> *)
    SerializedFunction -> Result<HashableFunction<'T, 'TResult>, string> 

type HashableFunctionBuilder<'T1, 'T2, 'TResult> = 
    (*ProofVerifier -> Deserializer<HashableFunction<'T1, 'T2, 'TResult>> -> *)
    SerializedFunction -> Result<HashableFunction<'T1, 'T2, 'TResult>, string> 

type FunctionSerializer<'T, 'TResult> = (*Signer -> Serializer<Expr<Func<'T, 'TResult>>> -> Hasher -> *)
    Expr<'T -> 'TResult> -> SerializedFunction

type FunctionSerializer<'T1, 'T2, 'TResult> = (*Signer -> Serializer<Expr<Func<'T1, 'T2, 'TResult>>> -> Hasher -> *)
    Expr<'T1 -> 'T2 -> 'TResult> -> SerializedFunction

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
        Proof.Signature = signer hash
        ValueHash = hash
    }
    proof, serialized

// NOTE: Projection takes just event data and not the whole event
type Projection<'TState, 'TData  when 'TData: equality and 'TState: equality> = 
    HashableFunction<'TState, 'TData, ProjectionResult<'TState>>
and ProjectionResult<'TState> = Result<'TState, string>
and ProjectionExpr<'TState, 'TData> = Quotations.Expr<'TState -> 'TData -> ProjectionResult<'TState>>

type ProjectionSerializer<'TState, 'TData> = FunctionSerializer<'TState, 'TData, ProjectionResult<'TState>>
type ProjectionDeserializer<'TState, 'TData> = HashableFunctionBuilder<'TState, 'TData, ProjectionResult<'TState>>


type ProjectionStorage<'TState, 'TData  when 'TData: equality and 'TState: equality> (serializeFunction: ProjectionSerializer<'TState, 'TData>, deserializeFunction: ProjectionDeserializer<'TState, 'TData>) = 
    let projections = new ConcurrentDictionary<Hash, Projection<'TState, 'TData>>()

    member this.ToProjection(projExpr: ProjectionExpr<'TState, 'TData>) =
        (serializeFunction projExpr) |> deserializeFunction
        
    member this.AddSerialized (projection: SerializedFunction) = 
        let result = deserializeFunction projection 
        result |> either (fun (p, _) -> projections.[(fst projection).ValueHash] <- p) ignore // TODO: Ignore already added
        result
    member this.AddAllSerialized (projs: SerializedFunction seq) = projs |> Seq.map (this.AddSerialized)
    member this.Add (projExpr: ProjectionExpr<'TState, 'TData>) = 
        let serialized = serializeFunction projExpr
        this.AddSerialized serialized
    member this.AddAll (projs: ProjectionExpr<'TState, 'TData> seq) = projs |> Seq.map (this.Add)
    member this.Item hash = projections.TryGetValue(hash) |> (fun (b, res) -> if b then Some res else None)
    member this.Projections = projections.Values
    member this.Export = projections.Values |> Seq.map (fun p -> serializeFunction p.Expr)
    member this.Import projs = 
        projections.Clear()
        this.AddAll projs


//type IntProjectionStorage = ProjectionStorage<int, int> (serializer, deserializer)

let latestPE<'T> : ProjectionExpr<'T, 'T> = <@ fun (s:'T) e -> ok (e) @>
let emptyPE<'T> : ProjectionExpr<'T, 'T> = <@ fun (s:'T) e -> ok (Unchecked.defaultof<'T>) @>
let constPE<'T> c : ProjectionExpr<'T, 'T> = <@ fun (s:'T) e -> ok (c) @>
let nonFailingPE<'TState, 'TData> (f: 'TState -> 'TData -> 'TState) : ProjectionExpr<'TState, 'TData> = <@ fun s e -> ok (f s e) @>