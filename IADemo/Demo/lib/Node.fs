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

    open Akka.Persistence.Query
    open Akka.Persistence.Query.Sql  

    open Microsoft.FSharp.Quotations
    open Microsoft.FSharp.Linq.QuotationEvaluation    

    
    module Network =
        type Endpoint = {
            IP: string
            Port: uint16
        }

    open Network
    open Chains

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
        Mediator: Lazy<IActorRef> 
        Journal: Lazy<SqlReadJournal>
    }

    let createSystem (name : string) (config : Akka.Configuration.Config) : ActorSystem = 
        let system = ActorSystem.Create(name, config)
        let extendedSystem = system :?> ExtendedActorSystem
        let exprSerializer = Akkling.Serialization.ExprSerializer(extendedSystem)
        let akka_pubsub = Akka.Cluster.Tools.PublishSubscribe.Serialization.DistributedPubSubMessageSerializer(extendedSystem)
        let hyperion = Akka.Serialization.HyperionSerializer(extendedSystem)           // I don't know why, but without this system cannot instantiate serializer
        system.Serialization.AddSerializer("expr", exprSerializer)
        system.Serialization.AddSerializationMap(typeof<Expr>, exprSerializer)
        // system.Serialization.AddSerializer("akka-pubsub", akka_pubsub)
        // system.Serialization.AddSerializationMap(typeof<Akka.Cluster.Tools.PublishSubscribe.IDistributedPubSubMessage>, akka_pubsub)
        system.Serialization.AddSerializer("hyperion", hyperion)
        system.Serialization.AddSerializationMap(typeof<Object>, hyperion)
        // system.Serialization.AddSerializationMap(typeof<Akka.Cluster.Tools.PublishSubscribe.Internal.SendToOneSubscriber>, akka_pubsub)
        system    

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

        //let dbFolder = "./" + nodeName + "/db"
        let dbFolder = "db"
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
                			connection-string = "Filename=./db/streams.db"
                			
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
                			connection-string = "Filename=./db/snapshots.db"

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
                }   """ //dbFolder dbFolder

        let config = 
            sprintf """
                akka {
                    actor {
                        provider = "Akka.Cluster.ClusterActorRefProvider, Akka.Cluster"
                        serializers {
                            akka-pubsub = "Akka.Cluster.Tools.PublishSubscribe.Serialization.DistributedPubSubMessageSerializer, Akka.Cluster.Tools"
                            akka-data-replication = "Akka.DistributedData.Serialization.ReplicatorMessageSerializer, Akka.DistributedData"
                            akka-replicated-data = "Akka.DistributedData.Serialization.ReplicatedDataSerializer, Akka.DistributedData"                            
                        }                            
                        serialization-bindings {
                            "Akka.Cluster.Tools.PublishSubscribe.IDistributedPubSubMessage, Akka.Cluster.Tools" = akka-pubsub
                            "Akka.Cluster.Tools.PublishSubscribe.Internal.SendToOneSubscriber, Akka.Cluster.Tools" = akka-pubsub
                            "Akka.DistributedData.IReplicatorMessage, Akka.DistributedData" = akka-data-replication
                            "Akka.DistributedData.IReplicatedDataSerialization, Akka.DistributedData" = akka-replicated-data
                        }                            
                        serialization-identifiers {
                            "Akka.Cluster.Tools.PublishSubscribe.Serialization.DistributedPubSubMessageSerializer, Akka.Cluster.Tools" = 21
                            "Akka.DistributedData.Serialization.ReplicatedDataSerializer, Akka.DistributedData" = 11
                            "Akka.DistributedData.Serialization.ReplicatorMessageSerializer, Akka.DistributedData" = 12
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
                        distributed-data {
                            # Actor name of the Replicator actor, /system/ddataReplicator
                            name = ddataReplicator

                            # Replicas are running on members tagged with this role.
                            # All members are used if undefined or empty.
                            role = ""

                            # How often the Replicator should send out gossip information
                            gossip-interval = 2 s

                            # How often the subscribers will be notified of changes, if any
                            notify-subscribers-interval = 500 ms

                            # Maximum number of entries to transfer in one gossip message when synchronizing
                            # the replicas. Next chunk will be transferred in next round of gossip.
                            max-delta-elements = 1000

                            # The id of the dispatcher to use for Replicator actors. If not specified
                            # default dispatcher is used.
                            # If specified you need to define the settings of the actual dispatcher.
                            use-dispatcher = ""

                            # How often the Replicator checks for pruning of data associated with
                            # removed cluster nodes.
                            pruning-interval = 30 s

                            # How long time it takes (worst case) to spread the data to all other replica nodes.
                            # This is used when initiating and completing the pruning process of data associated
                            # with removed cluster nodes. The time measurement is stopped when any replica is 
                            # unreachable, so it should be configured to worst case in a healthy cluster.
                            max-pruning-dissemination = 60 s

                            # Serialized Write and Read messages are cached when they are sent to 
                            # several nodes. If no further activity they are removed from the cache
                            # after this duration.
                            serializer-cache-time-to-live = 10s

                            delta-crdt {

                                # Some complex deltas grow in size for each update and above this
                                # threshold such deltas are discarded and sent as full state instead.
                                max-delta-size = 200  
                            }

                            durable {
                                # List of keys that are durable. Prefix matching is supported by using * at the
                                # end of a key.  
                                keys = [ "chainDefs", "transactions" ]

                                # The markers of that pruning has been performed for a removed node are kept for this
                                # time and thereafter removed. If and old data entry that was never pruned is
                                # injected and merged with existing data after this time the value will not be correct.
                                # This would be possible if replica with durable data didn't participate in the pruning
                                # (e.g. it was shutdown) and later started after this time. A durable replica should not 
                                # be stopped for longer time than this duration and if it is joining again after this
                                # duration its data should first be manually removed (from the lmdb directory).
                                # It should be in the magnitude of days. Note that there is a corresponding setting
                                # for non-durable data: 'akka.cluster.distributed-data.pruning-marker-time-to-live'.
                                pruning-marker-time-to-live = 10 d

                                # Fully qualified class name of the durable store actor. It must be a subclass
                                # of akka.actor.Actor and handle the protocol defined in 
                                # akka.cluster.ddata.DurableStore. The class must have a constructor with 
                                # com.typesafe.config.Config parameter.
                                store-actor-class = "Akka.DistributedData.LightningDB.LmdbDurableStore, Akka.DistributedData.LightningDB"

                                use-dispatcher = akka.cluster.distributed-data.durable.pinned-store

                                pinned-store {
                                    executor = thread-pool-executor
                                    type = PinnedDispatcher
                                }
                                lmdb {
                                    # Directory of LMDB file. There are two options:
                                    # 1. A relative or absolute path to a directory that ends with 'ddata'
                                    #    the full name of the directory will contain name of the ActorSystem
                                    #    and its remote port.
                                    # 2. Otherwise the path is used as is, as a relative or absolute path to
                                    #    a directory.
                                    #
                                    # When running in production you may want to configure this to a specific
                                    # path (alt 2), since the default directory contains the remote port of the
                                    # actor system to make the name unique. If using a dynamically assigned 
                                    # port (0) it will be different each time and the previously stored data 
                                    # will not be loaded.
                                    dir = "bin/Debug/net461/ddata"

                                    # Size in bytes of the memory mapped file.
                                    #map-size = 100 MiB
                                    map-size = 100000000

                                    # Accumulate changes before storing improves performance with the
                                    # risk of losing the last writes if the JVM crashes.
                                    # The interval is by default set to 'off' to write each update immediately.
                                    # Enabling write behind by specifying a duration, e.g. 200ms, is especially 
                                    # efficient when performing many writes to the same key, because it is only 
                                    # the last value for each key that will be serialized and stored.  
                                    write-behind-interval = 200 ms
                                    #write-behind-interval = off
                                }                                
                            }
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
                        //|> System.create systemName 
                        |> createSystem systemName 
        {   Endpoint = endpoint
            System = system
            Mat = system.Materializer()
            OverflowStrategy = overflowStrategy
            MaxBuffer = maxBuffer
            Mediator = lazy(DistributedPubSub.Get(system).Mediator)
            Journal = lazy(readJournal system)
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

