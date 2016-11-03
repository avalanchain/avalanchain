package com.avalanchain.core

import java.util.UUID

import com.avalanchain.core.domain._
import com.avalanchain.core.toolbox.Pipe._

/**
  * Created by Yuriy Habarov on 25/10/2016.
  */
package object chain {

  final case class ChainRefData(id: Id, name: String, ver: Version = 0)
  type ChainRef = HashedValue[ChainRefData]

  //case class ChainRef (override val hash: Hash, override val bytes: Serialized, override val value: ChainRefData)
  //  extends HashedValue[ChainRefData](hash, bytes, value)
  //object ChainRef {
  //  def UID = this.hash.toString()
  //}

  final case class ChainDefData(ref: ChainRef, execGroups: Set[ExecGroup])
  type ChainDef = HashedValue[ChainDefData]

  //case class Data[T](value: HashedValue[T])

  final case class MerkledRef(streamRefHash: Hash, pmHash: Hash, pos: Version, ownHash: Hash)

  type HashedMR = HashedValue[MerkledRef]

  trait StateFrame[T] {
    val mref: HashedMR
    val value: Option[HashedValue[T]]

    def pos = mref.value.pos
  }

  object StateFrame {
    case class InitialFrame[T](override val mref: HashedMR, override val value: Option[HashedValue[T]]) extends StateFrame[T]
    case class Frame[T](override val mref: HashedMR, override val value: Option[HashedValue[T]]) // add proofs?
      extends StateFrame[T]
  }

  object FrameBuilder {
    def buildNestedRef(cr: ChainRef, nestedName: String)(implicit hasher: Hasher[ChainRefData]): ChainRef = {
      val data = cr.value
      val newData = data.copy(id = UUID.randomUUID(), name = data.name + "/" + nestedName, ver = data.ver)
      newData |> hasher
    }

    def buildInitialFrame[T](cr: ChainRef, initial: Option[T])(implicit hasherT: Hasher[T], hasherMR: Hasher[MerkledRef]): StateFrame[T] = {
      val hashed = initial.map(hasherT)
      val mr = MerkledRef(cr.hash, Hash.Zero, 0, hashed.map(_.hash).getOrElse(Hash.Zero))
      StateFrame.InitialFrame[T](hasherMR(mr), hashed).asInstanceOf[StateFrame[T]]
    }

    def buildFrame[T](cr: ChainRef, state: StateFrame[T], data: T)(implicit hasherT: Hasher[T], hasherMR: Hasher[MerkledRef]): StateFrame[T] = {
      val hashedData = hasherT(data)
      val mr = MerkledRef(cr.hash, state.mref.hash, state.pos + 1, hashedData.hash)
      StateFrame.Frame[T](hasherMR(mr), Some(hashedData))
    }
  }

  object ChainRefFactory {
    def chainRef(hasher: Hasher[ChainRefData], chainRefData: ChainRefData) = hasher(chainRefData)

    def nestedRef(hasher: Hasher[ChainRefData], chainRef: ChainRef, name: String, chainVersion: Version) = {
      hasher(ChainRefData(UUID.randomUUID(), s"${chainRef.value.name}#${chainRef.value.ver}\\$name", chainVersion))
    }
  }
}
