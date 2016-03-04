module Avalanchain.NodeContext

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
//    // Checks if all toEmit streams have corresponding definitions    // TODO: Reintroduce EmitTo logic
//    do (let notDefinedEmitStream = 
//            streams.
//                collect(fun s -> s.Def.Value.EmitsTo).
//                tryFind(fun es -> streamMap.tryFind(fun sm -> es.Hash = sm.Key.Hash).IsNone)
//        if notDefinedEmitStream.IsSome
//        then failwith (sprintf "Stream {%s} for Emitting is not defined" notDefinedEmitStream.Value.Value.Path)
//    )

    member this.StreamMap = streamMap
    member this.Add (stream: IEventStream<'TState, 'TData>) = 
        if (not <| streamMap.ContainsKey stream.Def.Value.Ref) then
            streamMap <- streamMap.Add (stream.Def.Value.Ref, stream)
        stream
    member this.Remove (streamRef: Hashed<EventStreamRef>) = 
        streamMap <- streamMap.Remove streamRef
        ()
    member this.Item (streamRef: Hashed<EventStreamRef>) = streamMap.[streamRef] // TODO: Add reaction to "not found"
    member this.Refs = streamMap.map(fun kv -> kv.Key)
    member this.Streams = streamMap.map(fun kv -> kv.Value)

type Topology<'TState, 'TData when 'TData: equality and 'TState: equality> = IEventStream<'TState, 'TData> list // TODO: Probably replace with some Tree<'T>?

type NodeContext<'TState, 'TData when 'TData: equality and 'TState: equality> = {
    CryptoContext: CryptoContext
    Serializers: Serializers<'TState, 'TData>
    DataHashers: DataHashers<'TState, 'TData>
    ProjectionStorage: ProjectionStorage<'TState, 'TData>
    EventProcessor: StreamEventProcessor<'TState, 'TData>
    Proofer: Proofer<'TState, 'TData>
    PermissionsChecker: HashedEvent<'TData> -> DataResult<unit>
    //FrameSynchronizationContextBuilder: ExecutionGroup -> ExecutionPolicy -> FrameSynchronizationContext<'TState, 'TData>
}

type Node<'TState, 'TData when 'TData: equality and 'TState: equality>(path: NodePath, executionGroups: ExecutionGroup list, nodeContext) =
    // OwnIPAddress: IPAddress
    let streams = EventStreamBag<'TState, 'TData>([])

    member __.Address = nodeContext.CryptoContext.Address
    member private this.ToEvent data = {
            Data = data
            SubmitterKey = nodeContext.CryptoContext.SigningPublicKey
            SubmitterSignature = nodeContext.CryptoContext.Signer(Unsigned(nodeContext.Serializers.data data))
            SubmittedVia = nodeContext.DataHashers.nodeRefDh(path, nodeContext.CryptoContext.SigningPublicKey)
        }
    member this.Push (streamRef: Hashed<EventStreamRef>) data =
        let stream = streams.[streamRef] // TODO: Handle "not found"
        let event = this.ToEvent data
        let hashedEvent = nodeContext.DataHashers.eventDh event
        stream.Push(hashedEvent)

    member this.CreateStreamDef path version (projectionExpr: ProjectionExpr<'TState, 'TData>) executionPolicy = 
        let streamRef = {
            Path = path
            Version = version
        }
        let projection = nodeContext.ProjectionStorage.ToProjection(projectionExpr)
        let buildDef prj = 
            let streamDef = {
                Ref = nodeContext.DataHashers.streamRefDh streamRef
                Projection = prj
                //EmitsTo: Hashed<EventStreamRef> list //TODO: Add EmitTo
                ExecutionPolicy = executionPolicy 
            }
            ok (nodeContext.DataHashers.streamDefDh streamDef)

        projection >>= buildDef

    member this.CreateStreamFromDef hashedStreamDef =
        let createStream projection = 
            let eventStream = 
                EventStream (hashedStreamDef, nodeContext.CryptoContext.Hasher, nodeContext.DataHashers.frameDh, nodeContext.EventProcessor, nodeContext.Serializers.frame) :> IEventStream<'TState, 'TData>
            ok (eventStream)

        let newStream = hashedStreamDef.Value.Projection.Expr 
                        |> nodeContext.ProjectionStorage.Add 
                        >>= createStream
                        |> lift streams.Add
        newStream

    member this.CreateStream path version (projectionExpr: ProjectionExpr<'TState, 'TData>) executionPolicy = 
        this.CreateStreamDef path version projectionExpr executionPolicy 
        >>= this.CreateStreamFromDef

    member this.States = streams.Streams |> Seq.map(fun s -> s.Ref, s.CurrentState)
    member this.State streamRef = streams.StreamMap.TryFind streamRef


let buildNodeContext<'TState, 'TData when 'TData: equality and 'TState: equality>(ct: CryptoContext) =
    let ss = serializeFunction ct.HashSigner Utils.picklerSerializer ct.Hasher
    let ds = deserializeFunction ct.ProofVerifier Utils.picklerDeserializer
    let projectionStorage = ProjectionStorage(ss, ds)

    let executionSigner signer serializer executionProofData = 
        let serd = serializer executionProofData
        let signed = signer (Unsigned serd)
        signed

    let serializers = picklerSerializers
    let dhs = dataHashers<'TState, 'TData> ct serializers

    let pfr = proofer (executionSigner ct.Signer serializers.epd) dhs.epDh
    let permissionsChecker hashedEvent = ok() // TODO: Add proper permission checking

    let eventProcessor = processEvent ct picklerSerializers dhs.eventDh permissionsChecker pfr

    //let executionGroupBuilder // TODO: Add executionGroupBuilder

    let nodeContext = {
        CryptoContext = ct
        Serializers = serializers
        DataHashers = dhs
        ProjectionStorage = projectionStorage
        EventProcessor = eventProcessor
        Proofer = pfr
        PermissionsChecker = permissionsChecker
    }
    nodeContext

//let buildNode<'TData, 'TState when 'TData: equality and 'TState: equality> nodePath projectionExprs = 
let buildNode nodePath executionGroups nodeContext = 
    Node(nodePath, executionGroups, nodeContext)


let registerProjections (node: Node<'TState, 'TData>) projectionExprs =
    projectionExprs 
    |> List.map (fun (path, ver, pe, ep) -> node.CreateStream path ver pe ep) 
    |> collect 
    >>= (fun _ -> ok(node))


//let defaultProjections : (string * uint32 * ProjectionExpr<decimal, decimal> * ExecutionPolicy) list = [
//    "Sum", 0u, <@ fun (s:decimal) e -> ok (s + e) @>, ExecutionPolicy.None
//    "Max", 0u, <@ fun (s:decimal) e -> ok (Math.Max(s, e)) @>, ExecutionPolicy.None
//    "Min", 0u, <@ fun (s:decimal) e -> ok (Math.Min(s, e)) @>, ExecutionPolicy.None
//    "First", 0u, <@ fun (s:decimal) e -> ok (Unchecked.defaultof<decimal>) @>, ExecutionPolicy.None
//    "Last", 0u, <@ fun (s:decimal) e -> ok (e) @>, ExecutionPolicy.None
//    "LastAbs", 0u, <@ fun (s:decimal) e -> ok (Math.Abs(s)) @>, ExecutionPolicy.None
//]
//
//let defaultNode = defaultProjections  
//                    |> registerProjections (buildNodeContext(Utils.cryptoContext) |> buildNode "_DefaultNode_" [ExecutionGroup.Default])
//                    |> returnOrFail