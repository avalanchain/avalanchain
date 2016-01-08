module Avalanchain.Utils

open System

open FSharp.Core.Fluent
open Chessie.ErrorHandling
open Nessos.FsPickler
open Nessos.FsPickler.Json

open SecPrimitives
open SecKeys
open RefsAndPathes


let __serializer = FsPickler.CreateJsonSerializer()
//let __serializer = FsPickler.CreateBinarySerializer()

let picklerSerializer data =
    __serializer.Pickle data

let picklerDeserializer<'T> data =
    __serializer.UnPickle<'T> data
