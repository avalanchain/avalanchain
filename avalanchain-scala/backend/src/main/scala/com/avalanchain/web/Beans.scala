package com.avalanchain.web

import akka.actor.ActorSystem
import com.avalanchain.web.config.{ServerConfig, CoreConfig}
import com.typesafe.config.ConfigFactory
import com.typesafe.scalalogging.StrictLogging

trait Beans extends StrictLogging {
  def system: ActorSystem

  lazy val config = new CoreConfig with ServerConfig {
    override def rootConfig = ConfigFactory.load()
  }

  lazy val daoExecutionContext = system.dispatchers.lookup("dao-dispatcher")

}
