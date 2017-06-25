
#load "broadcast.fsx"

open System
open System.Collections.Immutable
open System.Security.Cryptography
open FSharp.Control

open Avalanchain
open Network


let endpoint1 = { IP = "127.0.0.1"; Port = 5000us }
let endpoint2 = { IP = "127.0.0.1"; Port = 5001us }
let endpoint3 = { IP = "127.0.0.1"; Port = 5002us }
let node1 = setupNode endpoint1 [endpoint1; endpoint2]
Threading.Thread.Sleep 5000
let node2 = setupNode endpoint2 [endpoint1; endpoint2]
Threading.Thread.Sleep 2000
// let node3 = setupNode endpoint3 [endpoint1; endpoint2]
