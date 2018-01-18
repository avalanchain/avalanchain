#load "Process.fsx"
open Process

let pr = runProcess "Demo.exe" "" dir
let processes = pr :: [ for port in 5501 .. 5505 -> runProcess "Demo.exe" (port.ToString()) dir ]

processes |> killProcesses

