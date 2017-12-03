#r "../packages/Spreads/lib/net451/Spreads.dll"
#r "../packages/Spreads.Core/lib/net451/Spreads.Core.dll"
#r "../packages/Spreads.Collections/lib/net451/Spreads.Collections.dll"
#r "../packages/Spreads.Unsafe/lib/netstandard1.0/Spreads.Unsafe.dll"
#r "../packages/System.ValueTuple/lib/portable-net40+sl4+win8+wp8/System.ValueTuple.dll"
#r "../packages/System.Buffers/lib/netstandard1.1/System.Buffers.dll"
#r "../packages/Spreads.Utils/lib/netstandard1.1/System.Buffers.Primitives.dll"

#time

open System
open Spreads
open Spreads.Collections

let rnd = Random()
let data = [| for j in 0 .. 9999 -> [| for i in 0 .. 10230 -> rnd.Next() |] |]

let scm = new SortedMap<int, int[]>(50);
//for i in 0 .. 999999 do scm.Add(i, i)
for i in 0 .. 999999 do scm.Add(i, data.[i % 10000])
