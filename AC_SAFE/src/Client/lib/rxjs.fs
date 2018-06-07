// ts2fable 0.6.1
module rec rxjs
open System
open Fable.Core
open Fable.Import.JS

type SubscriptionLike = __types.SubscriptionLike
type TeardownLogic = __types.TeardownLogic

type [<AllowNullLiteral>] IExports =
    abstract Subscription: SubscriptionStatic

/// Represents a disposable resource, such as the execution of an Observable. A
/// Subscription has one important method, `unsubscribe`, that takes no argument
/// and just disposes the resource held by the subscription.
/// 
/// Additionally, subscriptions may be grouped together through the `add()`
/// method, which will attach a child Subscription to the current Subscription.
/// When a Subscription is unsubscribed, all its children (and its grandchildren)
/// will be unsubscribed as well.
type [<AllowNullLiteral>] Subscription =
    inherit SubscriptionLike
    abstract EMPTY: Subscription with get, set
    /// A flag to indicate whether this Subscription has already been unsubscribed.
    abstract closed: bool with get, set
    abstract _parent: Subscription with get, set
    abstract _parents: ResizeArray<Subscription> with get, set
    abstract _subscriptions: obj with get, set
    /// Disposes the resources held by the subscription. May, for instance, cancel
    /// an ongoing Observable execution or cancel any other type of work that
    /// started when the Subscription was created.
    abstract unsubscribe: unit -> unit
    /// <summary>Adds a tear down to be called during the unsubscribe() of this
    /// Subscription.
    /// 
    /// If the tear down being added is a subscription that is already
    /// unsubscribed, is the same reference `add` is being called on, or is
    /// `Subscription.EMPTY`, it will not be added.
    /// 
    /// If this subscription is already in an `closed` state, the passed
    /// tear down logic will be executed immediately.</summary>
    /// <param name="teardown">The additional logic to execute on
    /// teardown.</param>
    abstract add: teardown: TeardownLogic -> Subscription
    /// <summary>Removes a Subscription from the internal list of subscriptions that will
    /// unsubscribe during the unsubscribe process of this Subscription.</summary>
    /// <param name="subscription">The subscription to remove.</param>
    abstract remove: subscription: Subscription -> unit
    abstract _addParent: parent: obj -> unit

/// Represents a disposable resource, such as the execution of an Observable. A
/// Subscription has one important method, `unsubscribe`, that takes no argument
/// and just disposes the resource held by the subscription.
/// 
/// Additionally, subscriptions may be grouped together through the `add()`
/// method, which will attach a child Subscription to the current Subscription.
/// When a Subscription is unsubscribed, all its children (and its grandchildren)
/// will be unsubscribed as well.
type [<AllowNullLiteral>] SubscriptionStatic =
    /// <param name="unsubscribe">A function describing how to
    /// perform the disposal of resources when the `unsubscribe` method is called.</param>
    [<Emit "new $0($1...)">] abstract Create: ?unsubscribe: (unit -> unit) -> Subscription
type Observable = __Observable.Observable
type Subscription = __Subscription.Subscription

/// OPERATOR INTERFACES 
type [<AllowNullLiteral>] UnaryFunction<'T, 'R> =
    [<Emit "$0($1...)">] abstract Invoke: source: 'T -> 'R

type [<AllowNullLiteral>] OperatorFunction<'T, 'R> =
    inherit UnaryFunction<Observable<'T>, Observable<'R>>

type FactoryOrValue<'T> =
    U2<'T, (unit -> 'T)>

