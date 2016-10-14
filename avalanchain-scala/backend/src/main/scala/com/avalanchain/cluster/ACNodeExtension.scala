package com.avalanchain.cluster

import akka.actor.Extension
import akka.actor.ActorSystem
import akka.actor.ExtensionId
import akka.actor.ExtensionIdProvider
import akka.actor.ExtendedActorSystem


/**
  * Created by Yuriy on 14/10/2016.
  */
class ACNodeExtensionImpl extends Extension {
  //Since this Extension is a shared instance
  // per ActorSystem we need to be threadsafe
  //private val counter = new AtomicLong(0)

  //This is the operation this Extension provides
  //def increment() = counter.incrementAndGet()
}

object ACNodeExtension
  extends ExtensionId[ACNodeExtensionImpl]
    with ExtensionIdProvider {
  //The lookup method is required by ExtensionIdProvider,
  // so we return ourselves here, this allows us
  // to configure our extension to be loaded when
  // the ActorSystem starts up
  override def lookup = ACNodeExtension

  //This method will be called by Akka
  // to instantiate our Extension
  override def createExtension(system: ExtendedActorSystem) = new ACNodeExtensionImpl

  /**
    * Java API: retrieve the ACNode extension for the given system.
    */
  override def get(system: ActorSystem): ACNodeExtensionImpl = super.get(system)
}
