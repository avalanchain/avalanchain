namespace Avalanchain

module Node =

    open System
    open Akka.Actor
    open Akka.Configuration
    open Akka.Cluster
    open Akka.Cluster.Tools.Singleton
    open Akka.Cluster.Tools.PublishSubscribe
    open Akka.Cluster.Sharding
    open Akka.Persistence
    open Akka.Streams
    open Akka.Streams.Dsl
    open Reactive.Streams

    open Hyperion

    open Akkling
    open Akkling.Persistence
    open Akkling.Cluster
    open Akkling.Cluster.Sharding
    open Akkling.Streams

    
    module Network =
        type Endpoint = {
            IP: string
            Port: uint16
        }

    open Network

    [<RequireQualifiedAccess>]
    type PersistTo = 
    | Memory
    | Sqlite

    type ACNode = {
        Endpoint: Endpoint
        System: ActorSystem
        Mat: ActorMaterializer
        OverflowStrategy: OverflowStrategy
        MaxBuffer: int
        Mediator: unit -> IActorRef 
    }

    // type DistPubSubMessage<'T> =
    //     | Message of 'T

    module DistPubSub =
        type [<CLIMutable>] DistPubSubMessage<'T> = { Message: 'T }
        with static member Complete = (null :> obj :?> DistPubSubMessage<'T>)

        let (|SubscribeAck|_|) (msg: obj) : SubscribeAck option =
            match msg with
            | :? SubscribeAck as e -> Some e
            | _ -> None
        let (|UnsubscribeAck|_|) (msg: obj) : UnsubscribeAck option =
            match msg with
            | :? UnsubscribeAck as e -> Some e
            | _ -> None
        

        type MediatorPublisher<'T>(topic: string, queue: ISourceQueue<'T>, log: string -> unit) as actor =
            inherit Akka.Actor.ActorBase()
            do log "Initing"
            let mdr = typed (DistributedPubSub.Get(ActorBase.Context.System).Mediator)
            do mdr <! Subscribe(topic, actor.Self)
            do log "Initialized"
            override actor.Receive (msg: obj) =
                match msg with
                | SubscribeAck _ ->
                    log (sprintf "Actor subscribed to topic: %s" topic)
                    true
                | UnsubscribeAck _ ->
                    log (sprintf "Actor unsubscribed from topic: %s" topic)
                    true
                | :? DistPubSubMessage<'T> as dps ->
                    // match dps with
                    // | Message m ->
                    //     printfn "Received: %A" msg
                    //     queue.AsyncOffer(m) |!> (typed ActorBase.Context.Self)
                    //     true
                    log (sprintf "Received: %A" msg)
                    queue.AsyncOffer(dps.Message) |!> (typed ActorBase.Context.Self)
                    true
                | :? IQueueOfferResult as qr ->
                    match qr with
                    | :? QueueOfferResult.Enqueued -> true
                    | :? QueueOfferResult.Dropped -> failwith "Message dropped"
                    | :? QueueOfferResult.Failure as f -> failwithf "Failed with exception: %A" f.Cause
                    | :? QueueOfferResult.QueueClosed -> failwith "Queue closed"
                    | _ -> false
                | _ ->
                    log (sprintf "Unhandled: %A" msg)
                    actor.Unhandled msg
                    false
            static member Props (topic: string, queue: ISourceQueue<'T>, log: string -> unit) = Props.Create<MediatorPublisher<'T>>(topic, queue, log)

        let distPubSubSource<'T> system topic (overflowStrategy: OverflowStrategy) (maxBuffer: int) =
            Source.queue overflowStrategy maxBuffer
            |> Source.mapMaterializedValue(fun queue ->
                                            MediatorPublisher<'T>.Props(topic, queue, printfn "%A")
                                            |> Props.From
                                            |> spawnAnonymous system
                                            |> ignore // TODO: Add actor removing
                                            Akka.NotUsed.Instance)

        let topic = "distpubsub"
        let distPubSubSink<'T, 'mat> system topic = 
            let mediator = DistributedPubSub.Get(system).Mediator |> typed
            Flow.id<'T, 'mat>
            |> Flow.map (fun (msg: 'T) -> Publish(topic, { Message = msg } ))
            |> Flow.toMat(Sink.toActorRef (Publish(topic, DistPubSubMessage<'T>.Complete)) mediator) Keep.left
            // |> Flow.toMat(Sink.forEach(fun (msg: 'T) -> mediator <! (Publish(topic, { Message = msg } )))) Keep.left
        

    let setupNode (nodeName: string) endpoint (seedNodes: Endpoint list) (overflowStrategy: OverflowStrategy) (maxBuffer: int) = //(distTopic: string option) =
        let systemName = "ac"
        let seedNodes = seedNodes 
                        |> List.map (fun ep -> sprintf "\"akka.tcp://%s@%s:%d/\"" systemName ep.IP ep.Port) 
                        |> fun l -> "[" + String.Join(", ", l) + "]"
        printfn "%s" seedNodes

        let dbFolder = "./" + nodeName + "/db"
        let sqliteSpec = 
            sprintf """
                persistence {
                    #journal.plugin = "akka.persistence.journal.inmem"
          
                    #snapshot-store.plugin = "akka.persistence.snapshot-store.local"
                	journal {
                		plugin = "akka.persistence.journal.sqlite"
                		sqlite {
                		
                			# qualified type name of the SQLite persistence journal actor
                			class = "Akka.Persistence.Sqlite.Journal.SqliteJournal, Akka.Persistence.Sqlite"

                			# dispatcher used to drive journal actor
                			plugin-dispatcher = "akka.actor.default-dispatcher"

                			# connection string used for database access
                            # "Filename=file:memdb-journal-" + counter.IncrementAndGet() + ".db;Mode=Memory;Cache=Shared"
                			connection-string = "Filename=%s/streams.db"
                			
                			# connection string name for .config file used when no connection string has been provided
                			connection-string-name = ""

                			# default SQLite commands timeout
                			connection-timeout = 30s

                			# SQLite table corresponding with persistent journal
                			table-name = event_journal
                			
                			# metadata table
                			metadata-table-name = journal_metadata

                			# should corresponding journal table be initialized automatically
                			auto-initialize = on

                			# timestamp provider used for generation of journal entries timestamps
                			timestamp-provider = "Akka.Persistence.Sql.Common.Journal.DefaultTimestampProvider, Akka.Persistence.Sql.Common"
                			
                			circuit-breaker {
                				max-failures = 5
                				call-timeout = 20s
                				reset-timeout = 60s
                			}

                            #Query section
                            #event-adapters {{
                            #  color-tagger  = ""Akka.Persistence.TCK.Query.ColorFruitTagger, Akka.Persistence.TCK""
                            #}}
                            #event-adapter-bindings = {{
                            #  ""System.String"" = color-tagger
                            #}}
                            #refresh-interval = 100ms
                		}
                	}

                	snapshot-store {
                		plugin = "akka.persistence.snapshot-store.sqlite"
                		sqlite {
                		
                			# qualified type name of the SQLite persistence journal actor
                			class = "Akka.Persistence.Sqlite.Snapshot.SqliteSnapshotStore, Akka.Persistence.Sqlite"

                			# dispatcher used to drive journal actor
                			plugin-dispatcher = "akka.actor.default-dispatcher"

                			# connection string used for database access
                			connection-string = "Filename=%s/snapshots.db"

                			# connection string name for .config file used when no connection string has been provided
                			connection-string-name = ""

                			# default SQLite commands timeout
                			connection-timeout = 30s
                			
                			# SQLite table corresponding with persistent journal
                			table-name = snapshot_store

                			# should corresponding journal table be initialized automatically
                			auto-initialize = on

                		}
                	}                                   

                    view.auto-update-interval = 100
                    query.journal.sql {
                      # Implementation class of the SQL ReadJournalProvider
                      class = "Akka.Persistence.Query.Sql.SqlReadJournalProvider, Akka.Persistence.Query.Sql"
                      # Absolute path to the write journal plugin configuration entry that this
                      # query journal will connect to.
                      # If undefined (or "") it will connect to the default journal as specified by the
                      # akka.persistence.journal.plugin property.
                      write-plugin = ""
                      # The SQL write journal is notifying the query side as soon as things
                      # are persisted, but for efficiency reasons the query side retrieves the events
                      # in batches that sometimes can be delayed up to the configured `refresh-interval`.
                      refresh-interval = 1s
                      # How many events to fetch in one query (replay) and keep buffered until they
                      # are delivered downstreams.
                      max-buffer-size = 1000
                    }                    
                }   """ dbFolder dbFolder

        let config = 
            sprintf """
                akka {
                    actor {
                        provider = "Akka.Cluster.ClusterActorRefProvider, Akka.Cluster"
                        serializers {
                            hyperion = "Akka.Serialization.HyperionSerializer, Akka.Serialization.Hyperion"
                        }
                        serialization-bindings {
                            "System.Object" = hyperion
                        }
                    }
                    remote {
                        helios.tcp {
                        public-hostname = "%s"
                        hostname = "%s"
                        port = %d
                        maximum-frame-size = 40000000b
                        }
                    }
                    cluster {
                        auto-down-unreachable-after = 5s
                        seed-nodes = %s
                        distributed-data {
                            max-delta-elements = 10000
                        }

                        pub-sub {
                            # Actor name of the mediator actor, /system/distributedPubSubMediator
                            name = distributedPubSubMediator

                            # Start the mediator on members tagged with this role.
                            # All members are used if undefined or empty.
                            #role = ""

                            # The routing logic to use for 'Send'
                            # Possible values: random, round-robin, broadcast
                            routing-logic = broadcast

                            # How often the DistributedPubSubMediator should send out gossip information
                            gossip-interval = 1s

                            # Removed entries are pruned after this duration
                            removed-time-to-live = 120s

                            # Maximum number of elements to transfer in one message when synchronizing the registries.
                            # Next chunk will be transferred in next round of gossip.
                            max-delta-elements = 3000
                        }            
                    }
                    #persistence {
                    #    journal.plugin = "akka.persistence.journal.inmem"
                    #    snapshot-store.plugin = "akka.persistence.snapshot-store.local"
                    #}
                    %s
                }
                """ endpoint.IP endpoint.IP endpoint.Port seedNodes sqliteSpec
                |> Configuration.parse
    
        let system = config//.WithFallback (DistributedPubSub.DefaultConfig())
                        |> System.create systemName 
        {   Endpoint = endpoint
            System = system
            Mat = system.Materializer()
            OverflowStrategy = overflowStrategy
            MaxBuffer = maxBuffer
            Mediator = fun () -> DistributedPubSub.Get(system).Mediator
             }            



    /////////

    //open Akka.Cluster
    //open Akka.DistributedData
    //open Akkling
    //open Akkling.DistributedData
    //open Akkling.DistributedData.Consistency

    //// let system = System.create "system" <| Configuration.parse """
    //// akka.actor.provider = "Akka.Cluster.ClusterActorRefProvider, Akka.Cluster"
    //// akka.remote.helios.tcp {
    ////     hostname = "127.0.0.1"
    ////     port = 4551
    //// }
    //// """
    //let cluster = Cluster.Get node1.System
    //let ddata = DistributedData.Get node1.System

    //// some helper functions
    //let (++) set e = ORSet.add cluster e set

    //// initialize set
    //let set = [ 1; 2; 3 ] |> List.fold (++) ORSet.empty

    //let key = ORSet.key "test-set"

    //// write that up in replicator under key 'test-set'
    //ddata.AsyncUpdate(key, set, writeLocal)
    //|> Async.RunSynchronously

    //// read data 
    //async {
    //    let! reply = ddata.AsyncGet(key, readLocal)
    //    match reply with
    //    | Some value -> printfn "Data for key %A: %A" key value
    //    | None -> printfn "Data for key '%A' not found" key
    //} |> Async.RunSynchronously

    //// delete data 
    //ddata.AsyncDelete(key, writeLocal) |> Async.RunSynchronously

    ////////

