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
//let __serializer = FsPickler.CreateXmlSerializer()

let picklerSerializer data =
    __serializer.Pickle data

let picklerDeserializer<'T> data =
    __serializer.UnPickle<'T> data

let picklerJsonSerializer data =
    __serializer.PickleToString data

let picklerJsonDeserializer<'T> data =
    __serializer.UnPickleOfString<'T> data

let cryptoContext = cryptoContextRSANet("RSA Test")