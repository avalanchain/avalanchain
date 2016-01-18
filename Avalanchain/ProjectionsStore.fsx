#I "../packages/Newtonsoft.Json.8.0.2/lib/net45"
#r "../packages/FSharp.Interop.Dynamic.3.0.0.0/lib/portable-net45+sl50+win/FSharp.Interop.Dynamic.dll"
#r "../packages/FSharp.Core.Fluent-4.0.1.0.0.5/lib/portable-net45+netcore45+wpa81+wp8+MonoAndroid1+MonoTouch1/FSharp.Core.Fluent-4.0.dll"
#r "../packages/FSharpx.Extras.1.10.3/lib/40/FSharpx.Extras.dll"
#r "../packages/FSharpx.Async.1.12.0/lib/net40/FSharpx.Async.dll"
#r "../packages/FSharpx.Collections.1.13.4/lib/net40/FSharpx.Collections.dll"
#r "../packages/Chessie.0.2.2/lib/net40/Chessie.dll"
#r "../packages/FSharp.Quotations.Evaluator.1.0.7/lib/net40/FSharp.Quotations.Evaluator.dll"
#r "../packages/FsPickler.1.7.1/lib/net45/FsPickler.dll"
#r "../packages/FsPickler.Json.1.7.1/lib/net45/FsPickler.Json.dll"

#load "SecPrimitives.fs"
#load "SecKeys.fs"
#load "RefsAndPathes.fs"
#load "StreamEvent.fs"
#load "Projection.fs"
#load "Quorum.fs"
#load "Acl.fs"
#load "Utils.fs"
#load "EventStream.fs"
#load "EventProcessor.fs"


open System

open FSharp.Interop.Dynamic
open FSharp.Core.Fluent
open Chessie.ErrorHandling

open Avalanchain
open SecKeys
open SecPrimitives
open RefsAndPathes
open StreamEvent
open Projection
open EventProcessor
open EventStream
open System.Dynamic



let ct = cryptoContextRSANet("RSA Test")


let intStorage = 
    let ss = serializeFunction ct.HashSigner Utils.picklerSerializer ct.Hasher
    let ds = deserializeFunction ct.ProofVerifier Utils.picklerDeserializer
    let projectionStorage = ProjectionStorage<int, int>(ss, ds)

    let calcF = 
        let f = <@ fun a b -> ok (a + b) @>
        let res = projectionStorage.Add(f)
        (returnOrFail res).F 1 2

    let calcMax = 
        let max = <@ fun (a: int) b -> ok (Math.Max(a, b)) @>
        let res = projectionStorage.Add(max)
        (returnOrFail res).F 1 2

    let calcMin = 
        let max = <@ fun (a: int) b -> ok (Math.Min(a, b)) @>
        let res = projectionStorage.Add(max)
        (returnOrFail res).F 1 2
    calcF, calcMax, calcMin


//let dynStorage = 
//    let ss = serializeFunction ct.HashSigner Utils.picklerSerializer ct.Hasher
//    let ds = deserializeFunction ct.ProofVerifier Utils.picklerDeserializer
//    let projectionStorage = ProjectionStorage<ExpandoObject, ExpandoObject>(ss, ds)
//
//    let toDyn (v: int) = 
//        let eo = ExpandoObject()
//        eo?Val <- v
//        eo
//
//    let calcF = 
//        let f = <@ fun a b -> ok (toDyn(a?Val + b?Val)) @>
//        let res = projectionStorage.Add(f)
//        (returnOrFail res).F (toDyn 1) (toDyn 2)
//
//    let calcMax = 
//        let max = <@ fun a b -> ok (toDyn(Math.Max(a?Val :> int, b?Val :> int))) @>
//        let res = projectionStorage.Add(max)
//        (returnOrFail res).F (toDyn 1) (toDyn 2)
//
//    let calcMin = 
//        let min = <@ fun a b -> ok (toDyn(Math.Min(a?Val :> int, b?Val :> int))) @>
//        let res = projectionStorage.Add(min)
//        (returnOrFail res).F (toDyn 1) (toDyn 2)
//    calcF, calcMax, calcMin


