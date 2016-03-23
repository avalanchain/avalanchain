namespace Avalanchain.Web.Chain

open System.IO
open MBrace.Thespian

module Config = 

    open MBrace.Core
    open MBrace.Runtime
    open MBrace.Thespian

    // change to alter cluster size
    let private workerCount = 4
    
    let mutable private thespian = None
    /// Gets or creates a new Thespian cluster session.
    let GetCluster() = 
        match thespian with 
        | None -> 
            let cluster = 
                ThespianCluster.InitOnCurrentMachine(workerCount, 
                                                     logger = new ConsoleLogger(), 
                                                     logLevel = LogLevel.Info)
            thespian <- Some cluster
        | Some t -> ()
        thespian.Value

    /// Kills the current cluster session
    let KillCluster() =
        match thespian with
        | None -> ()
        | Some t -> t.KillAllWorkers() ; thespian <- None

type ChainClusterContext private() =
    do ThespianWorker.LocalExecutable <- Path.Combine(__SOURCE_DIRECTORY__, @"tools/mbrace.thespian.worker.exe")

    static member Instance = ChainClusterContext()


