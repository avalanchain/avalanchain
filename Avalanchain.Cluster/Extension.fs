module Avalanchain.Cluster.Extension

open System.Collections.Generic
open System.Collections.Concurrent

open Akka.Actor
open Akka.Actor.Internal
open Avalanchain
open Avalanchain.Cluster.Actors
open Avalanchain.Cluster.Actors.Stream2
open Chessie.ErrorHandling
open Avalanchain.EventStream
open Akka.FSharp
open Actors.KeyValue
open Actors.Stream

type ChainNodeExtension =
    inherit ExtensionIdProvider<ChainNode>
        override __.CreateExtension (system: ExtendedActorSystem) = new ChainNode(system :?> ActorSystemImpl)

and ChainNode(system: ActorSystemImpl) =
    let ct = Utils.cryptoContext
    let nodeContexts = new ConcurrentDictionary<string, obj>()

    member this.GetNodeContext<'TS, 'TD when 'TD: equality and 'TS: equality>() =
        let key = typedefof<'TD>.FullName + "~" + typedefof<'TS>.FullName
        let nc = nodeContexts.GetOrAdd(key, (fun k -> NodeContext.buildNodeContext<'TD, 'TS>(ct) :> obj))
        nc :?> NodeContext.NodeContext<'TS, 'TD>

    member this.GetByHashBucket<'T> name =
        let key = "hash~" + name + "~" + typedefof<'T>.FullName
        let actor = system.SystemGuardian.GetChild([key])
        if ActorRefs.Nobody.Equals(actor) then
            spawn system key <| actorOf2 (KeyValue.bucket)
        else
            actor

    member this.GetByStreamBucket<'TS, 'TE> name f =
        let key = "hash~" + name + "~" + typedefof<'TS>.FullName + "~" + typedefof<'TE>.FullName
        let actor = system.SystemGuardian.GetChild([key])
        if ActorRefs.Nobody.Equals(actor) then
            system.ActorOf (Props.Create(<@fun () -> new StreamActor2<'TS, 'TE, string>(f, name)@>), key)
        else
            actor
    
    member this.StreamContext<'TS, 'TD when 'TD: equality and 'TS: equality>() = 
        let nodeContext = this.GetNodeContext<'TD, 'TS>()
        {
            StreamLogicContext.Hasher = nodeContext.CryptoContext.Hasher
            StreamLogicContext.Proofer = nodeContext.Proofer
            StreamLogicContext.DataHasher = nodeContext.DataHashers.eventDh
            StreamLogicContext.StateHasher = nodeContext.DataHashers.stateDh
            StreamLogicContext.PermissionsChecker = (fun data -> ok(())) // TODO: add permission checking
        }

    interface Akka.Actor.IExtension 
    static member Get (system: ActorSystem): ChainNode =
            system.WithExtension<ChainNode, ChainNodeExtension>()

    member this.KeyValueLogic = KeyValue.kvLogic
    member this.StreamLogic = Stream.streamLogic
    member this.StreamLogic2<'TS, 'TD when 'TD: equality and 'TS: equality> streamDef = Stream2.streamLogic<'TS, 'TD, EventProcessingMsg> (this.StreamContext()) streamDef

        // GetByHash
    member private this.GetFromBucket<'T> bucketName (hash: Hash) : Async<'T> = 
        let bucket = this.GetByHashBucket<'T> bucketName
        async {
            return! bucket <? KVMsg<Hash, 'T>.Get(hash)
        }

    member private this.AddToBucket<'T> bucketName (hash: Hash) value = 
        let bucket = this.GetByHashBucket<'T> bucketName
        bucket <! KVMsg<Hash, 'T>.Add(hash, value) // TODO: Replace with ack delivery


    member this.StreamRefByHash<'T> (hash: Hash) = 
        this.GetFromBucket<'T> "stream-ref" hash
    member this.StreamDefByHash<'T> (hash: Hash) = 
        this.GetFromBucket<'T> "stream-def" hash
    member this.EventByHash<'T> (hash: Hash) = 
        this.GetFromBucket<'T> "event" hash
    member this.StateByHash<'T> (hash: Hash) = 
        this.GetFromBucket<'T> "state" hash
    member this.FrameByHash<'T> (hash: Hash) = 
        this.GetFromBucket<'T> "frame" hash
    member this.MerkleByHash<'T> (hash: Hash) = 
        this.GetFromBucket<'T> "merkle" hash

    member this.StreamRefAddHash<'T> (hash: Hash) value = 
        this.AddToBucket<'T> "stream-ref" hash value
    member this.StreamDefAddHash<'T> (hash: Hash) value = 
        this.AddToBucket<'T> "stream-def" hash value
    member this.EventAddHash<'T> (hash: Hash) value = 
        this.AddToBucket<'T> "event" hash value
    member this.StateAddHash<'T> (hash: Hash) value = 
        this.AddToBucket<'T> "state" hash value
    member this.FrameAddHash<'T> (hash: Hash) value = 
        this.AddToBucket<'T> "frame" hash value
    member this.MerkleAddHash<'T> (hash: Hash) value = 
        this.AddToBucket<'T> "merkle" hash value

