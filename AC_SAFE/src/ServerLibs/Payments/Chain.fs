namespace Avalanchain.Core

module Chain =
    open System

    type Hash = Guid // TODO: Redo this
    type Sig = { Sig: string } // TODO: Redo this

    type Uid = 
    | UUID of Guid
    | Hash of Hash
    | Sig of Sig

    type NodeRef = { Nid: string }
    type MasterNodeRef = { MNid: string }

    type NodeProof = { NRef: NodeRef; Sig: Sig }
    type MasterNodeProof = { MRef: MasterNodeRef; Sig: Sig }

    type ChainRef = { Cid: string }

    type Asset = { Asset: string }

    // type DVVClock = {
    //     Nodes: Map<NodeRef, Pos>
    // }