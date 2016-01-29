module Avalanchain.Cluster.Messages

type ShardId = string
type EntityId = string
type MessageBody = string

type ShardedMessage = { 
    ShardId: ShardId
    EntityId: EntityId
    Message: MessageBody
}