#r "../packages/Edge.js/lib/net40/EdgeJs.dll"

open EdgeJs

let func = Edge.Func("""
    return function (data, cb) {
        cb(null, 'Node.js ' + process.version + ' welcomes ' + data);
    }
""")

let b = 1 

async { let! res = func.Invoke(".NET") |> Async.AwaitTask 
        printfn "Received: %A" res } 
|> Async.RunSynchronously