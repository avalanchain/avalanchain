﻿open Avalanchain.Cluster

// Define your library scripting code here

#time "on"
#load "Bootstrap.fsx"

open System
open Akka.Actor
open Akka.Configuration
open Akka.FSharp
open Akka.TestKit


//let system = System.create "system" <| Configuration.parse """
//    akka {
//        loglevel = DEBUG
//        actor {
//            serialization-bidnings {
//                "System.Object" = wire
//            }
//        }
//        debug {
//          receive = on
//          autoreceive = on
//          lifecycle = on
//          fsm = on
//          event-stream = on
//          unhandled = on
//          router-misconfiguration = on
//        }
//    }
//"""

let config = Configuration.parse """
    akka {

      cluster {
        # Initial contact points of the cluster.
        # The nodes to join automatically at startup.
        # Comma separated full URIs defined by a string on the form of
        # "akka.tcp://system@hostname:port"
        # Leave as empty if the node is supposed to be joined manually.
        seed-nodes = []

        # how long to wait for one of the seed nodes to reply to initial join request
        seed-node-timeout = 5s

        # If a join request fails it will be retried after this period.
        # Disable join retry by specifying "off".
        retry-unsuccessful-join-after = 10s

        # Should the 'leader' in the cluster be allowed to automatically mark
        # unreachable nodes as DOWN after a configured time of unreachability?
        # Using auto-down implies that two separate clusters will automatically be
        # formed in case of network partition.
        # Disable with "off" or specify a duration to enable auto-down.
        auto-down-unreachable-after = off
	
        # Margin until shards or singletons that belonged to a downed/removed
        # partition are created in surviving partition. The purpose of this margin is that 
        # in case of a network partition the persistent actors in the non-surviving partitions
        # must be stopped before corresponding persistent actors are started somewhere else.
        down-removal-margin = 20s
        
        # The roles of this member. List of strings, e.g. roles = ["A", "B"].
        # The roles are part of the membership information and can be used by
        # routers or other services to distribute work to certain member types,
        # e.g. front-end and back-end nodes.
        roles = []

        role {
          # Minimum required number of members of a certain role before the leader
          # changes member status of 'Joining' members to 'Up'. Typically used together
          # with 'Cluster.registerOnMemberUp' to defer some action, such as starting
          # actors, until the cluster has reached a certain size.
          # E.g. to require 2 nodes with role 'frontend' and 3 nodes with role 'backend':
          #   frontend.min-nr-of-members = 2
          #   backend.min-nr-of-members = 3
          #<role-name>.min-nr-of-members = 1
        }

        # Minimum required number of members before the leader changes member status
        # of 'Joining' members to 'Up'. Typically used together with
        # 'Cluster.registerOnMemberUp' to defer some action, such as starting actors,
        # until the cluster has reached a certain size.
        min-nr-of-members = 1

        # Enable/disable info level logging of cluster events
        log-info = on

        # Enable or disable JMX MBeans for management of the cluster
        jmx.enabled = on

        # how long should the node wait before starting the periodic tasks
        # maintenance tasks?
        periodic-tasks-initial-delay = 1s

        # how often should the node send out gossip information?
        gossip-interval = 1s
    
        # discard incoming gossip messages if not handled within this duration
        gossip-time-to-live = 2s

        # how often should the leader perform maintenance tasks?
        leader-actions-interval = 1s

        # how often should the node move nodes, marked as unreachable by the failure
        # detector, out of the membership ring?
        unreachable-nodes-reaper-interval = 1s

        # How often the current internal stats should be published.
        # A value of 0s can be used to always publish the stats, when it happens.
        # Disable with "off".
        publish-stats-interval = off

        # The id of the dispatcher to use for cluster actors. If not specified
        # default dispatcher is used.
        # If specified you need to define the settings of the actual dispatcher.
        use-dispatcher = "akka.cluster.default-cluster-dispatcher"

        # Gossip to random node with newer or older state information, if any with
        # this probability. Otherwise Gossip to any random live node.
        # Probability value is between 0.0 and 1.0. 0.0 means never, 1.0 means always.
        gossip-different-view-probability = 0.8
    
        # Reduced the above probability when the number of nodes in the cluster
        # greater than this value.
        reduce-gossip-different-view-probability = 400

        # Settings for the Phi accrual failure detector (http://ddg.jaist.ac.jp/pub/HDY+04.pdf
        # [Hayashibara et al]) used by the cluster subsystem to detect unreachable
        # members.
        failure-detector {

          # FQCN of the failure detector implementation.
          # It must implement akka.remote.FailureDetector and have
          # a public constructor with a com.typesafe.config.Config and
          # akka.actor.EventStream parameter.
          implementation-class = "Akka.Remote.PhiAccrualFailureDetector,Akka.Remote"

          # How often keep-alive heartbeat messages should be sent to each connection.
          heartbeat-interval = 1 s

          # Defines the failure detector threshold.
          # A low threshold is prone to generate many wrong suspicions but ensures
          # a quick detection in the event of a real crash. Conversely, a high
          # threshold generates fewer mistakes but needs more time to detect
          # actual crashes.
          threshold = 8.0

          # Number of the samples of inter-heartbeat arrival times to adaptively
          # calculate the failure timeout for connections.
          max-sample-size = 1000

          # Minimum standard deviation to use for the normal distribution in
          # AccrualFailureDetector. Too low standard deviation might result in
          # too much sensitivity for sudden, but normal, deviations in heartbeat
          # inter arrival times.
          min-std-deviation = 100 ms

          # Number of potentially lost/delayed heartbeats that will be
          # accepted before considering it to be an anomaly.
          # This margin is important to be able to survive sudden, occasional,
          # pauses in heartbeat arrivals, due to for example garbage collect or
          # network drop.
          acceptable-heartbeat-pause = 3 s

          # Number of member nodes that each member will send heartbeat messages to,
          # i.e. each node will be monitored by this number of other nodes.
          monitored-by-nr-of-members = 5
      
          # After the heartbeat request has been sent the first failure detection
          # will start after this period, even though no heartbeat mesage has
          # been received.
          expected-response-after = 5 s

        }

        metrics {
          # Enable or disable metrics collector for load-balancing nodes.
          enabled = on

          # FQCN of the metrics collector implementation.
          # It must implement akka.cluster.MetricsCollector and
          # have public constructor with akka.actor.ActorSystem parameter.
          collector-class = "Akka.Cluster.PerformanceCounterMetricsCollector, Akka.Cluster"

          # How often metrics are sampled on a node.
          # Shorter interval will collect the metrics more often.
          collect-interval = 3s

          # How often a node publishes metrics information.
          gossip-interval = 3s

          # How quickly the exponential weighting of past data is decayed compared to
          # new data. Set lower to increase the bias toward newer values.
          # The relevance of each data sample is halved for every passing half-life
          # duration, i.e. after 4 times the half-life, a data sample’s relevance is
          # reduced to 6% of its original relevance. The initial relevance of a data
          # sample is given by 1 – 0.5 ^ (collect-interval / half-life).
          # See http://en.wikipedia.org/wiki/Moving_average#Exponential_moving_average
          moving-average-half-life = 12s
        }

        # If the tick-duration of the default scheduler is longer than the
        # tick-duration configured here a dedicated scheduler will be used for
        # periodic tasks of the cluster, otherwise the default scheduler is used.
        # See akka.scheduler settings for more details.
        scheduler {
          tick-duration = 33ms
          ticks-per-wheel = 512
        }

        default-cluster-dispatcher {
          type = ForkJoinDispatcher
          dedicated-thread-pool {
            # Fixed number of threads to have in this threadpool
            thread-count = 4
          }
        }

      }

      # Default configuration for routers
      actor.deployment.default {
        # MetricsSelector to use
        # - available: "mix", "heap", "cpu", "load"
        # - or: Fully qualified class name of the MetricsSelector class.
        #       The class must extend akka.cluster.routing.MetricsSelector
        #       and have a public constructor with com.typesafe.config.Config
        #       parameter.
        # - default is "mix"
        metrics-selector = mix
      }
      actor.deployment.default.cluster {
        # enable cluster aware router that deploys to nodes in the cluster
        enabled = off

        # Maximum number of routees that will be deployed on each cluster
        # member node.
        # Note that nr-of-instances defines total number of routees, but
        # number of routees per node will not be exceeded, i.e. if you
        # define nr-of-instances = 50 and max-nr-of-instances-per-node = 2
        # it will deploy 2 routees per new member in the cluster, up to
        # 25 members.
        max-nr-of-instances-per-node = 1

        # Defines if routees are allowed to be located on the same node as
        # the head router actor, or only on remote nodes.
        # Useful for master-worker scenario where all routees are remote.
        allow-local-routees = on

        # Use members with specified role, or all members if undefined or empty.
        use-role = ""

      }

      # Protobuf serializer for cluster messages
      actor {
        serializers {
          akka-cluster = "Akka.Cluster.Proto.ClusterMessageSerializer, Akka.Cluster"
        }

        serialization-bindings {
          "Akka.Cluster.IClusterMessage, Akka.Cluster" = akka-cluster
        }
    
        router.type-mapping {
          adaptive-pool = "akka.cluster.routing.AdaptiveLoadBalancingPool"
          adaptive-group = "akka.cluster.routing.AdaptiveLoadBalancingGroup"
        }
      }
    }
"""

let system = System.create "system" <| config

type ActorMsg =
    | Hello of string
    | Hi

let echoServer1 = 
    spawn system "EchoServer"
    <| fun mailbox ->
        let rec replyEn() =
            actor {
                let! message = mailbox.Receive()
                match message with
                | Hello name -> printfn "Hi %s" name
                | Hi -> printfn "Hi!"

                return! replySw()
            } 
        and replySw() =
            actor {
                let! message = mailbox.Receive()
                match message with
                | Hello name -> printfn "Hallå %s" name
                | Hi -> printfn "Hallå!"

                return! replyEn()
            } 

        replyEn()

echoServer1 <! Hello "F# group!"
echoServer1 <! Hello "Akka.NET team!"

echoServer1 <! Hello "Akka.NET team!aaa"

//system.Shutdown()