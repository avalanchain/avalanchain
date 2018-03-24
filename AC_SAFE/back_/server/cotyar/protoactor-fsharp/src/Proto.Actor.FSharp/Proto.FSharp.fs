namespace Proto.FSharp

open Proto
open System.Threading.Tasks
open System

module Async = 
    let  startAsPlainTask (work : Async<unit>) = Async.StartAsTask work :> Task 

module System = 
    let  toFunc<'a> f = Func<'a>(f)
    let  toFunc2<'a, 'b> f = Func<'a, 'b>(f)

[<AutoOpen>]
module Core =
    type SystemMessage =
        | AutoReceiveMessage of AutoReceiveMessage
        | Terminated of Terminated
        | Restarting of Restarting
        | Failure of Failure
        | Watch of Watch
        | Unwatch of Unwatch
        | Restart of Restart
        | Stop of Stop
        | Started of Started
        | ReceiveTimeout of ReceiveTimeout
        | Continuation of Continuation

    let  (|IsSystemMessage|_|) (msg: obj) = 
        match msg with
        | :? AutoReceiveMessage as m -> Some(AutoReceiveMessage m)
        | :? Terminated as m -> Some(Terminated m)
        | :? Restarting as m -> Some(Restarting m)
        | :? Failure as m -> Some(Failure m)
        | :? Watch as m -> Some(Watch m)
        | :? Unwatch as m -> Some(Unwatch m)
        | :? Restart as m -> Some(Restart m)
        | :? Stop as m -> Some(Stop m)
        | :? Started as m -> Some(Started m)
        | :? ReceiveTimeout as m -> Some(ReceiveTimeout m)
        | :? Continuation as m -> Some(Continuation m)
        | _ -> None

    type Decider = PID -> Exception -> SupervisorDirective

    type SupervisionStrategy =
        | DefaultStrategy
        | AllForOneStrategy of decider:Decider * maxNrOfRetries:int * withinTimeSpan:TimeSpan option
        | OneForOneStrategy of decider:Decider * maxNrOfRetries:int * withinTimeSpan:TimeSpan option
        | ExponentialBackoffStrategy of backoffWindow:TimeSpan * initialBackoff:TimeSpan

    type Effect<'State> = IContext -> obj -> 'State -> 'State option
    type AsyncEffect<'State> = IContext -> obj -> 'State -> Async<'State option>

    let  (<|>) (e1: Effect<'State>) (e2: Effect<'State>) = 
        fun ctx o state -> 
            e1 ctx o state |> Option.orElse (e2 ctx o state)

    let (<||>) (e1: AsyncEffect<'State>) (e2: AsyncEffect<'State>) = 
        fun ctx o state -> async {
            let! state1Opt = e1 ctx o state 
            match state1Opt with 
            | Some _ -> return state1Opt
            | None -> return! e2 ctx o state
        }

    let (<|||>) (e1: Effect<'State>) (e2: AsyncEffect<'State>) = 
        fun ctx o state -> async {
            let state1Opt = e1 ctx o state 
            match state1Opt with 
            | Some _ -> return state1Opt
            | None -> return! e2 ctx o state
        }

    let (<&>) (e1: Effect<'State>) (e2: Effect<'State>) = 
        fun ctx o state ->
            let state1Opt = e1 ctx o state
            match state1Opt with
            | Some state1 -> match e2 ctx o state1 with
                                | Some state2 -> Some state2
                                | None -> state1Opt
            | None -> e2 ctx o state

    let (<&&>) (e1: AsyncEffect<'State>) (e2: AsyncEffect<'State>) = 
        fun ctx o state -> async {
            let! state1Opt = e1 ctx o state
            match state1Opt with
            | Some state1 ->    let! state2Opt = e2 ctx o state1 
                                match state2Opt with
                                | Some _ -> return state2Opt
                                | None -> return state1Opt
            | None -> return! e2 ctx o state
        }

    let (<&&&>) (e1: Effect<'State>) (e2: AsyncEffect<'State>) = 
        fun ctx o state -> async {
            let state1Opt = e1 ctx o state
            match state1Opt with
            | Some state1 ->    let! state2Opt = e2 ctx o state1 
                                match state2Opt with
                                | Some _ -> return state2Opt
                                | None -> return state1Opt
            | None -> return! e2 ctx o state
        }

    let typedEffect (handler: IContext -> 'State -> 'Message -> 'State) = 
        fun ctx (o: obj) state -> match o with
                                    | :? 'Message as msg -> Some <| handler ctx state msg
                                    | _ -> None 

    let typedAsyncEffect (handler: IContext -> 'State -> 'Message -> Async<'State>) = 
        fun ctx (o: obj) state ->
            printfn "Expected: %A" (typeof<'Message>)
            printfn "Received: %A" (o.GetType())
            match o with
            | :? 'Message as msg -> async { let! res = handler ctx state msg
                                            return Some res }
            | _ -> async { return None }

    let systemEffect (handler: IContext -> 'State -> SystemMessage -> 'State) = 
        fun ctx o state -> match o with    
                            | IsSystemMessage msg -> Some <| handler ctx state msg
                            | _ -> None 

    let systemAsyncEffect (handler: IContext -> 'State -> SystemMessage -> Async<'State>) = 
        fun ctx o state -> match o with    
                            | IsSystemMessage msg -> async {let! res = handler ctx state msg
                                                            return Some res }
                            | _ -> async { return None } 

    let activeEffect (recognizer: obj -> 'Message option) (handler: IContext -> 'State -> 'Message -> 'State) = 
        fun ctx o state -> recognizer o |> Option.map (handler ctx state)

    let activeAsyncEffect (recognizer: obj -> 'Message option) (handler: IContext -> 'State -> 'Message -> Async<'State>) = 
        fun ctx o state -> match recognizer o with    
                            | Some msg -> async {   let! res = handler ctx state msg
                                                    return Some res }
                            | None -> async { return None } 


    type FSharpAsyncActor<'State>(handler: AsyncEffect<'State>, initialState: 'State) = 
        let mutable state = initialState
        interface IActor with
            member this.ReceiveAsync(context: IContext) =
                async {
                    try
                        // if context.Message.GetType().Name.Contains("GetPos") then
                        let! applied = handler context context.Message state  
                        match applied with 
                        | Some state' -> state <- state'
                        | None -> ()
                    with
                    | x -> 
                        printfn "Failed to execute actor: %A" x
                        raise x
                } |> Async.startAsPlainTask

    type FSharpActor<'State>(handler: Effect<'State>, initialState: 'State) = 
        let mutable state = initialState
        interface IActor with
            member this.ReceiveAsync(context: IContext) =
                async {
                    try
                        let applied = handler context context.Message state  
                        match applied with 
                        | Some state' -> state <- state'
                        | None -> ()
                    with
                    | x -> 
                        printfn "Failed to execute actor: %A" x
                        raise x
                } |> Async.startAsPlainTask

[<RequireQualifiedAccess>]
module Actor =
    let  spawn (props: Props) = Actor.Spawn(props)

    let  spawnPrefix prefix (props: Props) = Actor.SpawnPrefix(props, prefix)

    let  spawnNamed name (props: Props) = Actor.SpawnNamed(props, name)

    let  initProps (producer: unit -> IActor) = Actor.FromProducer(System.Func<_>(producer))

    let  spawnProps p = p |> initProps |> spawn

    let  spawnPropsPrefix prefix = initProps >> spawnPrefix prefix

    let  spawnPropsNamed name = initProps >> spawnNamed name


    let  withState3Async (systemMessageHandler: IContext -> 'State -> SystemMessage -> Async<'State>) (handler: IContext -> 'State -> 'Message -> Async<'State>) (initialState: 'State) =
        fun () -> new FSharpAsyncActor<'State>(systemAsyncEffect(systemMessageHandler) <&&> typedAsyncEffect(handler), initialState) :> IActor
   
    let  withState2Async (handler: IContext -> 'State -> 'Message -> Async<'State>) (initialState: 'State) =
        fun () -> new FSharpAsyncActor<'State>(typedAsyncEffect handler, initialState) :> IActor

    let  withStateAsync (handler: 'State -> 'Message -> Async<'State>) (initialState: 'State) =
        withState2Async (fun _ m s -> handler m s) initialState

    let  create3Async (systemMessageHandler: IContext -> SystemMessage -> Async<unit>) (handler: IContext -> 'Message -> Async<unit>) =
        withState3Async (fun context _ message -> systemMessageHandler context message) (fun context _ message -> handler context message) ()

    let  create2Async (handler: IContext -> 'Message -> Async<unit>) =
        withState2Async (fun context _ message -> handler context message) ()

    let  createAsync (handler: 'Message -> Async<unit>) =
        withState2Async (fun _ _ m -> handler m) ()

    let  withState3 (systemMessageHandler: IContext -> 'State -> SystemMessage -> 'State) (handler: IContext -> 'State -> 'Message -> 'State) (initialState: 'State) =
        fun () -> new FSharpActor<'State>(systemEffect(systemMessageHandler) <&> typedEffect(handler), initialState) :> IActor

    let  withState2 (handler: IContext -> 'State -> 'Message -> 'State) (initialState: 'State) =
        fun () -> new FSharpActor<'State>(typedEffect handler, initialState) :> IActor

    let  withState (handler: 'State -> 'Message -> 'State) (initialState: 'State) =
        withState2 (fun _ s m -> handler s m) initialState

    let  create3 (systemMessageHandler: IContext -> SystemMessage -> unit) (handler: IContext -> 'Message -> unit) =
        withState3 (fun context _ message -> systemMessageHandler context message) (fun context _ message -> handler context message) ()

    let  create2 (handler: IContext -> 'Message -> unit) =
        withState2 (fun context _ message -> handler context message) ()

    let  create (handler: 'Message -> unit) =
        withState2 (fun _ _ m -> handler m) ()


[<RequireQualifiedAccess>]
module Props = 
    open System

    let  newProps() = Props()

    let  withProducer producer (props: Props) = 
        props.WithProducer(producer)

    let  withDispatcher dispatcher (props: Props) = 
        props.WithDispatcher(dispatcher)

    let  withMailbox mailbox (props: Props) = 
        props.WithMailbox(mailbox)

    let  withChildSupervisorStrategy supervisorStrategy (props: Props) =
        let strategy =
            match supervisorStrategy with
            | DefaultStrategy -> Supervision.DefaultStrategy
            | OneForOneStrategy (decider, maxRetries, withinTimeSpan) ->
                let withinTimeSpanNullable =
                    match withinTimeSpan with
                    | None -> Nullable<TimeSpan>()
                    | Some timeSpan -> Nullable<TimeSpan>(timeSpan)
                Proto.OneForOneStrategy(Proto.Decider(decider), maxRetries, withinTimeSpanNullable) :> ISupervisorStrategy
            | AllForOneStrategy (decider, maxRetries, withinTimeSpan) -> 
                let withinTimeSpanNullable =
                    match withinTimeSpan with
                    | None -> Nullable<TimeSpan>()
                    | Some timeSpan -> Nullable<TimeSpan>(timeSpan)
                Proto.AllForOneStrategy(Proto.Decider(decider), maxRetries, withinTimeSpanNullable) :> ISupervisorStrategy
            | ExponentialBackoffStrategy (backoffWindow, initialBackoff) ->
                Proto.ExponentialBackoffStrategy(backoffWindow, initialBackoff) :> ISupervisorStrategy
        props.WithChildSupervisorStrategy(strategy)

    let  withReceiveMiddleware (middleware: Receive -> Receive) (props: Props) =
        props.WithReceiveMiddleware([|toFunc2(middleware)|])

    let  withReceiveMiddlewares (middlewares: (Receive -> Receive) list) (props: Props) =
        middlewares 
        |> List.map toFunc2
        |> Array.ofList
        |> (fun arr -> props.WithReceiveMiddleware(arr))

    let  withSenderMiddleware (middleware: Sender -> Sender) (props: Props) =
        props.WithSenderMiddleware([|toFunc2(middleware)|])

    let  withSenderMiddlewares (middlewares: (Sender -> Sender) list) (props: Props) =
        middlewares 
        |> List.map toFunc2
        |> Array.ofList
        |> (fun arr -> props.WithSenderMiddleware(arr))

    let  withSpawner spawner (props: Props) = 
        props.WithSpawner(spawner)

[<AutoOpen>]
module Pid = 
    let  tell (pid: PID) msg = 
        pid.Tell(msg)

    let  ask (pid: PID) msg = 
        pid.RequestAsync(msg) |> Async.AwaitTask

    let  (<!) (pid: PID) msg = tell pid msg
    let  (>!) msg (pid: PID) = tell pid msg
    let  (<?) (pid: PID) msg = ask pid msg
    let  (>?) msg (pid: PID) = ask pid msg

