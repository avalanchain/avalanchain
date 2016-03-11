module Avalanchain.Cluster.NodeActor

open Akka.Actor
open Akkling
open Avalanchain.Quorum
open Extension
open NodeCommand
open Avalanchain.NodeContext
open Chessie.ErrorHandling


let createNodeActor<'TS, 'TD when 'TS: equality and 'TD: equality> (system: IActorRefFactory) nodePath =
    spawn system "node"
        <| props(fun mailbox ->
                // define child actor
                let commandLog = CommandLog.createActor<'TD> mailbox
                let nodeRefStore = NodeRefStore.createActor mailbox
                let nodeExtension = ChainNode.Get mailbox.System
                let node() = nodeExtension.GetNode<'TS, 'TD>(nodePath, [ExecutionGroup.Default])
                    
                // define parent behavior
                let rec loop() =
                    actor {
                        let! (msg: NodeCommand<'TD>) = mailbox.Receive()
                        commandLog.Forward(msg)  // forward all messages through the log
                        match msg with
                        | Admin c -> nodeRefStore <! NodeRefStore.NodeMessage.Command c
                        | Post post -> 
                            let streamRef, t = post
                            let ret = node().Push streamRef t
                            mailbox.Sender() <! ret
                        | Confirmation c -> 
                            () // TODO:
                        | Monitor m ->
                            match m with 
                            | Streams -> 
                                let ret = node().States
                                mailbox.Sender() <! ret
                            | Stream streamRef -> 
                                let ret = node().State streamRef
                                mailbox.Sender() <! ret
                            | KnownNodeRefs -> 
                                let refs = 
                                    async {
                                            return! nodeRefStore <? NodeRefStore.Query (NodeRefStore.NodeRefQuery.All)
                                        } |> Async.RunSynchronously // TODO: Remove sync 
                                mailbox.Sender() <! refs
                            
                        return! loop()
                    }
                loop())

let test system =
    let nodeExtension = ChainNode.Get system
    let nodeStore = nodeExtension.NodeStore

    let sf = [|1.0; 2.0; 3.0|]
                |> StreamFlow.ofArray "/flow" nodeStore [ExecutionGroup.Default] 
                >>= StreamFlow.map ExecutionPolicy.Pass (fun x -> ok (x*x))

    printfn "res ->> %A" sf