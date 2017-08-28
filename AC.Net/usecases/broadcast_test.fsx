#load "broadcast.g.fsx"

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

let keyUID = (1).ToString()
let b1_uid1 = keyUID |> broadcaster<HashedValue> node1
let b2_uid1 = keyUID |> broadcaster<HashedValue> node2
// let b3_uid1 = keyUID |> broadcaster<HashedValue> node3

let inserts min max (broadcaster: Broadcaster<HashedValue>) = asyncSeq {
    let sha = SHA256Managed.Create()
    for i in min .. max do
        // if i % 10000 = 0 then do! Async.Sleep 100
        let v = sprintf "value: %s" (i.ToString())
        let hash = sha.ComputeHash(Text.ASCIIEncoding.UTF8.GetBytes v) |> Convert.ToBase64String
        // printfn "Sent %s" v
        yield broadcaster.Add { value = v; hash = hash }
} 

let printAsync av = async { let! vo = av; 
                            printfn "Added: %s" (match vo with Some v -> v | None -> "<<Error>>") }

let read f (broadcaster: Broadcaster<HashedValue>) = 
    async {
        let! vo = broadcaster.Read()
        match vo with
        | Some v -> f v
        | None -> ()
    }
    |> Async.RunSynchronously

let printCount (broadcaster: Broadcaster<HashedValue>) = read (fun v -> printfn "Size: %d" v.Count) broadcaster
let printState (broadcaster: Broadcaster<HashedValue>) = read (fun v -> printfn "Value: %A" v) broadcaster


#time;;

for j in 1 .. 10 do
    inserts (j * 10000) ((j + 1) * 10000 - 1) b1_uid1
    |> AsyncSeq.iterAsync (Async.Ignore)
    |> Async.RunSynchronously
    printfn "Done: %d" j
    Threading.Thread.Sleep 1000


printState b1_uid1
printState b2_uid1
// printState b3_uid1

printCount b1_uid1
printCount b2_uid1
// printCount b3_uid1