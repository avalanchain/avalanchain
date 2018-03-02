//////

type Sender = Sender of string
type Packet<'k, 'v when 'v: comparison> = {
    Sender: Sender
    Key: 'k
    Value: 'v
}
type ConfirmationStatus<'v when 'v: comparison> =
    | Confirmed of Value: 'v
    | Reconfirmed of Value: 'v * PreviousValue: 'v * PreviousCount: uint32
    | NotConfirmed
type ConfirmationState<'v when 'v: comparison> = {
    Status: ConfirmationStatus<'v>
    MinConfCount: uint32
    Values: Map<'v, Set<Sender>>
    ValuesCount: uint32
}
    with
        static member Empty() = {   Status = NotConfirmed
                                    MinConfCount = UInt32.MaxValue
                                    Values = Map.empty
                                    ValuesCount = 0u }
        member __.Value = match __.Status with Confirmed v | Reconfirmed (v, _, _) -> Some v | NotConfirmed -> None

type ConfirmationAttemptResult<'v when 'v: comparison> =
    | Added of ConfirmationState<'v>
    | SenderCheckFailed of ConfirmationState<'v>
    member __.State = match __ with Added s -> s | SenderCheckFailed s -> s

type Confirmations<'k, 'v when 'v: comparison>(senderChecker: Packet<'k, 'v> -> bool, minConfCount: uint32) =
    let mutable state: ConfirmationState<'v> = { ConfirmationState<'v>.Empty() with MinConfCount = minConfCount }
    member __.State = state
    member __.TryAdd(packet: Packet<'k, 'v>) =
        if senderChecker packet then
            let oldSendersForValue = match state.Values |> Map.tryFind packet.Value with
                                        | Some vv -> vv
                                        | None -> Set.empty
            let newSendersForValue = oldSendersForValue |> Set.add packet.Sender
            let newValues = state.Values |> Map.add packet.Value newSendersForValue
            let newValue, newCount =
                newValues
                |> Map.toSeq
                |> Seq.map(fun (v, ss) -> (v, ss |> Set.count |> uint32))
                |> Seq.maxBy(fun (_, c) -> c) // The Seq _will_ always have at list one element as we just added it

            let newStatus =
                if newCount < state.MinConfCount then NotConfirmed
                else
                    match state.Status with
                    | NotConfirmed -> Confirmed newValue
                    | Confirmed (value) when value = newValue || (value <> newValue && state.ValuesCount = newCount) -> state.Status // Keep existing value if counts the same
                    | Confirmed (value) -> Reconfirmed (newValue, value, state.ValuesCount)
                    | Reconfirmed _ -> Confirmed (newValue)

            let newState = { state with Values = newValues; ValuesCount = newCount; Status = newStatus }
            state <- newState
            Added state
        else
            SenderCheckFailed state


let confs = Confirmations<int, string>((fun p -> p.Sender <> Sender "wrong"), 3u)
let state = confs.State

confs.TryAdd { Sender = Sender "good 1"; Key = 1; Value = "val 1" }
confs.TryAdd { Sender = Sender "good 2"; Key = 1; Value = "val 1" }
confs.TryAdd { Sender = Sender "wrong"; Key = 1; Value = "val 2" }
confs.TryAdd { Sender = Sender "good 3"; Key = 1; Value = "val 2" }
confs.TryAdd { Sender = Sender "good 4"; Key = 1; Value = "val 4" }
confs.TryAdd { Sender = Sender "good 5"; Key = 1; Value = "val 1" }
confs.TryAdd { Sender = Sender "good 5"; Key = 1; Value = "val 2" } // Can vote for two values
confs.TryAdd { Sender = Sender "good 6"; Key = 1; Value = "val 2" }
confs.TryAdd { Sender = Sender "good 7"; Key = 1; Value = "val 2" }