[<RequireQualifiedAccess; CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module FactoryOrValue =
    let ofT v: FactoryOrValue<'T> = v |> U2.Case1
    let isT (v: FactoryOrValue<'T>) = match v with U2.Case1 _ -> true | _ -> false
    let asT (v: FactoryOrValue<'T>) = match v with U2.Case1 o -> Some o | _ -> None
    let ofCase2 v: FactoryOrValue<'T> = v |> U2.Case2
    let isCase2 (v: FactoryOrValue<'T>) = match v with U2.Case2 _ -> true | _ -> false
    let asCase2 (v: FactoryOrValue<'T>) = match v with U2.Case2 o -> Some o | _ -> None

type [<AllowNullLiteral>] MonoTypeOperatorFunction<'T> =
    inherit OperatorFunction<'T, 'T>

type [<AllowNullLiteral>] Timestamp<'T> =
    abstract value: 'T with get, set
    abstract timestamp: float with get, set

type [<AllowNullLiteral>] TimeInterval<'T> =
    abstract value: 'T with get, set
    abstract interval: float with get, set

/// SUBSCRIPTION INTERFACES 
type [<AllowNullLiteral>] Unsubscribable =
    abstract unsubscribe: unit -> unit

type TeardownLogic =
    U3<Unsubscribable, Function, unit>

[<RequireQualifiedAccess; CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module TeardownLogic =
    let ofUnsubscribable v: TeardownLogic = v |> U3.Case1
    let isUnsubscribable (v: TeardownLogic) = match v with U3.Case1 _ -> true | _ -> false
    let asUnsubscribable (v: TeardownLogic) = match v with U3.Case1 o -> Some o | _ -> None
    let ofFunction v: TeardownLogic = v |> U3.Case2
    let isFunction (v: TeardownLogic) = match v with U3.Case2 _ -> true | _ -> false
    let asFunction (v: TeardownLogic) = match v with U3.Case2 o -> Some o | _ -> None
    let ofUnit v: TeardownLogic = v |> U3.Case3
    let isUnit (v: TeardownLogic) = match v with U3.Case3 _ -> true | _ -> false
    let asUnit (v: TeardownLogic) = match v with U3.Case3 o -> Some o | _ -> None

type [<AllowNullLiteral>] SubscriptionLike =
    inherit Unsubscribable
    abstract unsubscribe: unit -> unit
    abstract closed: bool

type SubscribableOrPromise<'T> =
    U4<Subscribable<'T>, Subscribable<obj>, PromiseLike<'T>, InteropObservable<'T>>

[<RequireQualifiedAccess; CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module SubscribableOrPromise =
    let ofSubscribable v: SubscribableOrPromise<'T> = v |> U4.Case1
    let isSubscribable (v: SubscribableOrPromise<'T>) = match v with U4.Case1 _ -> true | _ -> false
    let asSubscribable (v: SubscribableOrPromise<'T>) = match v with U4.Case1 o -> Some o | _ -> None
    let ofSubscribable v: SubscribableOrPromise<'T> = v |> U4.Case2
    let isSubscribable (v: SubscribableOrPromise<'T>) = match v with U4.Case2 _ -> true | _ -> false
    let asSubscribable (v: SubscribableOrPromise<'T>) = match v with U4.Case2 o -> Some o | _ -> None
    let ofPromiseLike v: SubscribableOrPromise<'T> = v |> U4.Case3
    let isPromiseLike (v: SubscribableOrPromise<'T>) = match v with U4.Case3 _ -> true | _ -> false
    let asPromiseLike (v: SubscribableOrPromise<'T>) = match v with U4.Case3 o -> Some o | _ -> None
    let ofInteropObservable v: SubscribableOrPromise<'T> = v |> U4.Case4
    let isInteropObservable (v: SubscribableOrPromise<'T>) = match v with U4.Case4 _ -> true | _ -> false
    let asInteropObservable (v: SubscribableOrPromise<'T>) = match v with U4.Case4 o -> Some o | _ -> None

/// OBSERVABLE INTERFACES 
type [<AllowNullLiteral>] Subscribable<'T> =
    abstract subscribe: ?observerOrNext: U2<PartialObserver<'T>, ('T -> unit)> * ?error: (obj option -> unit) * ?complete: (unit -> unit) -> Unsubscribable

type ObservableInput<'T> =
    U3<SubscribableOrPromise<'T>, ArrayLike<'T>, Iterable<'T>>

[<RequireQualifiedAccess; CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module ObservableInput =
    let ofSubscribableOrPromise v: ObservableInput<'T> = v |> U3.Case1
    let isSubscribableOrPromise (v: ObservableInput<'T>) = match v with U3.Case1 _ -> true | _ -> false
    let asSubscribableOrPromise (v: ObservableInput<'T>) = match v with U3.Case1 o -> Some o | _ -> None
    let ofArrayLike v: ObservableInput<'T> = v |> U3.Case2
    let isArrayLike (v: ObservableInput<'T>) = match v with U3.Case2 _ -> true | _ -> false
    let asArrayLike (v: ObservableInput<'T>) = match v with U3.Case2 o -> Some o | _ -> None
    let ofIterable v: ObservableInput<'T> = v |> U3.Case3
    let isIterable (v: ObservableInput<'T>) = match v with U3.Case3 _ -> true | _ -> false
    let asIterable (v: ObservableInput<'T>) = match v with U3.Case3 o -> Some o | _ -> None

type ObservableLike<'T> =
    InteropObservable<'T>

type [<AllowNullLiteral>] InteropObservable<'T> =
    abstract ``[Symbol.observable]``: (unit -> Subscribable<'T>) with get, set

/// OBSERVER INTERFACES 
type [<AllowNullLiteral>] NextObserver<'T> =
    abstract closed: bool option with get, set
    abstract next: ('T -> unit) with get, set
    abstract error: (obj option -> unit) option with get, set
    abstract complete: (unit -> unit) option with get, set

type [<AllowNullLiteral>] ErrorObserver<'T> =
    abstract closed: bool option with get, set
    abstract next: ('T -> unit) option with get, set
    abstract error: (obj option -> unit) with get, set
    abstract complete: (unit -> unit) option with get, set

type [<AllowNullLiteral>] CompletionObserver<'T> =
    abstract closed: bool option with get, set
    abstract next: ('T -> unit) option with get, set
    abstract error: (obj option -> unit) option with get, set
    abstract complete: (unit -> unit) with get, set

type PartialObserver<'T> =
    U3<NextObserver<'T>, ErrorObserver<'T>, CompletionObserver<'T>>

[<RequireQualifiedAccess; CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module PartialObserver =
    let ofNextObserver v: PartialObserver<'T> = v |> U3.Case1
    let isNextObserver (v: PartialObserver<'T>) = match v with U3.Case1 _ -> true | _ -> false
    let asNextObserver (v: PartialObserver<'T>) = match v with U3.Case1 o -> Some o | _ -> None
    let ofErrorObserver v: PartialObserver<'T> = v |> U3.Case2
    let isErrorObserver (v: PartialObserver<'T>) = match v with U3.Case2 _ -> true | _ -> false
    let asErrorObserver (v: PartialObserver<'T>) = match v with U3.Case2 o -> Some o | _ -> None
    let ofCompletionObserver v: PartialObserver<'T> = v |> U3.Case3
    let isCompletionObserver (v: PartialObserver<'T>) = match v with U3.Case3 _ -> true | _ -> false
    let asCompletionObserver (v: PartialObserver<'T>) = match v with U3.Case3 o -> Some o | _ -> None

type [<AllowNullLiteral>] Observer<'T> =
    abstract closed: bool option with get, set
    abstract next: ('T -> unit) with get, set
    abstract error: (obj option -> unit) with get, set
    abstract complete: (unit -> unit) with get, set

/// SCHEDULER INTERFACES 
type [<AllowNullLiteral>] SchedulerLike =
    abstract now: unit -> float
    abstract schedule: work: (SchedulerAction<'T> -> 'T -> unit) * ?delay: float * ?state: 'T -> Subscription

type [<AllowNullLiteral>] SchedulerAction<'T> =
    inherit Subscription
    abstract schedule: ?state: 'T * ?delay: float -> Subscription
type Observer = __types.Observer
type PartialObserver = __types.PartialObserver
type Subscription = __Subscription.Subscription

type [<AllowNullLiteral>] IExports =
    abstract Subscriber: SubscriberStatic

/// Implements the {@link Observer} interface and extends the
/// {@link Subscription} class. While the {@link Observer} is the public API for
/// consuming the values of an {@link Observable}, all Observers get converted to
/// a Subscriber, in order to provide Subscription-like capabilities such as
/// `unsubscribe`. Subscriber is a common type in RxJS, and crucial for
/// implementing operators, but it is rarely used as a public API.
type [<AllowNullLiteral>] Subscriber<'T> =
    inherit Subscription
    inherit Observer<'T>
    abstract syncErrorValue: obj option with get, set
    abstract syncErrorThrown: bool with get, set
    abstract syncErrorThrowable: bool with get, set
    abstract isStopped: bool with get, set
    abstract destination: PartialObserver<obj option> with get, set
    /// <summary>The {@link Observer} callback to receive notifications of type `next` from
    /// the Observable, with a value. The Observable may call this method 0 or more
    /// times.</summary>
    /// <param name="value">The `next` value.</param>
    abstract next: ?value: 'T -> unit
    /// <summary>The {@link Observer} callback to receive notifications of type `error` from
    /// the Observable, with an attached {@link Error}. Notifies the Observer that
    /// the Observable has experienced an error condition.</summary>
    /// <param name="err">The `error` exception.</param>
    abstract error: ?err: obj option -> unit
    /// The {@link Observer} callback to receive a valueless notification of type
    /// `complete` from the Observable. Notifies the Observer that the Observable
    /// has finished sending push-based notifications.
    abstract complete: unit -> unit
    abstract unsubscribe: unit -> unit
    abstract _next: value: 'T -> unit
    abstract _error: err: obj option -> unit
    abstract _complete: unit -> unit
    abstract _unsubscribeAndRecycle: unit -> Subscriber<'T>

/// Implements the {@link Observer} interface and extends the
/// {@link Subscription} class. While the {@link Observer} is the public API for
/// consuming the values of an {@link Observable}, all Observers get converted to
/// a Subscriber, in order to provide Subscription-like capabilities such as
/// `unsubscribe`. Subscriber is a common type in RxJS, and crucial for
/// implementing operators, but it is rarely used as a public API.
type [<AllowNullLiteral>] SubscriberStatic =
    /// <summary>A static factory for a Subscriber, given a (potentially partial) definition
    /// of an Observer.</summary>
    /// <param name="next">The `next` callback of an Observer.</param>
    /// <param name="error">The `error` callback of an
    /// Observer.</param>
    /// <param name="complete">The `complete` callback of an
    /// Observer.</param>
    abstract create: ?next: ('T -> unit) * ?error: (obj option -> unit) * ?complete: (unit -> unit) -> Subscriber<'T>
    /// <param name="destinationOrNext">A partially
    /// defined Observer or a `next` callback function.</param>
    /// <param name="error">The `error` callback of an
    /// Observer.</param>
    /// <param name="complete">The `complete` callback of an
    /// Observer.</param>
    [<Emit "new $0($1...)">] abstract Create: ?destinationOrNext: U2<PartialObserver<obj option>, ('T -> unit)> * ?error: (obj option -> unit) * ?complete: (unit -> unit) -> Subscriber<'T>
type Subscriber = __Subscriber.Subscriber
type TeardownLogic = __types.TeardownLogic

type [<AllowNullLiteral>] Operator<'T, 'R> =
    abstract call: subscriber: Subscriber<'R> * source: obj option -> TeardownLogic
type Observable = ___Observable.Observable
type SubscribableOrPromise = ___types.SubscribableOrPromise

type [<AllowNullLiteral>] IExports =
    /// <summary>Decides at subscription time which Observable will actually be subscribed.
    /// 
    /// <span class="informal">`If` statement for Observables.</span>
    /// 
    /// `if` accepts a condition function and two Observables. When
    /// an Observable returned by the operator is subscribed, condition function will be called.
    /// Based on what boolean it returns at that moment, consumer will subscribe either to
    /// the first Observable (if condition was true) or to the second (if condition was false). Condition
    /// function may also not return anything - in that case condition will be evaluated as false and
    /// second Observable will be subscribed.
    /// 
    /// Note that Observables for both cases (true and false) are optional. If condition points to an Observable that
    /// was left undefined, resulting stream will simply complete immediately. That allows you to, rather
    /// then controlling which Observable will be subscribed, decide at runtime if consumer should have access
    /// to given Observable or not.
    /// 
    /// If you have more complex logic that requires decision between more than two Observables, {@link defer}
    /// will probably be a better choice. Actually `if` can be easily implemented with {@link defer}
    /// and exists only for convenience and readability reasons.</summary>
    /// <param name="condition">Condition which Observable should be chosen.</param>
    abstract iif: condition: (unit -> bool) * ?trueResult: SubscribableOrPromise<'T> * ?falseResult: SubscribableOrPromise<'F> -> Observable<U2<'T, 'F>>
type Observable = ___Observable.Observable
type SchedulerLike = ___types.SchedulerLike

type [<AllowNullLiteral>] IExports =
    /// <summary>Creates an Observable that emits no items to the Observer and immediately
    /// emits an error notification.
    /// 
    /// <span class="informal">Just emits 'error', and nothing else.
    /// </span>
    /// 
    /// <img src="./img/throw.png" width="100%">
    /// 
    /// This static operator is useful for creating a simple Observable that only
    /// emits the error notification. It can be used for composing with other
    /// Observables, such as in a {@link mergeMap}.</summary>
    /// <param name="error">The particular Error to pass to the error notification.</param>
    /// <param name="scheduler">A {</param>
    abstract throwError: error: obj option * ?scheduler: SchedulerLike -> Observable<obj>
type Operator = __Operator.Operator
type Subscriber = __Subscriber.Subscriber
type Subscription = __Subscription.Subscription
type TeardownLogic = __types.TeardownLogic
type OperatorFunction = __types.OperatorFunction
type PartialObserver = __types.PartialObserver
type Subscribable = __types.Subscribable
type iif = __observable_iif.iif
type throwError = __observable_throwError.throwError

type [<AllowNullLiteral>] IExports =
    abstract Observable: ObservableStatic

/// A representation of any set of values over any amount of time. This is the most basic building block
/// of RxJS.
type [<AllowNullLiteral>] Observable<'T> =
    inherit Subscribable<'T>
    /// Internal implementation detail, do not use directly. 
    abstract _isScalar: bool with get, set
    abstract source: Observable<obj option> with get, set
    abstract operator: Operator<obj option, 'T> with get, set
    /// Creates a new cold Observable by calling the Observable constructor
    abstract create: Function with get, set
    /// <summary>Creates a new Observable, with this Observable as the source, and the passed
    /// operator defined as the new observable's operator.</summary>
    /// <param name="operator">the operator defining the operation to take on the observable</param>
    abstract lift: operator: Operator<'T, 'R> -> Observable<'R>
    abstract subscribe: ?observer: PartialObserver<'T> -> Subscription
    abstract subscribe: ?next: ('T -> unit) * ?error: (obj option -> unit) * ?complete: (unit -> unit) -> Subscription
    abstract _trySubscribe: sink: Subscriber<'T> -> TeardownLogic
    /// <param name="next">a handler for each value emitted by the observable</param>
    /// <param name="promiseCtor">a constructor function used to instantiate the Promise</param>
    abstract forEach: next: ('T -> unit) * ?promiseCtor: PromiseConstructorLike -> Promise<unit>
    abstract _subscribe: subscriber: Subscriber<obj option> -> TeardownLogic
    abstract ``if``: obj with get, set
    abstract throw: obj with get, set
    abstract pipe: unit -> Observable<'T>
    abstract pipe: op1: OperatorFunction<'T, 'A> -> Observable<'A>
    abstract pipe: op1: OperatorFunction<'T, 'A> * op2: OperatorFunction<'A, 'B> -> Observable<'B>
    abstract pipe: op1: OperatorFunction<'T, 'A> * op2: OperatorFunction<'A, 'B> * op3: OperatorFunction<'B, 'C> -> Observable<'C>
    abstract pipe: op1: OperatorFunction<'T, 'A> * op2: OperatorFunction<'A, 'B> * op3: OperatorFunction<'B, 'C> * op4: OperatorFunction<'C, 'D> -> Observable<'D>
    abstract pipe: op1: OperatorFunction<'T, 'A> * op2: OperatorFunction<'A, 'B> * op3: OperatorFunction<'B, 'C> * op4: OperatorFunction<'C, 'D> * op5: OperatorFunction<'D, 'E> -> Observable<'E>
    abstract pipe: op1: OperatorFunction<'T, 'A> * op2: OperatorFunction<'A, 'B> * op3: OperatorFunction<'B, 'C> * op4: OperatorFunction<'C, 'D> * op5: OperatorFunction<'D, 'E> * op6: OperatorFunction<'E, 'F> -> Observable<'F>
    abstract pipe: op1: OperatorFunction<'T, 'A> * op2: OperatorFunction<'A, 'B> * op3: OperatorFunction<'B, 'C> * op4: OperatorFunction<'C, 'D> * op5: OperatorFunction<'D, 'E> * op6: OperatorFunction<'E, 'F> * op7: OperatorFunction<'F, 'G> -> Observable<'G>
    abstract pipe: op1: OperatorFunction<'T, 'A> * op2: OperatorFunction<'A, 'B> * op3: OperatorFunction<'B, 'C> * op4: OperatorFunction<'C, 'D> * op5: OperatorFunction<'D, 'E> * op6: OperatorFunction<'E, 'F> * op7: OperatorFunction<'F, 'G> * op8: OperatorFunction<'G, 'H> -> Observable<'H>
    abstract pipe: op1: OperatorFunction<'T, 'A> * op2: OperatorFunction<'A, 'B> * op3: OperatorFunction<'B, 'C> * op4: OperatorFunction<'C, 'D> * op5: OperatorFunction<'D, 'E> * op6: OperatorFunction<'E, 'F> * op7: OperatorFunction<'F, 'G> * op8: OperatorFunction<'G, 'H> * op9: OperatorFunction<'H, 'I> -> Observable<'I>
    abstract pipe: [<ParamArray>] operations: ResizeArray<OperatorFunction<'T, 'R>> -> Observable<'R>
    abstract toPromise: this: Observable<'T> -> Promise<'T>
    abstract toPromise: this: Observable<'T> * PromiseCtor: obj -> Promise<'T>
    abstract toPromise: this: Observable<'T> * PromiseCtor: PromiseConstructorLike -> Promise<'T>

/// A representation of any set of values over any amount of time. This is the most basic building block
/// of RxJS.
type [<AllowNullLiteral>] ObservableStatic =
    /// <param name="subscribe">the function that is called when the Observable is
    /// initially subscribed to. This function is given a Subscriber, to which new values
    /// can be `next`ed, or an `error` method can be called to raise an error, or
    /// `complete` can be called to notify of a successful completion.</param>
    [<Emit "new $0($1...)">] abstract Create: ?subscribe: (Observable<'T> -> Subscriber<'T> -> TeardownLogic) -> Observable<'T>
type Operator = __Operator.Operator
type Observable = __Observable.Observable
type Subscriber = __Subscriber.Subscriber
type Subscription = __Subscription.Subscription
type Observer = __types.Observer
type SubscriptionLike = __types.SubscriptionLike
type TeardownLogic = __types.TeardownLogic

type [<AllowNullLiteral>] IExports =
    abstract SubjectSubscriber: SubjectSubscriberStatic
    abstract Subject: SubjectStatic
    abstract AnonymousSubject: AnonymousSubjectStatic

type [<AllowNullLiteral>] SubjectSubscriber<'T> =
    inherit Subscriber<'T>
    abstract destination: Subject<'T> with get, set

type [<AllowNullLiteral>] SubjectSubscriberStatic =
    [<Emit "new $0($1...)">] abstract Create: destination: Subject<'T> -> SubjectSubscriber<'T>

type [<AllowNullLiteral>] Subject<'T> =
    inherit Observable<'T>
    inherit SubscriptionLike
    abstract observers: ResizeArray<Observer<'T>> with get, set
    abstract closed: bool with get, set
    abstract isStopped: bool with get, set
    abstract hasError: bool with get, set
    abstract thrownError: obj option with get, set
    abstract create: Function with get, set
    abstract lift: operator: Operator<'T, 'R> -> Observable<'R>
    abstract next: ?value: 'T -> unit
    abstract error: err: obj option -> unit
    abstract complete: unit -> unit
    abstract unsubscribe: unit -> unit
    abstract _trySubscribe: subscriber: Subscriber<'T> -> TeardownLogic
    abstract _subscribe: subscriber: Subscriber<'T> -> Subscription
    abstract asObservable: unit -> Observable<'T>

type [<AllowNullLiteral>] SubjectStatic =
    [<Emit "new $0($1...)">] abstract Create: unit -> Subject<'T>

type [<AllowNullLiteral>] AnonymousSubject<'T> =
    inherit Subject<'T>
    abstract destination: Observer<'T> with get, set
    abstract next: value: 'T -> unit
    abstract error: err: obj option -> unit
    abstract complete: unit -> unit
    abstract _subscribe: subscriber: Subscriber<'T> -> Subscription

type [<AllowNullLiteral>] AnonymousSubjectStatic =
    [<Emit "new $0($1...)">] abstract Create: ?destination: Observer<'T> * ?source: Observable<'T> -> AnonymousSubject<'T>
type Subject = ___Subject.Subject
type Observable = ___Observable.Observable
type Subscriber = ___Subscriber.Subscriber
type Subscription = ___Subscription.Subscription
let [<Import("*","rxjs")>] connectableObservableDescriptor: PropertyDescriptorMap = jsNative

type [<AllowNullLiteral>] IExports =
    abstract ConnectableObservable: ConnectableObservableStatic

type [<AllowNullLiteral>] ConnectableObservable<'T> =
    inherit Observable<'T>
    abstract source: Observable<'T> with get, set
    abstract subjectFactory: (unit -> Subject<'T>) with get, set
    abstract _subject: Subject<'T> with get, set
    abstract _refCount: float with get, set
    abstract _connection: Subscription with get, set
    abstract _isComplete: bool with get, set
    abstract _subscribe: subscriber: Subscriber<'T> -> Subscription
    abstract getSubject: unit -> Subject<'T>
    abstract connect: unit -> Subscription
    abstract refCount: unit -> Observable<'T>

type [<AllowNullLiteral>] ConnectableObservableStatic =
    [<Emit "new $0($1...)">] abstract Create: source: Observable<'T> * subjectFactory: (unit -> Subject<'T>) -> ConnectableObservable<'T>
type Subscriber = ___Subscriber.Subscriber
type Subscription = ___Subscription.Subscription
type Observable = ___Observable.Observable
type Subject = ___Subject.Subject
type OperatorFunction = ___types.OperatorFunction

type [<AllowNullLiteral>] IExports =
    abstract groupBy: keySelector: ('T -> 'K) -> OperatorFunction<'T, GroupedObservable<'K, 'T>>
    abstract groupBy: keySelector: ('T -> 'K) * elementSelector: unit * durationSelector: (GroupedObservable<'K, 'T> -> Observable<obj option>) -> OperatorFunction<'T, GroupedObservable<'K, 'T>>
    abstract groupBy: keySelector: ('T -> 'K) * ?elementSelector: ('T -> 'R) * ?durationSelector: (GroupedObservable<'K, 'R> -> Observable<obj option>) -> OperatorFunction<'T, GroupedObservable<'K, 'R>>
    abstract groupBy: keySelector: ('T -> 'K) * ?elementSelector: ('T -> 'R) * ?durationSelector: (GroupedObservable<'K, 'R> -> Observable<obj option>) * ?subjectSelector: (unit -> Subject<'R>) -> OperatorFunction<'T, GroupedObservable<'K, 'R>>
    abstract GroupedObservable: GroupedObservableStatic

type [<AllowNullLiteral>] RefCountSubscription =
    abstract count: float with get, set
    abstract unsubscribe: (unit -> unit) with get, set
    abstract closed: bool with get, set
    abstract attemptedToUnsubscribe: bool with get, set

/// An Observable representing values belonging to the same group represented by
/// a common key. The values emitted by a GroupedObservable come from the source
/// Observable. The common key is available as the field `key` on a
/// GroupedObservable instance.
type [<AllowNullLiteral>] GroupedObservable<'K, 'T> =
    inherit Observable<'T>
    abstract key: 'K with get, set
    abstract groupSubject: obj with get, set
    abstract refCountSubscription: obj with get, set
    abstract _subscribe: subscriber: Subscriber<'T> -> Subscription

/// An Observable representing values belonging to the same group represented by
/// a common key. The values emitted by a GroupedObservable come from the source
/// Observable. The common key is available as the field `key` on a
/// GroupedObservable instance.
type [<AllowNullLiteral>] GroupedObservableStatic =
    [<Emit "new $0($1...)">] abstract Create: key: 'K * groupSubject: Subject<'T> * ?refCountSubscription: RefCountSubscription -> GroupedObservable<'K, 'T>
let [<Import("*","rxjs")>] observable: U2<string, Symbol> = jsNative

type [<AllowNullLiteral>] SymbolConstructor =
    abstract observable: Symbol with get, set
type Subject = __Subject.Subject
type Subscriber = __Subscriber.Subscriber
type Subscription = __Subscription.Subscription

type [<AllowNullLiteral>] IExports =
    abstract BehaviorSubject: BehaviorSubjectStatic

type [<AllowNullLiteral>] BehaviorSubject<'T> =
    inherit Subject<'T>
    abstract _value: obj with get, set
    abstract value: 'T
    abstract _subscribe: subscriber: Subscriber<'T> -> Subscription
    abstract getValue: unit -> 'T
    abstract next: value: 'T -> unit

type [<AllowNullLiteral>] BehaviorSubjectStatic =
    [<Emit "new $0($1...)">] abstract Create: _value: 'T -> BehaviorSubject<'T>
type Subject = __Subject.Subject
type SchedulerLike = __types.SchedulerLike
type Subscriber = __Subscriber.Subscriber
type Subscription = __Subscription.Subscription

type [<AllowNullLiteral>] IExports =
    abstract ReplaySubject: ReplaySubjectStatic

type [<AllowNullLiteral>] ReplaySubject<'T> =
    inherit Subject<'T>
    abstract scheduler: obj with get, set
    abstract _events: obj with get, set
    abstract _bufferSize: obj with get, set
    abstract _windowTime: obj with get, set
    abstract _infiniteTimeWindow: obj with get, set
    abstract nextInfiniteTimeWindow: value: obj -> unit
    abstract nextTimeWindow: value: obj -> unit
    abstract _subscribe: subscriber: Subscriber<'T> -> Subscription
    abstract _getNow: unit -> float
    abstract _trimBufferThenGetEvents: unit -> unit

type [<AllowNullLiteral>] ReplaySubjectStatic =
    [<Emit "new $0($1...)">] abstract Create: ?bufferSize: float * ?windowTime: float * ?scheduler: SchedulerLike -> ReplaySubject<'T>
type Subject = __Subject.Subject
type Subscriber = __Subscriber.Subscriber
type Subscription = __Subscription.Subscription

type [<AllowNullLiteral>] IExports =
    abstract AsyncSubject: AsyncSubjectStatic

type [<AllowNullLiteral>] AsyncSubject<'T> =
    inherit Subject<'T>
    abstract value: obj with get, set
    abstract hasNext: obj with get, set
    abstract hasCompleted: obj with get, set
    abstract _subscribe: subscriber: Subscriber<obj option> -> Subscription
    abstract next: value: 'T -> unit
    abstract error: error: obj option -> unit
    abstract complete: unit -> unit

type [<AllowNullLiteral>] AsyncSubjectStatic =
    [<Emit "new $0($1...)">] abstract Create: unit -> AsyncSubject<'T>
type Action = __scheduler_Action.Action
type Subscription = __Subscription.Subscription
type SchedulerLike = __types.SchedulerLike
type SchedulerAction = __types.SchedulerAction

type [<AllowNullLiteral>] IExports =
    abstract Scheduler: SchedulerStatic

/// An execution context and a data structure to order tasks and schedule their
/// execution. Provides a notion of (potentially virtual) time, through the
/// `now()` getter method.
/// 
/// Each unit of work in a Scheduler is called an {@link Action}.
/// 
/// ```ts
/// class Scheduler {
///    now(): number;
///    schedule(work, delay?, state?): Subscription;
/// }
/// ```
type [<AllowNullLiteral>] Scheduler =
    inherit SchedulerLike
    abstract SchedulerAction: obj with get, set
    abstract now: (unit -> float) with get, set
    /// A getter method that returns a number representing the current time
    /// (at the time this function was called) according to the scheduler's own
    /// internal clock.
    abstract now: (unit -> float) with get, set
    /// <summary>Schedules a function, `work`, for execution. May happen at some point in
    /// the future, according to the `delay` parameter, if specified. May be passed
    /// some context object, `state`, which will be passed to the `work` function.
    /// 
    /// The given arguments will be processed an stored as an Action object in a
    /// queue of actions.</summary>
    /// <param name="work">A function representing a
    /// task, or some unit of work to be executed by the Scheduler.</param>
    /// <param name="delay">Time to wait before executing the work, where the
    /// time unit is implicit and defined by the Scheduler itself.</param>
    /// <param name="state">Some contextual data that the `work` function uses when
    /// called by the Scheduler.</param>
    abstract schedule: work: (SchedulerAction<'T> -> 'T -> unit) * ?delay: float * ?state: 'T -> Subscription

/// An execution context and a data structure to order tasks and schedule their
/// execution. Provides a notion of (potentially virtual) time, through the
/// `now()` getter method.
/// 
/// Each unit of work in a Scheduler is called an {@link Action}.
/// 
/// ```ts
/// class Scheduler {
///    now(): number;
///    schedule(work, delay?, state?): Subscription;
/// }
/// ```
type [<AllowNullLiteral>] SchedulerStatic =
    [<Emit "new $0($1...)">] abstract Create: SchedulerAction: obj * ?now: (unit -> float) -> Scheduler
type Scheduler = ___Scheduler.Scheduler
type Subscription = ___Subscription.Subscription
type SchedulerAction = ___types.SchedulerAction

type [<AllowNullLiteral>] IExports =
    abstract Action: ActionStatic

/// A unit of work to be executed in a {@link Scheduler}. An action is typically
/// created from within a Scheduler and an RxJS user does not need to concern
/// themselves about creating and manipulating an Action.
/// 
/// ```ts
/// class Action<T> extends Subscription {
///    new (scheduler: Scheduler, work: (state?: T) => void);
///    schedule(state?: T, delay: number = 0): Subscription;
/// }
/// ```
type [<AllowNullLiteral>] Action<'T> =
    inherit Subscription
    /// <summary>Schedules this action on its parent Scheduler for execution. May be passed
    /// some context object, `state`. May happen at some point in the future,
    /// according to the `delay` parameter, if specified.</summary>
    /// <param name="state">Some contextual data that the `work` function uses when
    /// called by the Scheduler.</param>
    /// <param name="delay">Time to wait before executing the work, where the
    /// time unit is implicit and defined by the Scheduler.</param>
    abstract schedule: ?state: 'T * ?delay: float -> Subscription

/// A unit of work to be executed in a {@link Scheduler}. An action is typically
/// created from within a Scheduler and an RxJS user does not need to concern
/// themselves about creating and manipulating an Action.
/// 
/// ```ts
/// class Action<T> extends Subscription {
///    new (scheduler: Scheduler, work: (state?: T) => void);
///    schedule(state?: T, delay: number = 0): Subscription;
/// }
/// ```
type [<AllowNullLiteral>] ActionStatic =
    [<Emit "new $0($1...)">] abstract Create: scheduler: Scheduler * work: (SchedulerAction<'T> -> 'T -> unit) -> Action<'T>
type Scheduler = ___Scheduler.Scheduler
type Action = __Action.Action
type AsyncAction = __AsyncAction.AsyncAction
type SchedulerAction = ___types.SchedulerAction
type Subscription = ___Subscription.Subscription

type [<AllowNullLiteral>] IExports =
    abstract AsyncScheduler: AsyncSchedulerStatic

type [<AllowNullLiteral>] AsyncScheduler =
    inherit Scheduler
    abstract ``delegate``: Scheduler option with get, set
    abstract actions: Array<AsyncAction<obj option>> with get, set
    /// A flag to indicate whether the Scheduler is currently executing a batch of
    /// queued actions.
    abstract active: bool with get, set
    /// An internal ID used to track the latest asynchronous task such as those
    /// coming from `setTimeout`, `setInterval`, `requestAnimationFrame`, and
    /// others.
    abstract scheduled: obj option with get, set
    abstract schedule: work: (SchedulerAction<'T> -> 'T -> unit) * ?delay: float * ?state: 'T -> Subscription
    abstract flush: action: AsyncAction<obj option> -> unit

type [<AllowNullLiteral>] AsyncSchedulerStatic =
    [<Emit "new $0($1...)">] abstract Create: SchedulerAction: obj * ?now: (unit -> float) -> AsyncScheduler
type Action = __Action.Action
type SchedulerAction = ___types.SchedulerAction
type Subscription = ___Subscription.Subscription
type AsyncScheduler = __AsyncScheduler.AsyncScheduler

type [<AllowNullLiteral>] IExports =
    abstract AsyncAction: AsyncActionStatic

/// We need this JSDoc comment for affecting ESDoc.
type [<AllowNullLiteral>] AsyncAction<'T> =
    inherit Action<'T>
    abstract scheduler: AsyncScheduler with get, set
    abstract work: (SchedulerAction<'T> -> 'T -> unit) with get, set
    abstract id: obj option with get, set
    abstract state: 'T with get, set
    abstract delay: float with get, set
    abstract pending: bool with get, set
    abstract schedule: ?state: 'T * ?delay: float -> Subscription
    abstract requestAsyncId: scheduler: AsyncScheduler * ?id: obj option * ?delay: float -> obj option
    abstract recycleAsyncId: scheduler: AsyncScheduler * id: obj option * ?delay: float -> obj option
    /// Immediately executes this action and the `work` it contains.
    abstract execute: state: 'T * delay: float -> obj option
    abstract _execute: state: 'T * delay: float -> obj option
    abstract _unsubscribe: unit -> unit

/// We need this JSDoc comment for affecting ESDoc.
type [<AllowNullLiteral>] AsyncActionStatic =
    [<Emit "new $0($1...)">] abstract Create: scheduler: AsyncScheduler * work: (SchedulerAction<'T> -> 'T -> unit) -> AsyncAction<'T>
type AsyncAction = __AsyncAction.AsyncAction
type AsyncScheduler = __AsyncScheduler.AsyncScheduler

type [<AllowNullLiteral>] IExports =
    abstract AsapScheduler: AsapSchedulerStatic

type [<AllowNullLiteral>] AsapScheduler =
    inherit AsyncScheduler
    abstract flush: ?action: AsyncAction<obj option> -> unit

type [<AllowNullLiteral>] AsapSchedulerStatic =
    [<Emit "new $0($1...)">] abstract Create: unit -> AsapScheduler
type AsyncScheduler = __AsyncScheduler.AsyncScheduler

type [<AllowNullLiteral>] IExports =
    abstract QueueScheduler: QueueSchedulerStatic

type [<AllowNullLiteral>] QueueScheduler =
    inherit AsyncScheduler

type [<AllowNullLiteral>] QueueSchedulerStatic =
    [<Emit "new $0($1...)">] abstract Create: unit -> QueueScheduler
type AsyncAction = __AsyncAction.AsyncAction
type AsyncScheduler = __AsyncScheduler.AsyncScheduler

type [<AllowNullLiteral>] IExports =
    abstract AnimationFrameScheduler: AnimationFrameSchedulerStatic

type [<AllowNullLiteral>] AnimationFrameScheduler =
    inherit AsyncScheduler
    abstract flush: ?action: AsyncAction<obj option> -> unit

type [<AllowNullLiteral>] AnimationFrameSchedulerStatic =
    [<Emit "new $0($1...)">] abstract Create: unit -> AnimationFrameScheduler
type AsyncAction = __AsyncAction.AsyncAction
type Subscription = ___Subscription.Subscription
type AsyncScheduler = __AsyncScheduler.AsyncScheduler
type SchedulerAction = ___types.SchedulerAction

type [<AllowNullLiteral>] IExports =
    abstract VirtualTimeScheduler: VirtualTimeSchedulerStatic
    abstract VirtualAction: VirtualActionStatic

type [<AllowNullLiteral>] VirtualTimeScheduler =
    inherit AsyncScheduler
    abstract maxFrames: float with get, set
    abstract frameTimeFactor: float with get, set
    abstract frame: float with get, set
    abstract index: float with get, set
    /// Prompt the Scheduler to execute all of its queued actions, therefore
    /// clearing its queue.
    abstract flush: unit -> unit

type [<AllowNullLiteral>] VirtualTimeSchedulerStatic =
    [<Emit "new $0($1...)">] abstract Create: ?SchedulerAction: obj * ?maxFrames: float -> VirtualTimeScheduler

/// We need this JSDoc comment for affecting ESDoc.
type [<AllowNullLiteral>] VirtualAction<'T> =
    inherit AsyncAction<'T>
    abstract scheduler: VirtualTimeScheduler with get, set
    abstract work: (SchedulerAction<'T> -> 'T -> unit) with get, set
    abstract index: float with get, set
    abstract active: bool with get, set
    abstract schedule: ?state: 'T * ?delay: float -> Subscription
    abstract requestAsyncId: scheduler: VirtualTimeScheduler * ?id: obj option * ?delay: float -> obj option
    abstract recycleAsyncId: scheduler: VirtualTimeScheduler * ?id: obj option * ?delay: float -> obj option
    abstract _execute: state: 'T * delay: float -> obj option

/// We need this JSDoc comment for affecting ESDoc.
type [<AllowNullLiteral>] VirtualActionStatic =
    [<Emit "new $0($1...)">] abstract Create: scheduler: VirtualTimeScheduler * work: (SchedulerAction<'T> -> 'T -> unit) * ?index: float -> VirtualAction<'T>
    abstract sortActions: a: VirtualAction<'T> * b: VirtualAction<'T> -> obj
type PartialObserver = __types.PartialObserver
type Observable = __Observable.Observable

type [<AllowNullLiteral>] IExports =
    abstract Notification: NotificationStatic

/// Represents a push-based event or value that an {@link Observable} can emit.
/// This class is particularly useful for operators that manage notifications,
/// like {@link materialize}, {@link dematerialize}, {@link observeOn}, and
/// others. Besides wrapping the actual delivered value, it also annotates it
/// with metadata of, for instance, what type of push message it is (`next`,
/// `error`, or `complete`).
type [<AllowNullLiteral>] Notification<'T> =
    abstract kind: string with get, set
    abstract value: 'T with get, set
    abstract error: obj option with get, set
    abstract hasValue: bool with get, set
    /// <summary>Delivers to the given `observer` the value wrapped by this Notification.</summary>
    /// <param name="observer"></param>
    abstract observe: observer: PartialObserver<'T> -> obj option
    /// <summary>Given some {@link Observer} callbacks, deliver the value represented by the
    /// current Notification to the correctly corresponding callback.</summary>
    /// <param name="next">An Observer `next` callback.</param>
    /// <param name="error">An Observer `error` callback.</param>
    /// <param name="complete">An Observer `complete` callback.</param>
    abstract ``do``: next: ('T -> unit) * ?error: (obj option -> unit) * ?complete: (unit -> unit) -> obj option
    /// <summary>Takes an Observer or its individual callback functions, and calls `observe`
    /// or `do` methods accordingly.</summary>
    /// <param name="nextOrObserver">An Observer or
    /// the `next` callback.</param>
    /// <param name="error">An Observer `error` callback.</param>
    /// <param name="complete">An Observer `complete` callback.</param>
    abstract accept: nextOrObserver: U2<PartialObserver<'T>, ('T -> unit)> * ?error: (obj option -> unit) * ?complete: (unit -> unit) -> obj option
    /// Returns a simple Observable that just delivers the notification represented
    /// by this Notification instance.
    abstract toObservable: unit -> Observable<'T>
    abstract completeNotification: obj with get, set
    abstract undefinedValueNotification: obj with get, set

/// Represents a push-based event or value that an {@link Observable} can emit.
/// This class is particularly useful for operators that manage notifications,
/// like {@link materialize}, {@link dematerialize}, {@link observeOn}, and
/// others. Besides wrapping the actual delivered value, it also annotates it
/// with metadata of, for instance, what type of push message it is (`next`,
/// `error`, or `complete`).
type [<AllowNullLiteral>] NotificationStatic =
    [<Emit "new $0($1...)">] abstract Create: kind: string * ?value: 'T * ?error: obj option -> Notification<'T>
    /// <summary>A shortcut to create a Notification instance of the type `next` from a
    /// given value.</summary>
    /// <param name="value">The `next` value.</param>
    abstract createNext: value: 'T -> Notification<'T>
    /// <summary>A shortcut to create a Notification instance of the type `error` from a
    /// given error.</summary>
    /// <param name="err">The `error` error.</param>
    abstract createError: ?err: obj option -> Notification<'T>
    /// A shortcut to create a Notification instance of the type `complete`.
    abstract createComplete: unit -> Notification<obj option>
type UnaryFunction = ___types.UnaryFunction

type [<AllowNullLiteral>] IExports =
    abstract pipe: unit -> UnaryFunction<'T, 'T>
    abstract pipe: op1: UnaryFunction<'T, 'A> -> UnaryFunction<'T, 'A>
    abstract pipe: op1: UnaryFunction<'T, 'A> * op2: UnaryFunction<'A, 'B> -> UnaryFunction<'T, 'B>
    abstract pipe: op1: UnaryFunction<'T, 'A> * op2: UnaryFunction<'A, 'B> * op3: UnaryFunction<'B, 'C> -> UnaryFunction<'T, 'C>
    abstract pipe: op1: UnaryFunction<'T, 'A> * op2: UnaryFunction<'A, 'B> * op3: UnaryFunction<'B, 'C> * op4: UnaryFunction<'C, 'D> -> UnaryFunction<'T, 'D>
    abstract pipe: op1: UnaryFunction<'T, 'A> * op2: UnaryFunction<'A, 'B> * op3: UnaryFunction<'B, 'C> * op4: UnaryFunction<'C, 'D> * op5: UnaryFunction<'D, 'E> -> UnaryFunction<'T, 'E>
    abstract pipe: op1: UnaryFunction<'T, 'A> * op2: UnaryFunction<'A, 'B> * op3: UnaryFunction<'B, 'C> * op4: UnaryFunction<'C, 'D> * op5: UnaryFunction<'D, 'E> * op6: UnaryFunction<'E, 'F> -> UnaryFunction<'T, 'F>
    abstract pipe: op1: UnaryFunction<'T, 'A> * op2: UnaryFunction<'A, 'B> * op3: UnaryFunction<'B, 'C> * op4: UnaryFunction<'C, 'D> * op5: UnaryFunction<'D, 'E> * op6: UnaryFunction<'E, 'F> * op7: UnaryFunction<'F, 'G> -> UnaryFunction<'T, 'G>
    abstract pipe: op1: UnaryFunction<'T, 'A> * op2: UnaryFunction<'A, 'B> * op3: UnaryFunction<'B, 'C> * op4: UnaryFunction<'C, 'D> * op5: UnaryFunction<'D, 'E> * op6: UnaryFunction<'E, 'F> * op7: UnaryFunction<'F, 'G> * op8: UnaryFunction<'G, 'H> -> UnaryFunction<'T, 'H>
    abstract pipe: op1: UnaryFunction<'T, 'A> * op2: UnaryFunction<'A, 'B> * op3: UnaryFunction<'B, 'C> * op4: UnaryFunction<'C, 'D> * op5: UnaryFunction<'D, 'E> * op6: UnaryFunction<'E, 'F> * op7: UnaryFunction<'F, 'G> * op8: UnaryFunction<'G, 'H> * op9: UnaryFunction<'H, 'I> -> UnaryFunction<'T, 'I>
    abstract pipeFromArray: fns: Array<UnaryFunction<'T, 'R>> -> UnaryFunction<'T, 'R>

type [<AllowNullLiteral>] IExports =
    abstract noop: unit -> unit

type [<AllowNullLiteral>] IExports =
    abstract identity: x: 'T -> 'T
type Observable = ___Observable.Observable

type [<AllowNullLiteral>] IExports =
    /// <summary>Tests to see if the object is an RxJS {@link Observable}</summary>
    /// <param name="obj">the object to test</param>
    abstract isObservable: obj: obj option -> bool

type [<AllowNullLiteral>] IExports =
    abstract ArgumentOutOfRangeError: ArgumentOutOfRangeErrorStatic

/// An error thrown when an element was queried at a certain index of an
/// Observable, but no such index or position exists in that sequence.
type [<AllowNullLiteral>] ArgumentOutOfRangeError =
    inherit Error
    abstract name: string

/// An error thrown when an element was queried at a certain index of an
/// Observable, but no such index or position exists in that sequence.
type [<AllowNullLiteral>] ArgumentOutOfRangeErrorStatic =
    [<Emit "new $0($1...)">] abstract Create: unit -> ArgumentOutOfRangeError

type [<AllowNullLiteral>] IExports =
    abstract EmptyError: EmptyErrorStatic

/// An error thrown when an Observable or a sequence was queried but has no
/// elements.
type [<AllowNullLiteral>] EmptyError =
    inherit Error
    abstract name: string

/// An error thrown when an Observable or a sequence was queried but has no
/// elements.
type [<AllowNullLiteral>] EmptyErrorStatic =
    [<Emit "new $0($1...)">] abstract Create: unit -> EmptyError

type [<AllowNullLiteral>] IExports =
    abstract ObjectUnsubscribedError: ObjectUnsubscribedErrorStatic

/// An error thrown when an action is invalid because the object has been
/// unsubscribed.
type [<AllowNullLiteral>] ObjectUnsubscribedError =
    inherit Error
    abstract name: string

/// An error thrown when an action is invalid because the object has been
/// unsubscribed.
type [<AllowNullLiteral>] ObjectUnsubscribedErrorStatic =
    [<Emit "new $0($1...)">] abstract Create: unit -> ObjectUnsubscribedError

type [<AllowNullLiteral>] IExports =
    abstract UnsubscriptionError: UnsubscriptionErrorStatic

/// An error thrown when one or more errors have occurred during the
/// `unsubscribe` of a {@link Subscription}.
type [<AllowNullLiteral>] UnsubscriptionError =
    inherit Error
    abstract errors: ResizeArray<obj option> with get, set
    abstract name: string

/// An error thrown when one or more errors have occurred during the
/// `unsubscribe` of a {@link Subscription}.
type [<AllowNullLiteral>] UnsubscriptionErrorStatic =
    [<Emit "new $0($1...)">] abstract Create: errors: ResizeArray<obj option> -> UnsubscriptionError

type [<AllowNullLiteral>] IExports =
    abstract TimeoutError: TimeoutErrorStatic

/// An error thrown when duetime elapses.
type [<AllowNullLiteral>] TimeoutError =
    inherit Error
    abstract name: string

/// An error thrown when duetime elapses.
type [<AllowNullLiteral>] TimeoutErrorStatic =
    [<Emit "new $0($1...)">] abstract Create: unit -> TimeoutError
type SchedulerLike = ___types.SchedulerLike
type Observable = ___Observable.Observable

type [<AllowNullLiteral>] IExports =
    abstract bindCallback: callbackFunc: Function * resultSelector: Function * ?scheduler: SchedulerLike -> (ResizeArray<obj option> -> Observable<obj option>)
    abstract bindCallback: callbackFunc: (('R1 -> 'R2 -> 'R3 -> 'R4 -> ResizeArray<obj option> -> obj option) -> obj option) * ?scheduler: SchedulerLike -> (unit -> Observable<ResizeArray<obj option>>)
    abstract bindCallback: callbackFunc: (('R1 -> 'R2 -> 'R3 -> obj option) -> obj option) * ?scheduler: SchedulerLike -> (unit -> Observable<'R1 * 'R2 * 'R3>)
    abstract bindCallback: callbackFunc: (('R1 -> 'R2 -> obj option) -> obj option) * ?scheduler: SchedulerLike -> (unit -> Observable<'R1 * 'R2>)
    abstract bindCallback: callbackFunc: (('R1 -> obj option) -> obj option) * ?scheduler: SchedulerLike -> (unit -> Observable<'R1>)
    abstract bindCallback: callbackFunc: ((unit -> obj option) -> obj option) * ?scheduler: SchedulerLike -> (unit -> Observable<unit>)
    abstract bindCallback: callbackFunc: ('A1 -> ('R1 -> 'R2 -> 'R3 -> 'R4 -> ResizeArray<obj option> -> obj option) -> obj option) * ?scheduler: SchedulerLike -> ('A1 -> Observable<ResizeArray<obj option>>)
    abstract bindCallback: callbackFunc: ('A1 -> ('R1 -> 'R2 -> 'R3 -> obj option) -> obj option) * ?scheduler: SchedulerLike -> ('A1 -> Observable<'R1 * 'R2 * 'R3>)
    abstract bindCallback: callbackFunc: ('A1 -> ('R1 -> 'R2 -> obj option) -> obj option) * ?scheduler: SchedulerLike -> ('A1 -> Observable<'R1 * 'R2>)
    abstract bindCallback: callbackFunc: ('A1 -> ('R1 -> obj option) -> obj option) * ?scheduler: SchedulerLike -> ('A1 -> Observable<'R1>)
    abstract bindCallback: callbackFunc: ('A1 -> (unit -> obj option) -> obj option) * ?scheduler: SchedulerLike -> ('A1 -> Observable<unit>)
    abstract bindCallback: callbackFunc: ('A1 -> 'A2 -> ('R1 -> 'R2 -> 'R3 -> 'R4 -> ResizeArray<obj option> -> obj option) -> obj option) * ?scheduler: SchedulerLike -> ('A1 -> 'A2 -> Observable<ResizeArray<obj option>>)
    abstract bindCallback: callbackFunc: ('A1 -> 'A2 -> ('R1 -> 'R2 -> 'R3 -> obj option) -> obj option) * ?scheduler: SchedulerLike -> ('A1 -> 'A2 -> Observable<'R1 * 'R2 * 'R3>)
    abstract bindCallback: callbackFunc: ('A1 -> 'A2 -> ('R1 -> 'R2 -> obj option) -> obj option) * ?scheduler: SchedulerLike -> ('A1 -> 'A2 -> Observable<'R1 * 'R2>)
    abstract bindCallback: callbackFunc: ('A1 -> 'A2 -> ('R1 -> obj option) -> obj option) * ?scheduler: SchedulerLike -> ('A1 -> 'A2 -> Observable<'R1>)
    abstract bindCallback: callbackFunc: ('A1 -> 'A2 -> (unit -> obj option) -> obj option) * ?scheduler: SchedulerLike -> ('A1 -> 'A2 -> Observable<unit>)
    abstract bindCallback: callbackFunc: ('A1 -> 'A2 -> 'A3 -> ('R1 -> 'R2 -> 'R3 -> 'R4 -> ResizeArray<obj option> -> obj option) -> obj option) * ?scheduler: SchedulerLike -> ('A1 -> 'A2 -> 'A3 -> Observable<ResizeArray<obj option>>)
    abstract bindCallback: callbackFunc: ('A1 -> 'A2 -> 'A3 -> ('R1 -> 'R2 -> 'R3 -> obj option) -> obj option) * ?scheduler: SchedulerLike -> ('A1 -> 'A2 -> 'A3 -> Observable<'R1 * 'R2 * 'R3>)
    abstract bindCallback: callbackFunc: ('A1 -> 'A2 -> 'A3 -> ('R1 -> 'R2 -> obj option) -> obj option) * ?scheduler: SchedulerLike -> ('A1 -> 'A2 -> 'A3 -> Observable<'R1 * 'R2>)
    abstract bindCallback: callbackFunc: ('A1 -> 'A2 -> 'A3 -> ('R1 -> obj option) -> obj option) * ?scheduler: SchedulerLike -> ('A1 -> 'A2 -> 'A3 -> Observable<'R1>)
    abstract bindCallback: callbackFunc: ('A1 -> 'A2 -> 'A3 -> (unit -> obj option) -> obj option) * ?scheduler: SchedulerLike -> ('A1 -> 'A2 -> 'A3 -> Observable<unit>)
    abstract bindCallback: callbackFunc: ('A1 -> 'A2 -> 'A3 -> 'A4 -> ('R1 -> 'R2 -> 'R3 -> 'R4 -> ResizeArray<obj option> -> obj option) -> obj option) * ?scheduler: SchedulerLike -> ('A1 -> 'A2 -> 'A3 -> 'A4 -> Observable<ResizeArray<obj option>>)
    abstract bindCallback: callbackFunc: ('A1 -> 'A2 -> 'A3 -> 'A4 -> ('R1 -> 'R2 -> 'R3 -> obj option) -> obj option) * ?scheduler: SchedulerLike -> ('A1 -> 'A2 -> 'A3 -> 'A4 -> Observable<'R1 * 'R2 * 'R3>)
    abstract bindCallback: callbackFunc: ('A1 -> 'A2 -> 'A3 -> 'A4 -> ('R1 -> 'R2 -> obj option) -> obj option) * ?scheduler: SchedulerLike -> ('A1 -> 'A2 -> 'A3 -> 'A4 -> Observable<'R1 * 'R2>)
    abstract bindCallback: callbackFunc: ('A1 -> 'A2 -> 'A3 -> 'A4 -> ('R1 -> obj option) -> obj option) * ?scheduler: SchedulerLike -> ('A1 -> 'A2 -> 'A3 -> 'A4 -> Observable<'R1>)
    abstract bindCallback: callbackFunc: ('A1 -> 'A2 -> 'A3 -> 'A4 -> (unit -> obj option) -> obj option) * ?scheduler: SchedulerLike -> ('A1 -> 'A2 -> 'A3 -> 'A4 -> Observable<unit>)
    abstract bindCallback: callbackFunc: ('A1 -> 'A2 -> 'A3 -> 'A4 -> 'A5 -> ('R1 -> 'R2 -> 'R3 -> 'R4 -> ResizeArray<obj option> -> obj option) -> obj option) * ?scheduler: SchedulerLike -> ('A1 -> 'A2 -> 'A3 -> 'A4 -> 'A5 -> Observable<ResizeArray<obj option>>)
    abstract bindCallback: callbackFunc: ('A1 -> 'A2 -> 'A3 -> 'A4 -> 'A5 -> ('R1 -> 'R2 -> 'R3 -> obj option) -> obj option) * ?scheduler: SchedulerLike -> ('A1 -> 'A2 -> 'A3 -> 'A4 -> 'A5 -> Observable<'R1 * 'R2 * 'R3>)
    abstract bindCallback: callbackFunc: ('A1 -> 'A2 -> 'A3 -> 'A4 -> 'A5 -> ('R1 -> 'R2 -> obj option) -> obj option) * ?scheduler: SchedulerLike -> ('A1 -> 'A2 -> 'A3 -> 'A4 -> 'A5 -> Observable<'R1 * 'R2>)
    abstract bindCallback: callbackFunc: ('A1 -> 'A2 -> 'A3 -> 'A4 -> 'A5 -> ('R1 -> obj option) -> obj option) * ?scheduler: SchedulerLike -> ('A1 -> 'A2 -> 'A3 -> 'A4 -> 'A5 -> Observable<'R1>)
    abstract bindCallback: callbackFunc: ('A1 -> 'A2 -> 'A3 -> 'A4 -> 'A5 -> (unit -> obj option) -> obj option) * ?scheduler: SchedulerLike -> ('A1 -> 'A2 -> 'A3 -> 'A4 -> 'A5 -> Observable<unit>)
    abstract bindCallback: callbackFunc: (Array<U2<'A, ('R -> obj option)>> -> obj option) * ?scheduler: SchedulerLike -> (ResizeArray<'A> -> Observable<'R>)
    abstract bindCallback: callbackFunc: (Array<U2<'A, (ResizeArray<'R> -> obj option)>> -> obj option) * ?scheduler: SchedulerLike -> (ResizeArray<'A> -> Observable<ResizeArray<'R>>)
    abstract bindCallback: callbackFunc: Function * ?scheduler: SchedulerLike -> (ResizeArray<obj option> -> Observable<obj option>)
type Observable = ___Observable.Observable
type SchedulerLike = ___types.SchedulerLike

type [<AllowNullLiteral>] IExports =
    abstract bindNodeCallback: callbackFunc: Function * resultSelector: Function * ?scheduler: SchedulerLike -> (ResizeArray<obj option> -> Observable<obj option>)
    abstract bindNodeCallback: callbackFunc: ((obj option -> 'R1 -> 'R2 -> 'R3 -> 'R4 -> ResizeArray<obj option> -> obj option) -> obj option) * ?scheduler: SchedulerLike -> (ResizeArray<obj option> -> Observable<ResizeArray<obj option>>)
    abstract bindNodeCallback: callbackFunc: ((obj option -> 'R1 -> 'R2 -> 'R3 -> obj option) -> obj option) * ?scheduler: SchedulerLike -> (unit -> Observable<'R1 * 'R2 * 'R3>)
    abstract bindNodeCallback: callbackFunc: ((obj option -> 'R1 -> 'R2 -> obj option) -> obj option) * ?scheduler: SchedulerLike -> (unit -> Observable<'R1 * 'R2>)
    abstract bindNodeCallback: callbackFunc: ((obj option -> 'R1 -> obj option) -> obj option) * ?scheduler: SchedulerLike -> (unit -> Observable<'R1>)
    abstract bindNodeCallback: callbackFunc: ((obj option -> obj option) -> obj option) * ?scheduler: SchedulerLike -> (unit -> Observable<unit>)
    abstract bindNodeCallback: callbackFunc: ('A1 -> (obj option -> 'R1 -> 'R2 -> 'R3 -> 'R4 -> ResizeArray<obj option> -> obj option) -> obj option) * ?scheduler: SchedulerLike -> (ResizeArray<obj option> -> Observable<ResizeArray<obj option>>)
    abstract bindNodeCallback: callbackFunc: ('A1 -> (obj option -> 'R1 -> 'R2 -> 'R3 -> obj option) -> obj option) * ?scheduler: SchedulerLike -> ('A1 -> Observable<'R1 * 'R2 * 'R3>)
    abstract bindNodeCallback: callbackFunc: ('A1 -> (obj option -> 'R1 -> 'R2 -> obj option) -> obj option) * ?scheduler: SchedulerLike -> ('A1 -> Observable<'R1 * 'R2>)
    abstract bindNodeCallback: callbackFunc: ('A1 -> (obj option -> 'R1 -> obj option) -> obj option) * ?scheduler: SchedulerLike -> ('A1 -> Observable<'R1>)
    abstract bindNodeCallback: callbackFunc: ('A1 -> (obj option -> obj option) -> obj option) * ?scheduler: SchedulerLike -> ('A1 -> Observable<unit>)
    abstract bindNodeCallback: callbackFunc: ('A1 -> 'A2 -> (obj option -> 'R1 -> 'R2 -> 'R3 -> 'R4 -> ResizeArray<obj option> -> obj option) -> obj option) * ?scheduler: SchedulerLike -> (ResizeArray<obj option> -> Observable<ResizeArray<obj option>>)
    abstract bindNodeCallback: callbackFunc: ('A1 -> 'A2 -> (obj option -> 'R1 -> 'R2 -> 'R3 -> obj option) -> obj option) * ?scheduler: SchedulerLike -> ('A1 -> 'A2 -> Observable<'R1 * 'R2 * 'R3>)
    abstract bindNodeCallback: callbackFunc: ('A1 -> 'A2 -> (obj option -> 'R1 -> 'R2 -> obj option) -> obj option) * ?scheduler: SchedulerLike -> ('A1 -> 'A2 -> Observable<'R1 * 'R2>)
    abstract bindNodeCallback: callbackFunc: ('A1 -> 'A2 -> (obj option -> 'R1 -> obj option) -> obj option) * ?scheduler: SchedulerLike -> ('A1 -> 'A2 -> Observable<'R1>)
    abstract bindNodeCallback: callbackFunc: ('A1 -> 'A2 -> (obj option -> obj option) -> obj option) * ?scheduler: SchedulerLike -> ('A1 -> 'A2 -> Observable<unit>)
    abstract bindNodeCallback: callbackFunc: ('A1 -> 'A2 -> 'A3 -> (obj option -> 'R1 -> 'R2 -> 'R3 -> 'R4 -> ResizeArray<obj option> -> obj option) -> obj option) * ?scheduler: SchedulerLike -> (ResizeArray<obj option> -> Observable<ResizeArray<obj option>>)
    abstract bindNodeCallback: callbackFunc: ('A1 -> 'A2 -> 'A3 -> (obj option -> 'R1 -> 'R2 -> 'R3 -> obj option) -> obj option) * ?scheduler: SchedulerLike -> ('A1 -> 'A2 -> 'A3 -> Observable<'R1 * 'R2 * 'R3>)
    abstract bindNodeCallback: callbackFunc: ('A1 -> 'A2 -> 'A3 -> (obj option -> 'R1 -> 'R2 -> obj option) -> obj option) * ?scheduler: SchedulerLike -> ('A1 -> 'A2 -> 'A3 -> Observable<'R1 * 'R2>)
    abstract bindNodeCallback: callbackFunc: ('A1 -> 'A2 -> 'A3 -> (obj option -> 'R1 -> obj option) -> obj option) * ?scheduler: SchedulerLike -> ('A1 -> 'A2 -> 'A3 -> Observable<'R1>)
    abstract bindNodeCallback: callbackFunc: ('A1 -> 'A2 -> 'A3 -> (obj option -> obj option) -> obj option) * ?scheduler: SchedulerLike -> ('A1 -> 'A2 -> 'A3 -> Observable<unit>)
    abstract bindNodeCallback: callbackFunc: ('A1 -> 'A2 -> 'A3 -> 'A4 -> (obj option -> 'R1 -> 'R2 -> 'R3 -> 'R4 -> ResizeArray<obj option> -> obj option) -> obj option) * ?scheduler: SchedulerLike -> (ResizeArray<obj option> -> Observable<ResizeArray<obj option>>)
    abstract bindNodeCallback: callbackFunc: ('A1 -> 'A2 -> 'A3 -> 'A4 -> (obj option -> 'R1 -> 'R2 -> 'R3 -> obj option) -> obj option) * ?scheduler: SchedulerLike -> ('A1 -> 'A2 -> 'A3 -> 'A4 -> Observable<'R1 * 'R2 * 'R3>)
    abstract bindNodeCallback: callbackFunc: ('A1 -> 'A2 -> 'A3 -> 'A4 -> (obj option -> 'R1 -> 'R2 -> obj option) -> obj option) * ?scheduler: SchedulerLike -> ('A1 -> 'A2 -> 'A3 -> 'A4 -> Observable<'R1 * 'R2>)
    abstract bindNodeCallback: callbackFunc: ('A1 -> 'A2 -> 'A3 -> 'A4 -> (obj option -> 'R1 -> obj option) -> obj option) * ?scheduler: SchedulerLike -> ('A1 -> 'A2 -> 'A3 -> 'A4 -> Observable<'R1>)
    abstract bindNodeCallback: callbackFunc: ('A1 -> 'A2 -> 'A3 -> 'A4 -> (obj option -> obj option) -> obj option) * ?scheduler: SchedulerLike -> ('A1 -> 'A2 -> 'A3 -> 'A4 -> Observable<unit>)
    abstract bindNodeCallback: callbackFunc: ('A1 -> 'A2 -> 'A3 -> 'A4 -> 'A5 -> (obj option -> 'R1 -> 'R2 -> 'R3 -> 'R4 -> ResizeArray<obj option> -> obj option) -> obj option) * ?scheduler: SchedulerLike -> (ResizeArray<obj option> -> Observable<ResizeArray<obj option>>)
    abstract bindNodeCallback: callbackFunc: ('A1 -> 'A2 -> 'A3 -> 'A4 -> 'A5 -> (obj option -> 'R1 -> 'R2 -> 'R3 -> obj option) -> obj option) * ?scheduler: SchedulerLike -> ('A1 -> 'A2 -> 'A3 -> 'A4 -> 'A5 -> Observable<'R1 * 'R2 * 'R3>)
    abstract bindNodeCallback: callbackFunc: ('A1 -> 'A2 -> 'A3 -> 'A4 -> 'A5 -> (obj option -> 'R1 -> 'R2 -> obj option) -> obj option) * ?scheduler: SchedulerLike -> ('A1 -> 'A2 -> 'A3 -> 'A4 -> 'A5 -> Observable<'R1 * 'R2>)
    abstract bindNodeCallback: callbackFunc: ('A1 -> 'A2 -> 'A3 -> 'A4 -> 'A5 -> (obj option -> 'R1 -> obj option) -> obj option) * ?scheduler: SchedulerLike -> ('A1 -> 'A2 -> 'A3 -> 'A4 -> 'A5 -> Observable<'R1>)
    abstract bindNodeCallback: callbackFunc: ('A1 -> 'A2 -> 'A3 -> 'A4 -> 'A5 -> (obj option -> obj option) -> obj option) * ?scheduler: SchedulerLike -> ('A1 -> 'A2 -> 'A3 -> 'A4 -> 'A5 -> Observable<unit>)
    abstract bindNodeCallback: callbackFunc: Function * ?scheduler: SchedulerLike -> (ResizeArray<obj option> -> Observable<ResizeArray<obj option>>)
type Subscriber = __Subscriber.Subscriber
type OuterSubscriber = __OuterSubscriber.OuterSubscriber

type [<AllowNullLiteral>] IExports =
    abstract InnerSubscriber: InnerSubscriberStatic

/// We need this JSDoc comment for affecting ESDoc.
type [<AllowNullLiteral>] InnerSubscriber<'T, 'R> =
    inherit Subscriber<'R>
    abstract parent: obj with get, set
    abstract outerValue: 'T with get, set
    abstract outerIndex: float with get, set
    abstract index: obj with get, set
    abstract _next: value: 'R -> unit
    abstract _error: error: obj option -> unit
    abstract _complete: unit -> unit

/// We need this JSDoc comment for affecting ESDoc.
type [<AllowNullLiteral>] InnerSubscriberStatic =
    [<Emit "new $0($1...)">] abstract Create: parent: OuterSubscriber<'T, 'R> * outerValue: 'T * outerIndex: float -> InnerSubscriber<'T, 'R>
type Subscriber = __Subscriber.Subscriber
type InnerSubscriber = __InnerSubscriber.InnerSubscriber

type [<AllowNullLiteral>] IExports =
    abstract OuterSubscriber: OuterSubscriberStatic

/// We need this JSDoc comment for affecting ESDoc.
type [<AllowNullLiteral>] OuterSubscriber<'T, 'R> =
    inherit Subscriber<'T>
    abstract notifyNext: outerValue: 'T * innerValue: 'R * outerIndex: float * innerIndex: float * innerSub: InnerSubscriber<'T, 'R> -> unit
    abstract notifyError: error: obj option * innerSub: InnerSubscriber<'T, 'R> -> unit
    abstract notifyComplete: innerSub: InnerSubscriber<'T, 'R> -> unit

/// We need this JSDoc comment for affecting ESDoc.
type [<AllowNullLiteral>] OuterSubscriberStatic =
    [<Emit "new $0($1...)">] abstract Create: unit -> OuterSubscriber<'T, 'R>
type Observable = ___Observable.Observable
type ObservableInput = ___types.ObservableInput
type SchedulerLike = ___types.SchedulerLike
type Subscriber = ___Subscriber.Subscriber
type OuterSubscriber = ___OuterSubscriber.OuterSubscriber
type Operator = ___Operator.Operator
type InnerSubscriber = ___InnerSubscriber.InnerSubscriber

type [<AllowNullLiteral>] IExports =
    abstract combineLatest: v1: ObservableInput<'T> * resultSelector: ('T -> 'R) * ?scheduler: SchedulerLike -> Observable<'R>
    abstract combineLatest: v1: ObservableInput<'T> * v2: ObservableInput<'T2> * resultSelector: ('T -> 'T2 -> 'R) * ?scheduler: SchedulerLike -> Observable<'R>
    abstract combineLatest: v1: ObservableInput<'T> * v2: ObservableInput<'T2> * v3: ObservableInput<'T3> * resultSelector: ('T -> 'T2 -> 'T3 -> 'R) * ?scheduler: SchedulerLike -> Observable<'R>
    abstract combineLatest: v1: ObservableInput<'T> * v2: ObservableInput<'T2> * v3: ObservableInput<'T3> * v4: ObservableInput<'T4> * resultSelector: ('T -> 'T2 -> 'T3 -> 'T4 -> 'R) * ?scheduler: SchedulerLike -> Observable<'R>
    abstract combineLatest: v1: ObservableInput<'T> * v2: ObservableInput<'T2> * v3: ObservableInput<'T3> * v4: ObservableInput<'T4> * v5: ObservableInput<'T5> * resultSelector: ('T -> 'T2 -> 'T3 -> 'T4 -> 'T5 -> 'R) * ?scheduler: SchedulerLike -> Observable<'R>
    abstract combineLatest: v1: ObservableInput<'T> * v2: ObservableInput<'T2> * v3: ObservableInput<'T3> * v4: ObservableInput<'T4> * v5: ObservableInput<'T5> * v6: ObservableInput<'T6> * resultSelector: ('T -> 'T2 -> 'T3 -> 'T4 -> 'T5 -> 'T6 -> 'R) * ?scheduler: SchedulerLike -> Observable<'R>
    abstract combineLatest: v1: ObservableInput<'T> * v2: ObservableInput<'T2> * ?scheduler: SchedulerLike -> Observable<'T * 'T2>
    abstract combineLatest: v1: ObservableInput<'T> * v2: ObservableInput<'T2> * v3: ObservableInput<'T3> * ?scheduler: SchedulerLike -> Observable<'T * 'T2 * 'T3>
    abstract combineLatest: v1: ObservableInput<'T> * v2: ObservableInput<'T2> * v3: ObservableInput<'T3> * v4: ObservableInput<'T4> * ?scheduler: SchedulerLike -> Observable<'T * 'T2 * 'T3 * 'T4>
    abstract combineLatest: v1: ObservableInput<'T> * v2: ObservableInput<'T2> * v3: ObservableInput<'T3> * v4: ObservableInput<'T4> * v5: ObservableInput<'T5> * ?scheduler: SchedulerLike -> Observable<'T * 'T2 * 'T3 * 'T4 * 'T5>
    abstract combineLatest: v1: ObservableInput<'T> * v2: ObservableInput<'T2> * v3: ObservableInput<'T3> * v4: ObservableInput<'T4> * v5: ObservableInput<'T5> * v6: ObservableInput<'T6> * ?scheduler: SchedulerLike -> Observable<'T * 'T2 * 'T3 * 'T4 * 'T5 * 'T6>
    abstract combineLatest: array: ResizeArray<ObservableInput<'T>> * ?scheduler: SchedulerLike -> Observable<ResizeArray<'T>>
    abstract combineLatest: array: ResizeArray<ObservableInput<obj option>> * ?scheduler: SchedulerLike -> Observable<'R>
    abstract combineLatest: array: ResizeArray<ObservableInput<'T>> * resultSelector: (Array<'T> -> 'R) * ?scheduler: SchedulerLike -> Observable<'R>
    abstract combineLatest: array: ResizeArray<ObservableInput<obj option>> * resultSelector: (Array<obj option> -> 'R) * ?scheduler: SchedulerLike -> Observable<'R>
    abstract combineLatest: [<ParamArray>] observables: Array<U2<ObservableInput<'T>, SchedulerLike>> -> Observable<ResizeArray<'T>>
    abstract combineLatest: [<ParamArray>] observables: Array<U3<ObservableInput<'T>, (Array<'T> -> 'R), SchedulerLike>> -> Observable<'R>
    abstract combineLatest: [<ParamArray>] observables: Array<U3<ObservableInput<obj option>, (Array<obj option> -> 'R), SchedulerLike>> -> Observable<'R>
    abstract CombineLatestOperator: CombineLatestOperatorStatic
    abstract CombineLatestSubscriber: CombineLatestSubscriberStatic

type [<AllowNullLiteral>] CombineLatestOperator<'T, 'R> =
    inherit Operator<'T, 'R>
    abstract resultSelector: obj with get, set
    abstract call: subscriber: Subscriber<'R> * source: obj option -> obj option

type [<AllowNullLiteral>] CombineLatestOperatorStatic =
    [<Emit "new $0($1...)">] abstract Create: ?resultSelector: (Array<obj option> -> 'R) -> CombineLatestOperator<'T, 'R>

/// We need this JSDoc comment for affecting ESDoc.
type [<AllowNullLiteral>] CombineLatestSubscriber<'T, 'R> =
    inherit OuterSubscriber<'T, 'R>
    abstract resultSelector: obj with get, set
    abstract active: obj with get, set
    abstract values: obj with get, set
    abstract observables: obj with get, set
    abstract toRespond: obj with get, set
    abstract _next: observable: obj option -> unit
    abstract _complete: unit -> unit
    abstract notifyComplete: unused: Subscriber<'R> -> unit
    abstract notifyNext: outerValue: 'T * innerValue: 'R * outerIndex: float * innerIndex: float * innerSub: InnerSubscriber<'T, 'R> -> unit
    abstract _tryResultSelector: values: obj -> unit

/// We need this JSDoc comment for affecting ESDoc.
type [<AllowNullLiteral>] CombineLatestSubscriberStatic =
    [<Emit "new $0($1...)">] abstract Create: destination: Subscriber<'R> * ?resultSelector: (Array<obj option> -> 'R) -> CombineLatestSubscriber<'T, 'R>
type Observable = ___Observable.Observable
type ObservableInput = ___types.ObservableInput
type SchedulerLike = ___types.SchedulerLike

type [<AllowNullLiteral>] IExports =
    abstract concat: v1: ObservableInput<'T> * ?scheduler: SchedulerLike -> Observable<'T>
    abstract concat: v1: ObservableInput<'T> * v2: ObservableInput<'T2> * ?scheduler: SchedulerLike -> Observable<U2<'T, 'T2>>
    abstract concat: v1: ObservableInput<'T> * v2: ObservableInput<'T2> * v3: ObservableInput<'T3> * ?scheduler: SchedulerLike -> Observable<U3<'T, 'T2, 'T3>>
    abstract concat: v1: ObservableInput<'T> * v2: ObservableInput<'T2> * v3: ObservableInput<'T3> * v4: ObservableInput<'T4> * ?scheduler: SchedulerLike -> Observable<U4<'T, 'T2, 'T3, 'T4>>
    abstract concat: v1: ObservableInput<'T> * v2: ObservableInput<'T2> * v3: ObservableInput<'T3> * v4: ObservableInput<'T4> * v5: ObservableInput<'T5> * ?scheduler: SchedulerLike -> Observable<U5<'T, 'T2, 'T3, 'T4, 'T5>>
    abstract concat: v1: ObservableInput<'T> * v2: ObservableInput<'T2> * v3: ObservableInput<'T3> * v4: ObservableInput<'T4> * v5: ObservableInput<'T5> * v6: ObservableInput<'T6> * ?scheduler: SchedulerLike -> Observable<U6<'T, 'T2, 'T3, 'T4, 'T5, 'T6>>
    abstract concat: [<ParamArray>] observables: ResizeArray<U2<ObservableInput<'T>, SchedulerLike>> -> Observable<'T>
    abstract concat: [<ParamArray>] observables: ResizeArray<U2<ObservableInput<obj option>, SchedulerLike>> -> Observable<'R>
type Observable = ___Observable.Observable
type SubscribableOrPromise = ___types.SubscribableOrPromise

type [<AllowNullLiteral>] IExports =
    /// <summary>Creates an Observable that, on subscribe, calls an Observable factory to
    /// make an Observable for each new Observer.
    /// 
    /// <span class="informal">Creates the Observable lazily, that is, only when it
    /// is subscribed.
    /// </span>
    /// 
    /// <img src="./img/defer.png" width="100%">
    /// 
    /// `defer` allows you to create the Observable only when the Observer
    /// subscribes, and create a fresh Observable for each Observer. It waits until
    /// an Observer subscribes to it, and then it generates an Observable,
    /// typically with an Observable factory function. It does this afresh for each
    /// subscriber, so although each subscriber may think it is subscribing to the
    /// same Observable, in fact each subscriber gets its own individual
    /// Observable.</summary>
    /// <param name="observableFactory">The Observable
    /// factory function to invoke for each Observer that subscribes to the output
    /// Observable. May also return a Promise, which will be converted on the fly
    /// to an Observable.</param>
    abstract defer: observableFactory: (unit -> U2<SubscribableOrPromise<'T>, unit>) -> Observable<'T>
type Observable = ___Observable.Observable
type SchedulerLike = ___types.SchedulerLike
let [<Import("*","rxjs")>] EMPTY: Observable<obj> = jsNative

type [<AllowNullLiteral>] IExports =
    /// <summary>Creates an Observable that emits no items to the Observer and immediately
    /// emits a complete notification.
    /// 
    /// <span class="informal">Just emits 'complete', and nothing else.
    /// </span>
    /// 
    /// <img src="./img/empty.png" width="100%">
    /// 
    /// This static operator is useful for creating a simple Observable that only
    /// emits the complete notification. It can be used for composing with other
    /// Observables, such as in a {@link mergeMap}.</summary>
    /// <param name="scheduler">A {</param>
    abstract empty: ?scheduler: SchedulerLike -> Observable<obj>
    abstract emptyScheduled: scheduler: SchedulerLike -> Observable<obj>
type Observable = ___Observable.Observable
type ObservableInput = ___types.ObservableInput

type [<AllowNullLiteral>] IExports =
    abstract forkJoin: sources: ObservableInput<'T> -> Observable<ResizeArray<'T>>
    abstract forkJoin: sources: ObservableInput<'T> * ObservableInput<'T2> -> Observable<'T * 'T2>
    abstract forkJoin: sources: ObservableInput<'T> * ObservableInput<'T2> * ObservableInput<'T3> -> Observable<'T * 'T2 * 'T3>
    abstract forkJoin: sources: ObservableInput<'T> * ObservableInput<'T2> * ObservableInput<'T3> * ObservableInput<'T4> -> Observable<'T * 'T2 * 'T3 * 'T4>
    abstract forkJoin: sources: ObservableInput<'T> * ObservableInput<'T2> * ObservableInput<'T3> * ObservableInput<'T4> * ObservableInput<'T5> -> Observable<'T * 'T2 * 'T3 * 'T4 * 'T5>
    abstract forkJoin: sources: ObservableInput<'T> * ObservableInput<'T2> * ObservableInput<'T3> * ObservableInput<'T4> * ObservableInput<'T5> * ObservableInput<'T6> -> Observable<'T * 'T2 * 'T3 * 'T4 * 'T5 * 'T6>
    abstract forkJoin: sources: Array<ObservableInput<'T>> -> Observable<ResizeArray<'T>>
    abstract forkJoin: v1: ObservableInput<'T> -> Observable<ResizeArray<'T>>
    abstract forkJoin: v1: ObservableInput<'T> * v2: ObservableInput<'T2> -> Observable<'T * 'T2>
    abstract forkJoin: v1: ObservableInput<'T> * v2: ObservableInput<'T2> * v3: ObservableInput<'T3> -> Observable<'T * 'T2 * 'T3>
    abstract forkJoin: v1: ObservableInput<'T> * v2: ObservableInput<'T2> * v3: ObservableInput<'T3> * v4: ObservableInput<'T4> -> Observable<'T * 'T2 * 'T3 * 'T4>
    abstract forkJoin: v1: ObservableInput<'T> * v2: ObservableInput<'T2> * v3: ObservableInput<'T3> * v4: ObservableInput<'T4> * v5: ObservableInput<'T5> -> Observable<'T * 'T2 * 'T3 * 'T4 * 'T5>
    abstract forkJoin: v1: ObservableInput<'T> * v2: ObservableInput<'T2> * v3: ObservableInput<'T3> * v4: ObservableInput<'T4> * v5: ObservableInput<'T5> * v6: ObservableInput<'T6> -> Observable<'T * 'T2 * 'T3 * 'T4 * 'T5 * 'T6>
    abstract forkJoin: [<ParamArray>] args: Array<U2<ObservableInput<obj option>, Function>> -> Observable<obj option>
    abstract forkJoin: [<ParamArray>] sources: ResizeArray<ObservableInput<'T>> -> Observable<ResizeArray<'T>>
type Observable = ___Observable.Observable
type ObservableInput = ___types.ObservableInput
type SchedulerLike = ___types.SchedulerLike

type [<AllowNullLiteral>] IExports =
    abstract from: input: ObservableInput<'T> * ?scheduler: SchedulerLike -> Observable<'T>
    abstract from: input: ObservableInput<ObservableInput<'T>> * ?scheduler: SchedulerLike -> Observable<Observable<'T>>
type Observable = ___Observable.Observable

type [<AllowNullLiteral>] IExports =
    abstract fromEvent: target: FromEventTarget<'T> * eventName: string -> Observable<'T>
    abstract fromEvent: target: FromEventTarget<'T> * eventName: string * resultSelector: (ResizeArray<obj option> -> 'T) -> Observable<'T>
    abstract fromEvent: target: FromEventTarget<'T> * eventName: string * options: EventListenerOptions -> Observable<'T>
    abstract fromEvent: target: FromEventTarget<'T> * eventName: string * options: EventListenerOptions * resultSelector: (ResizeArray<obj option> -> 'T) -> Observable<'T>

type [<AllowNullLiteral>] NodeStyleEventEmitter =
    abstract addListener: (U2<string, Symbol> -> NodeEventHandler -> NodeStyleEventEmitter) with get, set
    abstract removeListener: (U2<string, Symbol> -> NodeEventHandler -> NodeStyleEventEmitter) with get, set

type [<AllowNullLiteral>] NodeEventHandler =
    [<Emit "$0($1...)">] abstract Invoke: [<ParamArray>] args: ResizeArray<obj option> -> unit

type [<AllowNullLiteral>] JQueryStyleEventEmitter =
    abstract on: (string -> Function -> unit) with get, set
    abstract off: (string -> Function -> unit) with get, set

type [<AllowNullLiteral>] HasEventTargetAddRemove<'E> =
    abstract addEventListener: ``type``: string * listener: ('E -> unit) option * ?options: U2<bool, AddEventListenerOptions> -> unit
    abstract removeEventListener: ``type``: string * ?listener: ('E -> unit) option * ?options: U2<EventListenerOptions, bool> -> unit

type EventTargetLike<'T> =
    U3<HasEventTargetAddRemove<'T>, NodeStyleEventEmitter, JQueryStyleEventEmitter>

[<RequireQualifiedAccess; CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module EventTargetLike =
    let ofHasEventTargetAddRemove v: EventTargetLike<'T> = v |> U3.Case1
    let isHasEventTargetAddRemove (v: EventTargetLike<'T>) = match v with U3.Case1 _ -> true | _ -> false
    let asHasEventTargetAddRemove (v: EventTargetLike<'T>) = match v with U3.Case1 o -> Some o | _ -> None
    let ofNodeStyleEventEmitter v: EventTargetLike<'T> = v |> U3.Case2
    let isNodeStyleEventEmitter (v: EventTargetLike<'T>) = match v with U3.Case2 _ -> true | _ -> false
    let asNodeStyleEventEmitter (v: EventTargetLike<'T>) = match v with U3.Case2 o -> Some o | _ -> None
    let ofJQueryStyleEventEmitter v: EventTargetLike<'T> = v |> U3.Case3
    let isJQueryStyleEventEmitter (v: EventTargetLike<'T>) = match v with U3.Case3 _ -> true | _ -> false
    let asJQueryStyleEventEmitter (v: EventTargetLike<'T>) = match v with U3.Case3 o -> Some o | _ -> None

type FromEventTarget<'T> =
    U2<EventTargetLike<'T>, ArrayLike<EventTargetLike<'T>>>

[<RequireQualifiedAccess; CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module FromEventTarget =
    let ofEventTargetLike v: FromEventTarget<'T> = v |> U2.Case1
    let isEventTargetLike (v: FromEventTarget<'T>) = match v with U2.Case1 _ -> true | _ -> false
    let asEventTargetLike (v: FromEventTarget<'T>) = match v with U2.Case1 o -> Some o | _ -> None
    let ofArrayLike v: FromEventTarget<'T> = v |> U2.Case2
    let isArrayLike (v: FromEventTarget<'T>) = match v with U2.Case2 _ -> true | _ -> false
    let asArrayLike (v: FromEventTarget<'T>) = match v with U2.Case2 o -> Some o | _ -> None

type [<AllowNullLiteral>] EventListenerOptions =
    abstract capture: bool option with get, set
    abstract passive: bool option with get, set
    abstract once: bool option with get, set

type [<AllowNullLiteral>] AddEventListenerOptions =
    inherit EventListenerOptions
    abstract once: bool option with get, set
    abstract passive: bool option with get, set
type Observable = ___Observable.Observable

type [<AllowNullLiteral>] IExports =
    abstract fromEventPattern: addHandler: (Function -> obj option) * ?removeHandler: (Function -> obj option -> unit) -> Observable<'T>
    abstract fromEventPattern: addHandler: (Function -> obj option) * ?removeHandler: (Function -> obj option -> unit) * ?resultSelector: (ResizeArray<obj option> -> 'T) -> Observable<'T>
type Observable = ___Observable.Observable
type SchedulerLike = ___types.SchedulerLike

type [<AllowNullLiteral>] IExports =
    /// <summary>Generates an observable sequence by running a state-driven loop
    /// producing the sequence's elements, using the specified scheduler
    /// to send out observer messages.
    /// 
    /// <img src="./img/generate.png" width="100%"></summary>
    /// <param name="initialState">Initial state.</param>
    /// <param name="condition">Condition to terminate generation (upon returning false).</param>
    /// <param name="iterate">Iteration step function.</param>
    /// <param name="resultSelector">Selector function for results produced in the sequence.</param>
    /// <param name="scheduler">A {</param>
    abstract generate: initialState: 'S * condition: ConditionFunc<'S> * iterate: IterateFunc<'S> * resultSelector: ResultFunc<'S, 'T> * ?scheduler: SchedulerLike -> Observable<'T>
    /// <summary>Generates an observable sequence by running a state-driven loop
    /// producing the sequence's elements, using the specified scheduler
    /// to send out observer messages.
    /// The overload uses state as an emitted value.
    /// 
    /// <img src="./img/generate.png" width="100%"></summary>
    /// <param name="initialState">Initial state.</param>
    /// <param name="condition">Condition to terminate generation (upon returning false).</param>
    /// <param name="iterate">Iteration step function.</param>
    /// <param name="scheduler">A {</param>
    abstract generate: initialState: 'S * condition: ConditionFunc<'S> * iterate: IterateFunc<'S> * ?scheduler: SchedulerLike -> Observable<'S>
    /// <summary>Generates an observable sequence by running a state-driven loop
    /// producing the sequence's elements, using the specified scheduler
    /// to send out observer messages.
    /// The overload accepts options object that might contain initial state, iterate,
    /// condition and scheduler.
    /// 
    /// <img src="./img/generate.png" width="100%"></summary>
    /// <param name="options">Object that must contain initialState, iterate and might contain condition and scheduler.</param>
    abstract generate: options: GenerateBaseOptions<'S> -> Observable<'S>
    /// <summary>Generates an observable sequence by running a state-driven loop
    /// producing the sequence's elements, using the specified scheduler
    /// to send out observer messages.
    /// The overload accepts options object that might contain initial state, iterate,
    /// condition, result selector and scheduler.
    /// 
    /// <img src="./img/generate.png" width="100%"></summary>
    /// <param name="options">Object that must contain initialState, iterate, resultSelector and might contain condition and scheduler.</param>
    abstract generate: options: GenerateOptions<'T, 'S> -> Observable<'T>

type [<AllowNullLiteral>] ConditionFunc<'S> =
    [<Emit "$0($1...)">] abstract Invoke: state: 'S -> bool

type [<AllowNullLiteral>] IterateFunc<'S> =
    [<Emit "$0($1...)">] abstract Invoke: state: 'S -> 'S

type [<AllowNullLiteral>] ResultFunc<'S, 'T> =
    [<Emit "$0($1...)">] abstract Invoke: state: 'S -> 'T

type [<AllowNullLiteral>] GenerateBaseOptions<'S> =
    /// Initial state.
    abstract initialState: 'S with get, set
    /// Condition function that accepts state and returns boolean.
    /// When it returns false, the generator stops.
    /// If not specified, a generator never stops.
    abstract condition: ConditionFunc<'S> option with get, set
    /// Iterate function that accepts state and returns new state.
    abstract iterate: IterateFunc<'S> with get, set
    /// SchedulerLike to use for generation process.
    /// By default, a generator starts immediately.
    abstract scheduler: SchedulerLike option with get, set

type [<AllowNullLiteral>] GenerateOptions<'T, 'S> =
    inherit GenerateBaseOptions<'S>
    /// Result selection function that accepts state and returns a value to emit.
    abstract resultSelector: ResultFunc<'S, 'T> with get, set
type Observable = ___Observable.Observable
type SchedulerLike = ___types.SchedulerLike

type [<AllowNullLiteral>] IExports =
    /// <summary>Creates an Observable that emits sequential numbers every specified
    /// interval of time, on a specified IScheduler.
    /// 
    /// <span class="informal">Emits incremental numbers periodically in time.
    /// </span>
    /// 
    /// <img src="./img/interval.png" width="100%">
    /// 
    /// `interval` returns an Observable that emits an infinite sequence of
    /// ascending integers, with a constant interval of time of your choosing
    /// between those emissions. The first emission is not sent immediately, but
    /// only after the first period has passed. By default, this operator uses the
    /// `async` IScheduler to provide a notion of time, but you may pass any
    /// IScheduler to it.</summary>
    /// <param name="period">The interval size in milliseconds (by default)
    /// or the time unit determined by the scheduler's clock.</param>
    /// <param name="scheduler">The IScheduler to use for scheduling
    /// the emission of values, and providing a notion of "time".</param>
    abstract interval: ?period: float * ?scheduler: SchedulerLike -> Observable<float>
type Observable = ___Observable.Observable
type ObservableInput = ___types.ObservableInput
type SchedulerLike = ___types.SchedulerLike

type [<AllowNullLiteral>] IExports =
    abstract merge: v1: ObservableInput<'T> * ?scheduler: SchedulerLike -> Observable<'T>
    abstract merge: v1: ObservableInput<'T> * ?concurrent: float * ?scheduler: SchedulerLike -> Observable<'T>
    abstract merge: v1: ObservableInput<'T> * v2: ObservableInput<'T2> * ?scheduler: SchedulerLike -> Observable<U2<'T, 'T2>>
    abstract merge: v1: ObservableInput<'T> * v2: ObservableInput<'T2> * ?concurrent: float * ?scheduler: SchedulerLike -> Observable<U2<'T, 'T2>>
    abstract merge: v1: ObservableInput<'T> * v2: ObservableInput<'T2> * v3: ObservableInput<'T3> * ?scheduler: SchedulerLike -> Observable<U3<'T, 'T2, 'T3>>
    abstract merge: v1: ObservableInput<'T> * v2: ObservableInput<'T2> * v3: ObservableInput<'T3> * ?concurrent: float * ?scheduler: SchedulerLike -> Observable<U3<'T, 'T2, 'T3>>
    abstract merge: v1: ObservableInput<'T> * v2: ObservableInput<'T2> * v3: ObservableInput<'T3> * v4: ObservableInput<'T4> * ?scheduler: SchedulerLike -> Observable<U4<'T, 'T2, 'T3, 'T4>>
    abstract merge: v1: ObservableInput<'T> * v2: ObservableInput<'T2> * v3: ObservableInput<'T3> * v4: ObservableInput<'T4> * ?concurrent: float * ?scheduler: SchedulerLike -> Observable<U4<'T, 'T2, 'T3, 'T4>>
    abstract merge: v1: ObservableInput<'T> * v2: ObservableInput<'T2> * v3: ObservableInput<'T3> * v4: ObservableInput<'T4> * v5: ObservableInput<'T5> * ?scheduler: SchedulerLike -> Observable<U5<'T, 'T2, 'T3, 'T4, 'T5>>
    abstract merge: v1: ObservableInput<'T> * v2: ObservableInput<'T2> * v3: ObservableInput<'T3> * v4: ObservableInput<'T4> * v5: ObservableInput<'T5> * ?concurrent: float * ?scheduler: SchedulerLike -> Observable<U5<'T, 'T2, 'T3, 'T4, 'T5>>
    abstract merge: v1: ObservableInput<'T> * v2: ObservableInput<'T2> * v3: ObservableInput<'T3> * v4: ObservableInput<'T4> * v5: ObservableInput<'T5> * v6: ObservableInput<'T6> * ?scheduler: SchedulerLike -> Observable<U6<'T, 'T2, 'T3, 'T4, 'T5, 'T6>>
    abstract merge: v1: ObservableInput<'T> * v2: ObservableInput<'T2> * v3: ObservableInput<'T3> * v4: ObservableInput<'T4> * v5: ObservableInput<'T5> * v6: ObservableInput<'T6> * ?concurrent: float * ?scheduler: SchedulerLike -> Observable<U6<'T, 'T2, 'T3, 'T4, 'T5, 'T6>>
    abstract merge: [<ParamArray>] observables: ResizeArray<U3<ObservableInput<'T>, SchedulerLike, float>> -> Observable<'T>
    abstract merge: [<ParamArray>] observables: ResizeArray<U3<ObservableInput<obj option>, SchedulerLike, float>> -> Observable<'R>
type Observable = ___Observable.Observable
let [<Import("*","rxjs")>] NEVER: Observable<obj> = jsNative

type [<AllowNullLiteral>] IExports =
    abstract never: unit -> Observable<obj>
type SchedulerLike = ___types.SchedulerLike
type Observable = ___Observable.Observable

type [<AllowNullLiteral>] IExports =
    abstract ``of``: a: 'T * ?scheduler: SchedulerLike -> Observable<'T>
    abstract ``of``: a: 'T * b: 'T2 * ?scheduler: SchedulerLike -> Observable<U2<'T, 'T2>>
    abstract ``of``: a: 'T * b: 'T2 * c: 'T3 * ?scheduler: SchedulerLike -> Observable<U3<'T, 'T2, 'T3>>
    abstract ``of``: a: 'T * b: 'T2 * c: 'T3 * d: 'T4 * ?scheduler: SchedulerLike -> Observable<U4<'T, 'T2, 'T3, 'T4>>
    abstract ``of``: a: 'T * b: 'T2 * c: 'T3 * d: 'T4 * e: 'T5 * ?scheduler: SchedulerLike -> Observable<U5<'T, 'T2, 'T3, 'T4, 'T5>>
    abstract ``of``: a: 'T * b: 'T2 * c: 'T3 * d: 'T4 * e: 'T5 * f: 'T6 * ?scheduler: SchedulerLike -> Observable<U6<'T, 'T2, 'T3, 'T4, 'T5, 'T6>>
    abstract ``of``: a: 'T * b: 'T2 * c: 'T3 * d: 'T4 * e: 'T5 * f: 'T6 * g: 'T7 * ?scheduler: SchedulerLike -> Observable<U7<'T, 'T2, 'T3, 'T4, 'T5, 'T6, 'T7>>
    abstract ``of``: a: 'T * b: 'T2 * c: 'T3 * d: 'T4 * e: 'T5 * f: 'T6 * g: 'T7 * h: 'T8 * ?scheduler: SchedulerLike -> Observable<U8<'T, 'T2, 'T3, 'T4, 'T5, 'T6, 'T7, 'T8>>
    abstract ``of``: a: 'T * b: 'T2 * c: 'T3 * d: 'T4 * e: 'T5 * f: 'T6 * g: 'T7 * h: 'T8 * i: 'T9 * ?scheduler: SchedulerLike -> Observable<obj>
    abstract ``of``: [<ParamArray>] args: Array<U2<'T, SchedulerLike>> -> Observable<'T>
type Observable = ___Observable.Observable
type ObservableInput = ___types.ObservableInput

type [<AllowNullLiteral>] IExports =
    abstract onErrorResumeNext: v: ObservableInput<'R> -> Observable<'R>
    abstract onErrorResumeNext: v2: ObservableInput<'T2> * v3: ObservableInput<'T3> -> Observable<'R>
    abstract onErrorResumeNext: v2: ObservableInput<'T2> * v3: ObservableInput<'T3> * v4: ObservableInput<'T4> -> Observable<'R>
    abstract onErrorResumeNext: v2: ObservableInput<'T2> * v3: ObservableInput<'T3> * v4: ObservableInput<'T4> * v5: ObservableInput<'T5> -> Observable<'R>
    abstract onErrorResumeNext: v2: ObservableInput<'T2> * v3: ObservableInput<'T3> * v4: ObservableInput<'T4> * v5: ObservableInput<'T5> * v6: ObservableInput<'T6> -> Observable<'R>
    abstract onErrorResumeNext: [<ParamArray>] observables: Array<U2<ObservableInput<obj option>, (Array<obj option> -> 'R)>> -> Observable<'R>
    abstract onErrorResumeNext: array: ResizeArray<ObservableInput<obj option>> -> Observable<'R>
type Observable = ___Observable.Observable
type SchedulerAction = ___types.SchedulerAction
type SchedulerLike = ___types.SchedulerLike
type Subscriber = ___Subscriber.Subscriber
type Subscription = ___Subscription.Subscription

type [<AllowNullLiteral>] IExports =
    /// <summary>Convert an object into an observable sequence of [key, value] pairs
    /// using an optional IScheduler to enumerate the object.</summary>
    /// <param name="obj">The object to inspect and turn into an
    /// Observable sequence.</param>
    /// <param name="scheduler">An optional IScheduler to run the
    /// enumeration of the input sequence on.</param>
    abstract pairs: obj: Object * ?scheduler: SchedulerLike -> Observable<string * 'T>
    abstract dispatch: this: SchedulerAction<obj option> * state: DispatchState -> unit

type [<AllowNullLiteral>] DispatchState =
    abstract keys: ResizeArray<string> with get, set
    abstract index: float with get, set
    abstract subscriber: Subscriber<string * 'T> with get, set
    abstract subscription: Subscription with get, set
    abstract obj: Object with get, set
type Observable = ___Observable.Observable
type Operator = ___Operator.Operator
type Subscriber = ___Subscriber.Subscriber
type TeardownLogic = ___types.TeardownLogic
type OuterSubscriber = ___OuterSubscriber.OuterSubscriber
type InnerSubscriber = ___InnerSubscriber.InnerSubscriber

type [<AllowNullLiteral>] IExports =
    /// Returns an Observable that mirrors the first source Observable to emit an item.
    abstract race: observables: Array<Observable<'T>> -> Observable<'T>
    abstract race: observables: Array<Observable<obj option>> -> Observable<'T>
    abstract race: [<ParamArray>] observables: Array<U2<Observable<'T>, Array<Observable<'T>>>> -> Observable<'T>
    abstract RaceOperator: RaceOperatorStatic
    abstract RaceSubscriber: RaceSubscriberStatic

type [<AllowNullLiteral>] RaceOperator<'T> =
    inherit Operator<'T, 'T>
    abstract call: subscriber: Subscriber<'T> * source: obj option -> TeardownLogic

type [<AllowNullLiteral>] RaceOperatorStatic =
    [<Emit "new $0($1...)">] abstract Create: unit -> RaceOperator<'T>

/// We need this JSDoc comment for affecting ESDoc.
type [<AllowNullLiteral>] RaceSubscriber<'T> =
    inherit OuterSubscriber<'T, 'T>
    abstract hasFirst: obj with get, set
    abstract observables: obj with get, set
    abstract subscriptions: obj with get, set
    abstract _next: observable: obj option -> unit
    abstract _complete: unit -> unit
    abstract notifyNext: outerValue: 'T * innerValue: 'T * outerIndex: float * innerIndex: float * innerSub: InnerSubscriber<'T, 'T> -> unit

/// We need this JSDoc comment for affecting ESDoc.
type [<AllowNullLiteral>] RaceSubscriberStatic =
    [<Emit "new $0($1...)">] abstract Create: destination: Subscriber<'T> -> RaceSubscriber<'T>
type SchedulerAction = ___types.SchedulerAction
type SchedulerLike = ___types.SchedulerLike
type Observable = ___Observable.Observable

type [<AllowNullLiteral>] IExports =
    /// <summary>Creates an Observable that emits a sequence of numbers within a specified
    /// range.
    /// 
    /// <span class="informal">Emits a sequence of numbers in a range.</span>
    /// 
    /// <img src="./img/range.png" width="100%">
    /// 
    /// `range` operator emits a range of sequential integers, in order, where you
    /// select the `start` of the range and its `length`. By default, uses no
    /// IScheduler and just delivers the notifications synchronously, but may use
    /// an optional IScheduler to regulate those deliveries.</summary>
    /// <param name="start">The value of the first integer in the sequence.</param>
    /// <param name="count">The number of sequential integers to generate.</param>
    /// <param name="scheduler">A {</param>
    abstract range: ?start: float * ?count: float * ?scheduler: SchedulerLike -> Observable<float>
    abstract dispatch: this: SchedulerAction<obj option> * state: obj option -> unit
type Observable = ___Observable.Observable
type SchedulerLike = ___types.SchedulerLike

type [<AllowNullLiteral>] IExports =
    /// <summary>Creates an Observable that starts emitting after an `initialDelay` and
    /// emits ever increasing numbers after each `period` of time thereafter.
    /// 
    /// <span class="informal">Its like {@link interval}, but you can specify when
    /// should the emissions start.</span>
    /// 
    /// <img src="./img/timer.png" width="100%">
    /// 
    /// `timer` returns an Observable that emits an infinite sequence of ascending
    /// integers, with a constant interval of time, `period` of your choosing
    /// between those emissions. The first emission happens after the specified
    /// `initialDelay`. The initial delay may be a {@link Date}. By default, this
    /// operator uses the `async` IScheduler to provide a notion of time, but you
    /// may pass any IScheduler to it. If `period` is not specified, the output
    /// Observable emits only one value, `0`. Otherwise, it emits an infinite
    /// sequence.</summary>
    /// <param name="dueTime">The initial delay time to wait before
    /// emitting the first value of `0`.</param>
    /// <param name="periodOrScheduler">The period of time between emissions of the
    /// subsequent numbers.</param>
    /// <param name="scheduler">The IScheduler to use for scheduling
    /// the emission of values, and providing a notion of "time".</param>
    abstract timer: ?dueTime: U2<float, DateTime> * ?periodOrScheduler: U2<float, SchedulerLike> * ?scheduler: SchedulerLike -> Observable<float>
type Observable = ___Observable.Observable
type Unsubscribable = ___types.Unsubscribable
type ObservableInput = ___types.ObservableInput

type [<AllowNullLiteral>] IExports =
    /// <summary>Creates an Observable that uses a resource which will be disposed at the same time as the Observable.
    /// 
    /// <span class="informal">Use it when you catch yourself cleaning up after an Observable.</span>
    /// 
    /// `using` is a factory operator, which accepts two functions. First function returns a disposable resource.
    /// It can be an arbitrary object that implements `unsubscribe` method. Second function will be injected with
    /// that object and should return an Observable. That Observable can use resource object during its execution.
    /// Both functions passed to `using` will be called every time someone subscribes - neither an Observable nor
    /// resource object will be shared in any way between subscriptions.
    /// 
    /// When Observable returned by `using` is subscribed, Observable returned from the second function will be subscribed
    /// as well. All its notifications (nexted values, completion and error events) will be emitted unchanged by the output
    /// Observable. If however someone unsubscribes from the Observable or source Observable completes or errors by itself,
    /// the `unsubscribe` method on resource object will be called. This can be used to do any necessary clean up, which
    /// otherwise would have to be handled by hand. Note that complete or error notifications are not emitted when someone
    /// cancels subscription to an Observable via `unsubscribe`, so `using` can be used as a hook, allowing you to make
    /// sure that all resources which need to exist during an Observable execution will be disposed at appropriate time.</summary>
    /// <param name="resourceFactory">A function which creates any resource object
    /// that implements `unsubscribe` method.</param>
    /// <param name="observableFactory">A function which
    /// creates an Observable, that can use injected resource object.</param>
    abstract using: resourceFactory: (unit -> U2<Unsubscribable, unit>) * observableFactory: (U2<Unsubscribable, unit> -> U2<ObservableInput<'T>, unit>) -> Observable<'T>
type Observable = ___Observable.Observable
type Operator = ___Operator.Operator
type ObservableInput = ___types.ObservableInput
type Subscriber = ___Subscriber.Subscriber

type [<AllowNullLiteral>] IExports =
    abstract zip: v1: ObservableInput<'T> * resultSelector: ('T -> 'R) -> Observable<'R>
    abstract zip: v1: ObservableInput<'T> * v2: ObservableInput<'T2> * resultSelector: ('T -> 'T2 -> 'R) -> Observable<'R>
    abstract zip: v1: ObservableInput<'T> * v2: ObservableInput<'T2> * v3: ObservableInput<'T3> * resultSelector: ('T -> 'T2 -> 'T3 -> 'R) -> Observable<'R>
    abstract zip: v1: ObservableInput<'T> * v2: ObservableInput<'T2> * v3: ObservableInput<'T3> * v4: ObservableInput<'T4> * resultSelector: ('T -> 'T2 -> 'T3 -> 'T4 -> 'R) -> Observable<'R>
    abstract zip: v1: ObservableInput<'T> * v2: ObservableInput<'T2> * v3: ObservableInput<'T3> * v4: ObservableInput<'T4> * v5: ObservableInput<'T5> * resultSelector: ('T -> 'T2 -> 'T3 -> 'T4 -> 'T5 -> 'R) -> Observable<'R>
    abstract zip: v1: ObservableInput<'T> * v2: ObservableInput<'T2> * v3: ObservableInput<'T3> * v4: ObservableInput<'T4> * v5: ObservableInput<'T5> * v6: ObservableInput<'T6> * resultSelector: ('T -> 'T2 -> 'T3 -> 'T4 -> 'T5 -> 'T6 -> 'R) -> Observable<'R>
    abstract zip: v1: ObservableInput<'T> * v2: ObservableInput<'T2> -> Observable<'T * 'T2>
    abstract zip: v1: ObservableInput<'T> * v2: ObservableInput<'T2> * v3: ObservableInput<'T3> -> Observable<'T * 'T2 * 'T3>
    abstract zip: v1: ObservableInput<'T> * v2: ObservableInput<'T2> * v3: ObservableInput<'T3> * v4: ObservableInput<'T4> -> Observable<'T * 'T2 * 'T3 * 'T4>
    abstract zip: v1: ObservableInput<'T> * v2: ObservableInput<'T2> * v3: ObservableInput<'T3> * v4: ObservableInput<'T4> * v5: ObservableInput<'T5> -> Observable<'T * 'T2 * 'T3 * 'T4 * 'T5>
    abstract zip: v1: ObservableInput<'T> * v2: ObservableInput<'T2> * v3: ObservableInput<'T3> * v4: ObservableInput<'T4> * v5: ObservableInput<'T5> * v6: ObservableInput<'T6> -> Observable<'T * 'T2 * 'T3 * 'T4 * 'T5 * 'T6>
    abstract zip: array: ResizeArray<ObservableInput<'T>> -> Observable<ResizeArray<'T>>
    abstract zip: array: ResizeArray<ObservableInput<obj option>> -> Observable<'R>
    abstract zip: array: ResizeArray<ObservableInput<'T>> * resultSelector: (Array<'T> -> 'R) -> Observable<'R>
    abstract zip: array: ResizeArray<ObservableInput<obj option>> * resultSelector: (Array<obj option> -> 'R) -> Observable<'R>
    abstract zip: [<ParamArray>] observables: Array<ObservableInput<'T>> -> Observable<ResizeArray<'T>>
    abstract zip: [<ParamArray>] observables: Array<U2<ObservableInput<'T>, (Array<'T> -> 'R)>> -> Observable<'R>
    abstract zip: [<ParamArray>] observables: Array<U2<ObservableInput<obj option>, (Array<obj option> -> 'R)>> -> Observable<'R>
    abstract ZipOperator: ZipOperatorStatic
    abstract ZipSubscriber: ZipSubscriberStatic

type [<AllowNullLiteral>] ZipOperator<'T, 'R> =
    inherit Operator<'T, 'R>
    abstract resultSelector: (Array<obj option> -> 'R) with get, set
    abstract call: subscriber: Subscriber<'R> * source: obj option -> obj option

type [<AllowNullLiteral>] ZipOperatorStatic =
    [<Emit "new $0($1...)">] abstract Create: ?resultSelector: (Array<obj option> -> 'R) -> ZipOperator<'T, 'R>

/// We need this JSDoc comment for affecting ESDoc.
type [<AllowNullLiteral>] ZipSubscriber<'T, 'R> =
    inherit Subscriber<'T>
    abstract values: obj with get, set
    abstract resultSelector: obj with get, set
    abstract iterators: obj with get, set
    abstract active: obj with get, set
    abstract _next: value: obj option -> unit
    abstract _complete: unit -> unit
    abstract notifyInactive: unit -> unit
    abstract checkIterators: unit -> unit
    abstract _tryresultSelector: args: ResizeArray<obj option> -> unit

/// We need this JSDoc comment for affecting ESDoc.
type [<AllowNullLiteral>] ZipSubscriberStatic =
    [<Emit "new $0($1...)">] abstract Create: destination: Subscriber<'R> * ?resultSelector: (Array<obj option> -> 'R) * ?values: obj option -> ZipSubscriber<'T, 'R>
let [<Import("*","rxjs")>] config: obj = jsNative
