open System
open System.Diagnostics

let runProcess  filename args startDir = 
    let timer = Stopwatch.StartNew()
    let procStartInfo = 
        ProcessStartInfo(
            RedirectStandardOutput = false,
            RedirectStandardError = false,
            UseShellExecute = true,
            FileName = filename,
            Arguments = args
        )
    match startDir with | Some d -> procStartInfo.WorkingDirectory <- d | _ -> ()

    new Process(StartInfo = procStartInfo)

let killProcesses (processes: Process seq) = for p in processes do p.Kill()


let pr = runProcess "Demo.exe" "" (Some """C:\GitHub\avalanchain2\avalanchain\IADemo\Demo\bin\Debug\net461\""")
pr.Start()
let processes = [ for port in 5501 .. 5002 -> 
                    runProcess "Demo.exe" (port.ToString()) (Some """C:\GitHub\avalanchain2\avalanchain\IADemo\Demo\bin\Debug\net461\""")]

let runProcessBackground  filename args startDir = 
    let timer = Stopwatch.StartNew()
    let procStartInfo = 
        ProcessStartInfo(
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            FileName = filename,
            Arguments = args
        )
    match startDir with | Some d -> procStartInfo.WorkingDirectory <- d | _ -> ()

    let outputs = System.Collections.Generic.List<string>()
    let errors = System.Collections.Generic.List<string>()
    let outputHandler f (_sender:obj) (args:DataReceivedEventArgs) = f args.Data
    let p = new Process(StartInfo = procStartInfo)
    p.OutputDataReceived.AddHandler(DataReceivedEventHandler (outputHandler outputs.Add))
    p.ErrorDataReceived.AddHandler(DataReceivedEventHandler (outputHandler errors.Add))
    let started = 
        try
            p.Start()
        with | ex ->
            ex.Data.Add("filename", filename)
            reraise()
    if not started then
        failwithf "Failed to start process %s" filename
    printfn "Started %s with pid %i" p.ProcessName p.Id
    p.BeginOutputReadLine()
    p.BeginErrorReadLine()
    p.WaitForExit()
    timer.Stop()
    printfn "Finished %s after %A milliseconds" filename timer.ElapsedMilliseconds
    let cleanOut l = l |> Seq.filter (fun o -> String.IsNullOrEmpty o |> not)
    cleanOut outputs,cleanOut errors