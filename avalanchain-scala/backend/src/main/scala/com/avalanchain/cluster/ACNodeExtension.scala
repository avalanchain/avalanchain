package com.avalanchain.cluster

import akka.actor.{Actor, ActorSystem, ExtendedActorSystem, Extension, ExtensionId, ExtensionIdProvider}
import com.avalanchain.core.builders.{CryptoContextBuilder, CryptoContextSettingsBuilder}
import com.avalanchain.core.domain.{PrivateKey, PublicKey}
import com.avalanchain.toolbox.Pipe._


/**
  * Created by Yuriy on 14/10/2016.
  */
class ACNodeExtensionImpl extends Extension {
  //Since this Extension is a shared instance
  // per ActorSystem we need to be threadsafe
  //private val counter = new AtomicLong(0)

  //This is the operation this Extension provides
  def cryptoContext = {
    implicit val ccs = CryptoContextSettingsBuilder.CryptoContextSettings
    val priv = "BHpiB7Zpanb76Unue5bqFaiVD3atAQY4EBi1CzpBvNns" |> (ccs.hexed2Bytes) |> (PrivateKey(_))
    val pub = "8rAwg7esrUog6UhWJWfrzY91cnhXf4LeaaH3J79aS2ug" |> (ccs.hexed2Bytes) |> (PublicKey(_))
    CryptoContextBuilder.createCryptoContext(priv, pub, Set("8rAwg7esrUog6UhWJWfrzY91cnhXf4LeaaH3J79aS2ug"))
  }
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

trait ACNodeContext { self: Actor =>
  def cryptoContext = ACNodeExtension(context.system).cryptoContext
}
