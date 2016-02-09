module Avalanchain.Cluster.ActorEventStream

open Avalanchain
open Avalanchain.EventStream
open Chessie.ErrorHandling
open Sharded

//type ActorEventStream<'TState, 'TData when 'TData: equality and 'TState: equality>
//    (def: Hashed<EventStreamDef<'TState, 'TData>>, hasher, dataHasher, eventProcessor: StreamEventProcessor<'TState, 'TData>(*, frameSynchronizer, initFrame *), frameSerializer, shardedSystem: ShardedSystem) =
//
////    let mutable frames = PersistentVector.empty
////    let mutable frameRefs = PersistentHashMap.empty
////    let mutable eventRefs = PersistentHashMap.empty
////    let mutable stateRefs = PersistentHashMap.empty
//    let mutable merkledFrame = Option.None 
////    let mutable subscribers = PersistentHashMap.empty
//
//    let actorSelection = shardedSystem.ActorSelector("/streams/" + def.Value.Ref.Value.Path)
//
//    // TODO: Add Acl checks
//    interface IEventStream<'TState, 'TData> with
////        member this.Subscribe(subscriber) = subscribers <- subscribers.Add (subscriber.Ref, subscriber)
////        member this.Unsubscribe(subscriber) = subscribers <- subscribers.Remove (subscriber.Ref)
//        
//        member this.Def with get() = def
//        member this.Ref with get() = def.Value.Ref
//        member this.CurrentFrame with get() = frames.tryHead()
//        member this.CurrentState with get() = frames.tryHead() |> Option.bind (fun x -> Some x.Value.State.HashedValue)
//        member this.GetEvent<'TData> eventRef = 
//            if eventRefs.ContainsKey(eventRef) then ok (eventRefs.[eventRef])
//            else fail (DataNotExists(eventRef.ToString()))
//        member this.GetState<'TState> stateRef =
//            if stateRefs.ContainsKey(stateRef) then ok (stateRefs.[stateRef])
//            else fail (DataNotExists(stateRef.ToString()))
//        member this.GetFrame<'TState, 'TData> frameRef =
//            if frameRefs.ContainsKey(frameRef) then ok (frameRefs.[frameRef])
//            else fail (DataNotExists(frameRef.ToString()))
//        member this.GetByNonce nonce = 
//            if frames.length > int(nonce) then ok frames.[int(nonce)]
//            else fail (DataNotExists(nonce.ToString()))
//        member this.GetFromNonce nonce =
//            if frames.length > int(nonce) then ok (seq { for i = int(nonce) to frames.length do yield frames.[i] })
//            else fail (DataNotExists(nonce.ToString()))
//        member this.Push hashedEvent = 
//            let currentFrame = (this :> IEventStream<'TState, 'TData>).CurrentFrame.bind(fun f -> Some f.Value)
//            let newFrame = eventProcessor def currentFrame hashedEvent
//            match newFrame with
//            | Bad _ -> newFrame
//            | Ok (frame, msgs) -> 
//                let hashedFrame = dataHasher frame
//                frames <- frames.Conj hashedFrame
//                frameRefs <- frameRefs.Add (hashedFrame.Hash, hashedFrame)
//                eventRefs <- eventRefs.Add (hashedFrame.Value.Event.HashedValue.Hash, hashedFrame.Value.Event.HashedValue)
//                stateRefs <- stateRefs.Add (hashedFrame.Value.State.HashedValue.Hash, hashedFrame.Value.State.HashedValue)
//                merkledFrame <- Some (toMerkled frameSerializer hasher (merkledFrame.bind(fun f -> Some f.Merkle)) frame)
//                //subscribers |> Seq.iter (fun s -> (snd s).Push (hashedFrame.Value.State.HashedValue.Value.Value) |> ignore)
//                newFrame
