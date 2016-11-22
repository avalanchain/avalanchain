package com.avalanchain.jwt

import com.avalanchain.jwt.api.back.UsersApi
;

trait Routes extends UsersApi {
  val routes = pathPrefix("v1") {
    usersRoutes
  } ~ path("")(getFromResource("public/index.html"))
}
