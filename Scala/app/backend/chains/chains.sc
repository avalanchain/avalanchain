import java.util.UUID

import ChainStream._

trait Node

trait ExecGroup

object ChainStream {
  type Id = UUID
  type Version = Long
  type Hash = Array[Byte]
  type Serialized = Array[Byte]
}



case class ChainRef(id: Id, ver: Version, hash: Hash)
