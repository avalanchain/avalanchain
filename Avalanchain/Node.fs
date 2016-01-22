module Avalanchain.Node

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
open EventStream
open EventProcessor
open FrameSynchronizer


// Bag of Streams
type EventStreamBag<'TState, 'TData when 'TData: equality and 'TState: equality>(streams: IEventStream<'TState, 'TData> list) = 
    let mutable streamMap = Map(streams.map(fun s -> s.Def.Value.Ref, s))
    // Checks if all toEmit streams have corresponding definitions
    let allEmitsExist = 
        let notDefinedEmitStream = 
            streams.
                collect(fun s -> s.Def.Value.EmitsTo).
                tryFind(fun es -> streamMap.tryFind(fun sm -> es.Hash = sm.Key.Hash).IsNone)
        if notDefinedEmitStream.IsSome
        then failwith (sprintf "Stream {%s} for Emitting is not defined" notDefinedEmitStream.Value.Value.Path)

    member this.StreamMap = streamMap
    member this.Add (stream: IEventStream<'TState, 'TData>) = streamMap.Add (stream.Def.Value.Ref, stream)
    member this.Delete (streamRef: Hashed<EventStreamRef>) = streamMap.Remove streamRef
    member this.Item (streamRef: Hashed<EventStreamRef>) = streamMap.[streamRef] // TODO: Add reaction to "not found"
    member this.Refs = streamMap.map(fun kv -> kv.Key)
    member this.Streams = streamMap.map(fun kv -> kv.Value)

type Topology<'TState, 'TData when 'TData: equality and 'TState: equality> = IEventStream<'TState, 'TData> list // TODO: Probably replace with some Tree<'T>?

type Node<'TState, 'TData when 'TData: equality and 'TState: equality> = {
    Path: NodePath
    CryptoContext: CryptoContext
    Serializers: Serializers<'TState, 'TData>
    Streams: EventStreamBag<'TState, 'TData>
    EventHasher: DataHasher<Event<'TData>>
    //FrameSynchronizationContextBuilder: ExecutionGroup -> ExecutionPolicy -> FrameSynchronizationContext<'TState, 'TData>

    // OwnIPAddress: IPAddress
    ExecutionGroups: ExecutionGroup list 
}
with 
    member private this.ToEvent data = {
            Data = data
            SubmitterKey = this.CryptoContext.SigningPublicKey
            SubmitterSignature = this.CryptoContext.Signer(Unsigned(this.Serializers.data data))
            SubmittedVia = NodeRef(this.CryptoContext.SigningPublicKey)
        }
    member this.Push (streamRef: Hashed<EventStreamRef>) data =
        let stream = this.Streams.[streamRef] // TODO: Handle "not found"
        let event = this.ToEvent data
        let hashedEvent = this.EventHasher event
        stream.Push(hashedEvent)


//let buildNode<'TData, 'TState when 'TData: equality and 'TState: equality> nodePath projectionExprs = 
let buildNode nodePath projectionExprs = 
    let ct = cryptoContextRSANet("RSA Test")

    let ss = serializeFunction ct.HashSigner Utils.picklerSerializer ct.Hasher
    let ds = deserializeFunction ct.ProofVerifier Utils.picklerDeserializer
    let projectionStorage = ProjectionStorage(ss, ds)

    let executionSigner signer serializer executionProofData = 
        let serd = serializer executionProofData
        let signed = signer (Unsigned serd)
        signed

    let serializers = picklerSerializers
    let dhs = dataHashers ct serializers

    let pfr = proofer (executionSigner ct.Signer serializers.epd) dhs.epDh
    let permissionsChecker hashedEvent = ok() // TODO: Add proper permission checking

    let eventProcessor = processEvent ct picklerSerializers dhs.eventDh permissionsChecker pfr

    let buildStream projectionExpr =
        let streamRef = {
            EventStreamRef.Path = "/stream/" + (fst projectionExpr)
            Version = 0u
        }

        let hashedStreamRef = dhs.streamRefDh streamRef

        let newStream projection = 
            let streamDef = {
                Ref = hashedStreamRef
                Projection = projection
                EmitsTo = []
                ExecutionPolicy = ExecutionPolicy.None 
            }

            let hashedStreamDef = dhs.streamDefDh streamDef
            let eventStream = EventStream (hashedStreamDef, ct.Hasher, dhs.frameDh, eventProcessor, serializers.frame) :> IEventStream<decimal, decimal>
            ok (eventStream)

        snd projectionExpr |> projectionStorage.Add >>= newStream

    let newNode streams =
        let node = {
            Path = "/node/" + nodePath
            CryptoContext = ct
            Serializers = serializers
            Streams = EventStreamBag(streams)
            EventHasher = dhs.eventDh
            ExecutionGroups = [ExecutionGroup "_main_"]
        }
        ok (node)

    projectionExprs |> List.map buildStream |> collect >>= newNode

let defaultProjections : (string * Quotations.Expr<decimal -> decimal -> Result<decimal, string>>) list = [
    "Sum", <@ fun (s:decimal) e -> ok (s + e) @>
    "Max", <@ fun (s:decimal) e -> ok (Math.Max(s, e)) @>
    "Min", <@ fun (s:decimal) e -> ok (Math.Min(s, e)) @>
    "First", <@ fun (s:decimal) e -> ok (Unchecked.defaultof<decimal>) @>
    "Last", <@ fun (s:decimal) e -> ok (e) @>
    "LastAbs", <@ fun (s:decimal) e -> ok (Math.Abs(s)) @>
]

let defaultNode = defaultProjections |> buildNode "_DefaultNode_" |> returnOrFail