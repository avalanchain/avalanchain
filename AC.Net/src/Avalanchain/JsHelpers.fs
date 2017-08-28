module Avalanchain.JsHelpers

open System
open FSharp.Interop.Dynamic
open Jint

type Json = string
type JsFunc = string // TODO
// Javascript function of 1 parameter
type JsFunc1 = JsFunc 
// Javascript function of 2 parameters
type JsFunc2 = JsFunc 

let internal jsRunner (jsFunc: JsFunc) =
    let engine = Engine()
    engine.SetValue("log", new Action<obj>(Console.WriteLine)).
        SetValue("func", jsFunc).
        GetValue("func")

let mapFunc (mapFunc: JsFunc1) = 
    fun (json: Json) -> (jsRunner mapFunc).Invoke([| json |> Native.JsValue |]).AsString()

let filterFunc (filterFunc: JsFunc1) = 
    fun (json: Json) -> (jsRunner filterFunc).Invoke([| json |> Native.JsValue |]).AsBoolean()

