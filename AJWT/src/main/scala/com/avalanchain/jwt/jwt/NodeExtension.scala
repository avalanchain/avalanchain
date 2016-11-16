package com.avalanchain.jwt.jwt

import akka.actor.{Actor, ActorSystem, ExtendedActorSystem, Extension, ExtensionId, ExtensionIdProvider}

/**
  * Created by Yuriy on 14/10/2016.
  */
class NodeExtensionImpl extends Extension {
  //Since this Extension is a shared instance
  // per ActorSystem we need to be threadsafe
  //private val counter = new AtomicLong(0)

  //This is the operation this Extension provides
  def cryptoContext = CurveContext
}

object NodeExtension
  extends ExtensionId[NodeExtensionImpl]
    with ExtensionIdProvider {
  //The lookup method is required by ExtensionIdProvider,
  // so we return ourselves here, this allows us
  // to configure our extension to be loaded when
  // the ActorSystem starts up
  override def lookup = NodeExtension

  //This method will be called by Akka
  // to instantiate our Extension
  override def createExtension(system: ExtendedActorSystem) = new NodeExtensionImpl

  /**
    * Java API: retrieve the ACNode extension for the given system.
    */
  override def get(system: ActorSystem): NodeExtensionImpl = super.get(system)
}

trait NodeContext { self: Actor =>
  def cryptoContext = NodeExtension(context.system).cryptoContext
}
