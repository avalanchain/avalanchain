package com.avalanchain.web.test

import akka.http.scaladsl.testkit.ScalatestRouteTest
import com.avalanchain.web.api.JsonSupport
import com.avalanchain.web.user.Session
import com.softwaremill.session.{SessionConfig, SessionManager}
import com.typesafe.config.ConfigFactory
import org.scalatest.Matchers

trait BaseRoutesSpec extends FlatSpecWithDb with ScalatestRouteTest with Matchers with JsonSupport { spec =>

  lazy val sessionConfig = SessionConfig.fromConfig(ConfigFactory.load()).copy(sessionEncryptData = true)

  implicit def mapCbs = CanBeSerialized[Map[String, String]]

  trait TestRoutesSupport {
    lazy val sessionConfig = spec.sessionConfig
    implicit def materializer = spec.materializer
    implicit def ec = spec.executor
    implicit def sessionManager = new SessionManager[Session](sessionConfig)
    implicit def refreshTokenStorage = null
  }
}
