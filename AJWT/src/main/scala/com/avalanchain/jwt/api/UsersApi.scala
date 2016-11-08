package com.avalanchain.jwt.api

import akka.http.scaladsl.server.Directives
import com.avalanchain.jwt.utils.BearerTokenGenerator

trait UsersApi extends BearerTokenGenerator with Directives {
  val users1Routes =
    (path("users1" / "authentication") & post) {
      complete(generateSHAToken("InstagramPicsFilter"))
    }

  val usersRoutes =
    (path("users" / "authentication") & post) {
      complete(generateSHAToken("InstagramPicsFilter"))
    }

  val adminRoutes =
    (path("admin" / "generateKeys")) {
      complete(generateSHAToken("InstagramPicsFilter"))
    }


}
