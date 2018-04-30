namespace FSharpx
#nowarn "40"

open System
open System.Collections
open System.Collections.Generic
open System.Runtime.ExceptionServices
//open FSharpx.Collections

module Result =
    let ofOption error = function Some s -> Ok s | None -> Error error

    type ResultBuilder() =
        member __.Return(x) = Ok x

        member __.ReturnFrom(m: Result<_, _>) = m

        member __.Bind(m, f) = Result.bind f m
        member __.Bind((m, error): (Option<'T> * 'E), f) = m |> ofOption error |> Result.bind f

        member __.Zero() = None

        member __.Combine(m, f) = Result.bind f m

        member __.Delay(f: unit -> _) = f

        member __.Run(f) = f()

        member __.TryWith(m, h) =
            try __.ReturnFrom(m)
            with e -> h e

        member __.TryFinally(m, compensation) =
            try __.ReturnFrom(m)
            finally compensation()

        member __.Using(res:#IDisposable, body) =
            __.TryFinally(body res, fun () -> match res with null -> () | disp -> disp.Dispose())

        member __.While(guard, f) =
            if not (guard()) then Ok () else
            do f() |> ignore
            __.While(guard, f)

        member __.For(sequence:seq<_>, body) =
            __.Using(sequence.GetEnumerator(), fun enum -> __.While(enum.MoveNext, __.Delay(fun () -> body enum.Current)))

    let result = new ResultBuilder()

    // type MyErr = Err1 | Err2

    // let aa : Result<string, MyErr> = 
    //     result {
    //       let! (a: string) = Ok "a string"
    //       printfn "A: %A" a
    //     //   let! b = Error Err2
    //     //   printfn "B: %A" b
    //       let! c = (Some "c string", Err1)
    //     //   let! c = (None, Err1)
    //       printfn "C: %A" c
    //       let d = if true then a else c
    //       printfn "D: %A" d
    //       return d
    //     }

module Option =

    /// The maybe monad.
    /// This monad is my own and uses an 'T option. Others generally make their own Maybe<'T> type from Option<'T>.
    /// The builder approach is from Matthew Podwysocki's excellent Creating Extended Builders series http://codebetter.com/blogs/matthew.podwysocki/archive/2010/01/18/much-ado-about-monads-creating-extended-builders.aspx.
    type MaybeBuilder() =
        member this.Return(x) = Some x

        member this.ReturnFrom(m: 'T option) = m

        member this.Bind(m, f) = Option.bind f m

        member this.Zero() = None

        member this.Combine(m, f) = Option.bind f m

        member this.Delay(f: unit -> _) = f

        member this.Run(f) = f()

        member this.TryWith(m, h) =
            try this.ReturnFrom(m)
            with e -> h e

        member this.TryFinally(m, compensation) =
            try this.ReturnFrom(m)
            finally compensation()

        member this.Using(res:#IDisposable, body) =
            this.TryFinally(body res, fun () -> match res with null -> () | disp -> disp.Dispose())

        member this.While(guard, f) =
            if not (guard()) then Some () else
            do f() |> ignore
            this.While(guard, f)

        member this.For(sequence:seq<_>, body) =
            this.Using(sequence.GetEnumerator(),
                                 fun enum -> this.While(enum.MoveNext, this.Delay(fun () -> body enum.Current)))
    let maybe = MaybeBuilder()

    /// Maps a Nullable to Option
    let ofNullable (n: _ Nullable) = 
        if n.HasValue
            then Some n.Value
            else None

    /// Maps an Option to Nullable
    let toNullable =
        function
        | None -> Nullable()
        | Some x -> Nullable(x)

    /// True -> Some(), False -> None
    let inline ofBool b = if b then Some() else None

    /// Converts a function returning bool,value to a function returning value option.
    /// Useful to process TryXX style functions.
    let inline tryParseWith func = func >> function
       | true, value -> Some value
       | false, _ -> None
    
    /// If true,value then returns Some value. Otherwise returns None.
    /// Useful to process TryXX style functions.
    let inline ofBoolAndValue b = 
        match b with
        | true,v -> Some v
        | _ -> None

    /// Maps Choice 1Of2 to Some value, otherwise None.
    let ofChoice =
        function
        | Choice1Of2 a -> Some a
        | _ -> None

    /// Gets the value associated with the option or the supplied default value.
    let inline getOrElse v =
        function
        | Some x -> x
        | None -> v

    /// Gets the value associated with the option or the supplied default value.
    let inline getOrElseLazy (v: _ Lazy) =
        function
        | Some x -> x
        | None -> v.Value

    /// Gets the value associated with the option or the supplied default value from a function.
    let inline getOrElseF v =
        function
        | Some x -> x
        | None -> v()

    /// Gets the value associated with the option or fails with the supplied message.
    let inline getOrFail m =
        function
        | Some x -> x
        | None -> failwith m

    /// Gets the value associated with the option or raises the supplied exception.
    let inline getOrRaise e =
        function
        | Some x -> x
        | None -> raise e

    /// Bottom value
    let undefined<'T> : 'T = raise (NotImplementedException("result was implemented as undefined"))

    let reraise' (e:exn) : 'T = ExceptionDispatchInfo.Capture(e).Throw() ; undefined

    /// Gets the value associated with the option or reraises the supplied exception.
    let inline getOrReraise e =
        function
        | Some x -> x
        | None -> reraise' e

    /// Gets the value associated with the option or the default value for the type.
    let getOrDefault =
        function
        | Some x -> x
        | None -> Unchecked.defaultof<_>
    
    /// Gets the option if Some x, otherwise the supplied default value.
    let inline orElse v =
        function
        | Some x -> Some x
        | None -> v

    let inline orElseLazy (v : _ Lazy) =
        function
        | Some x -> Some x
        | None -> v.Force()

    /// Applies a predicate to the option. If the predicate returns true, returns Some x, otherwise None.
    let inline filter pred =
        function
        | Some x when pred x -> Some x
        | _ -> None

    /// Attempts to cast an object. Returns None if unsuccessful.
    [<CompiledName("Cast")>]
    let inline cast (o: obj) =
        try
            Some (unbox o)
        with _ -> None

    let inline getOrElseWith v f =
        function
        | Some x -> f x
        | None -> v

    // Additional Option-Module extensions

    /// Haskell-style maybe operator
    let option (defaultValue : 'U) (map : 'T -> 'U) = function
        | None   -> defaultValue
        | Some a -> map a

    /// transforms a function in the Try...(input, out output) style
    /// into a function of type: input -> output Option
    /// Example: fromTryPattern(System.Double.TryParse)
    /// See Examples.Option
    let fromTryPattern (tryFun : ('input -> (bool * 'output))) =
        fun input ->
            match tryFun input with
            | (true,  output) -> Some output
            | (false,      _) -> None

    /// Concatenates an option of option.
    let inline concat x = 
        x >>= id

    let inline isNone (o:Option<'a>) : bool =
        match o with
        | None -> true
        | _ -> false

    let inline isSome (o:Option<'a>) : bool =
        match o with
        | Some _ -> true
        | _ -> false


module Nullable =
    let (|Null|Value|) (x: _ Nullable) =
        if x.HasValue then Value x.Value else Null

    let create x = Nullable x
    /// Gets the value associated with the nullable or the supplied default value.
    let getOrDefault n v = match n with Value x -> x | _ -> v
    /// Gets the value associated with the nullable or the supplied default value.
    let getOrElse (n: Nullable<'T>) (v: Lazy<'T>) = match n with Value x -> x | _ -> v.Force()
    /// Gets the value associated with the Nullable.
    /// If no value, throws.
    let get (x: Nullable<_>) = x.Value
    /// Converts option to nullable
    let ofOption = Option.toNullable
    /// Converts nullable to option
    let toOption = Option.ofNullable
    /// Monadic bind
    let bind f x =
        match x with
        | Null -> Nullable()
        | Value v -> f v
    /// True if Nullable has value
    let hasValue (x: _ Nullable) = x.HasValue
    /// True if Nullable does not have value
    let isNull (x: _ Nullable) = not x.HasValue
    /// Returns 1 if Nullable has value, otherwise 0
    let count (x: _ Nullable) = if x.HasValue then 1 else 0
    /// Evaluates the equivalent of List.fold for a nullable.
    let fold f state x =
        match x with
        | Null -> state
        | Value v -> f state v
    /// Performs the equivalent of the List.foldBack operation on a nullable.
    let foldBack f x state =
        match x with
        | Null -> state
        | Value v -> f x state
    /// Evaluates the equivalent of List.exists for a nullable.
    let exists p x =
        match x with
        | Null -> false
        | Value v -> p x
    /// Evaluates the equivalent of List.forall for a nullable.
    let forall p x = 
        match x with
        | Null -> true
        | Value v -> p x
    /// Executes a function for a nullable value.
    let iter f x =
        match x with
        | Null -> ()
        | Value v -> f v
    /// Transforms a Nullable value by using a specified mapping function.
    let map f x =
        match x with
        | Null -> Nullable()
        | Value v -> Nullable(f v)
    /// Convert the nullable to an array of length 0 or 1.
    let toArray x = 
        match x with
        | Null -> [||]
        | Value v -> [| v |]
    /// Convert the nullable to a list of length 0 or 1.
    let toList x =
        match x with
        | Null -> []
        | Value v -> [v]
        
    /// Promote a function to a monad/applicative, scanning the monadic/applicative arguments from left to right.
    let lift2 f (a: _ Nullable) (b: _ Nullable) =
        if a.HasValue && b.HasValue
            then Nullable(f a.Value b.Value)
            else Nullable()

    let mapBool op a b =
        match a,b with
        | Value x, Value y -> op x y
        | _ -> false

    let inline (+?) a b = (lift2 (+)) a b
    let inline (-?) a b = (lift2 (-)) a b
    let inline ( *?) a b = (lift2 ( *)) a b
    let inline (/?) a b = (lift2 (/)) a b
    let inline (>?) a b = (mapBool (>)) a b
    let inline (>=?) a b = a >? b || a = b
    let inline (<?) a b = (mapBool (<)) a b
    let inline (<=?) a b = a <? b || a = b
    let inline notn (a: bool Nullable) = 
        if a.HasValue 
            then Nullable(not a.Value) 
            else Nullable()
    let inline (&?) a b = 
        let rec and' a b = 
            match a,b with
            | Null, Value y when not y -> Nullable(false)
            | Null, Value y when y -> Nullable()
            | Null, Null -> Nullable()
            | Value x, Value y -> Nullable(x && y)
            | _ -> and' b a
        and' a b

    let inline (|?) a b = notn ((notn a) &? (notn b))

    type Int32 with
        member x.n = Nullable x

    type Double with
        member x.n = Nullable x

    type Single with
        member x.n = Nullable x

    type Byte with
        member x.n = Nullable x

    type Int64 with
        member x.n = Nullable x

    type Decimal with
        member x.n = Nullable x

