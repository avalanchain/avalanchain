package com.avalanchain.jwt.jwt.actors.network
import akka.actor.ActorRef
import com.avalanchain.jwt.basicChain._

/**
  * Created by Yuriy Habarov on 06/03/2017.
  */
trait ChainLogFactory {
  def chainLog(chainDefToken: ChainDefToken): ActorRef
  def commandLog(chainDefToken: ChainDefToken): ActorRef
}
