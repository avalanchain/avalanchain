namespace Avalanchain
open Avalanchain.Core
open Avalanchain.Core.ChainDefs
open Avalanchain.Core.Chains

module ChainNetwork = 

    module Network =
        type Endpoint = {
            IP: string
            Port: uint16
        }

    open Network

    type ACClusterId = Uid

    type ACCluster = {
        CId: ACClusterId
    }

    type ACNodeId = 
        | NR  of NodeRef 
        | MNR of MasterNodeRef  

    type ACNode = {
        NId     : ACNodeId
        Chains  : Set<ChainDef>
        Endpoint: Endpoint
        Cluster : ACClusterId option 
    }

    type ACNodeState = Active | Passive | Banned

    type ACClusterMembership = {
        Cluster : ACCluster
        Nodes   : Map<ACNode, ACNodeState> 
    }

    module NodeManagement =
        type ChainStatus = 
            | Active 
            | Passive
            | Stopped
            | Blocked of Reason: string 
            | Banned

        type ChainState = {
            Def: ChainDef
            Status: ChainStatus
        }

        type Commands = 
            | AddChain      of Nid: ACNodeId * Chain    : ChainDef
            | RegisterChain of Nid: ACNodeId * ChainRef : ChainRef
            | StopChain     of Nid: ACNodeId * ChainRef : ChainRef
            | ListChains    of Map<ChainRef, ChainState>


    module ClusterManagement =
        type Commands = 
            | AddNode       of ClusterId: ACClusterId * Node: ACNode
            | RemoveNode    of ClusterId: ACClusterId * Nid : ACNodeId
            | ListNodes     of ACNode list

