package com.avalanchain.cluster.HashStorage

import com.avalanchain.core.domain.{Hash, Hashed, HashedValue}

/**
  * Created by Yuriy Habarov on 03/11/2016.
  */
object HashStorageProtocol {

  sealed trait HashStorageMsg {
    val hash: Hash
  }

  sealed trait HashStorageCmd extends HashStorageMsg
  object HashStorageCmd {
    case class StoreValue(hashed: Hashed) extends HashStorageCmd { val hash = hashed.hash }
  }

  sealed trait HashStorageAck extends HashStorageMsg
  object HashStorage {
    case class Stored(hash: Hash) extends HashStorageAck
    case class Failed(hash: Hash, reason: String) extends HashStorageAck
  }

  sealed trait HashStorageQuery extends HashStorageMsg
  object HashStorageQuery {
    case class GetValue(hash: Hash) extends HashStorageQuery
  }

  sealed trait HashStorageQueryResponse extends HashStorageMsg
  object HashStorageQueryResponse {
    case class Found(hashed: Hashed) extends HashStorageQueryResponse { val hash = hashed.hash }
    case class ValueNotFound(hash: Hash) extends HashStorageQueryResponse
    case class ValueNotAccessable(hash: Hash) extends HashStorageQueryResponse
    case class TemporalFailure(hash: Hash) extends HashStorageQueryResponse
  }

}
