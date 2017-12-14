namespace Avalanchain.Demo
open Suave
open App
open Argu

module Program =
    open System
    open System.Threading
    open FSharp.Configuration
    open Topshelf

    type Arguments =
    | Working_Directory of path:string
    | Listener of host:string * port:int
    | Log_Level of level:int
    | Install_Service of serviceName: string
    | Uninstall_Service of serviceName: string

    type Settings = AppSettings<"App.config">

    [<EntryPoint>]
    let main _ =
        let cancellationTokenSource = ref None
        let server = ref None
        let start hc =
            let cts = new CancellationTokenSource()
            let token = cts.Token
            server := Some (startServer(None, None, token))
            cancellationTokenSource := Some cts
            Thread.Sleep(1000)
            Catcher.queue03.Start
            Console.WriteLine("Server started. Press Ctrl-C to terminate.")
            true

        let stop hc =
            match !server with
            | Some s -> s.Dispose()
            | None -> ()
            match !cancellationTokenSource with
            | Some cts -> cts.Cancel()
            | None -> ()
            true

        Service.Default
        |> display_name (Settings.DisplayName)
        |> service_name (Settings.ServiceName)
        |> description (Settings.Description)
        |> with_start start
        |> with_stop stop
        |> run