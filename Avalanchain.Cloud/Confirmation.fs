namespace Avalanchain.Cloud

module Confirmation = 
    open Avalanchain.Quorum

    type Confirmation<'T> = {
        NodeId: string
        ValueId: ValueId
        Value: 'T
        Notifier: 'T -> unit
    }
    and ValueId = string

    type ConfirmationResult<'T> =
        | InvalidConfirmation
        | ConfirmedSame
        | ConfirmedDifferent of 'T
        | NotConfirmedYet

    type ConfirmationCounter<'T when 'T: equality> (policy: ExecutionPolicy, validator, policyChecker) =
        let mutable confirmations = []
        let mutable invalidConfirmations = []
        let mutable pendingConfirmations = []
        let mutable confirmedValue = None
        member __.Policy = policy
        member __.AddConfirmation (confirmation: Confirmation<'T>) = 
            if not <| validator confirmation then 
                invalidConfirmations <- confirmation :: invalidConfirmations
                InvalidConfirmation
            else
                confirmations <- confirmation :: confirmations
                match confirmedValue with
                | Some v -> 
                    if confirmation.Value = v then ConfirmedSame
                    else ConfirmedDifferent v
                | None ->
                    confirmedValue <- policyChecker policy confirmations // TODO: Add possibility for reconfirmations
                    match confirmedValue with
                    | Some v -> 
                        for pc in pendingConfirmations do pc.Notifier v // Notifying pendings
                        if confirmation.Value = v then ConfirmedSame
                        else ConfirmedDifferent v
                    | None -> 
                        pendingConfirmations <- confirmation :: pendingConfirmations
                        NotConfirmedYet
        member __.Confirmations with get() = confirmations
        member __.InvalidConfirmations with get() = invalidConfirmations
        member __.PendingConfirmations with get() = pendingConfirmations
            
  
//    let ofQueue (queue: CloudQueue<'T>) f = 
//        asyncSeq { 
//            let centroidsSoFar = ResizeArray()
//            while true do
//                match queue.TryDequeue() with
//                | Some d ->                  
//                        yield d
//                        do! Async.Sleep 1
//                | None -> do! Async.Sleep 1
//        }
//        |> AsyncSeq.map(f)   

