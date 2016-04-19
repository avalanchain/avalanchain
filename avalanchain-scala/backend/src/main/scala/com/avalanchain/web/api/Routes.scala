package com.avalanchain.web.api

import akka.http.scaladsl.model.StatusCodes
import akka.http.scaladsl.server.Directives._
import akka.http.scaladsl.server.{RejectionHandler, Route, ExceptionHandler}
import com.avalanchain.web.passwordreset.PasswordResetRoutes
import com.avalanchain.web.user.UsersRoutes

trait Routes extends UsersRoutes
    with PasswordResetRoutes
    with CacheSupport {

  private val exceptionHandler = ExceptionHandler {
    case e: Exception =>
      logger.error(s"Exception during client request processing: ${e.getMessage}", e)
      _.complete(StatusCodes.InternalServerError, "Internal server error")
  }

  private val rejectionHandler = RejectionHandler.default
  private val logDuration = extractRequestContext.flatMap { ctx =>
    val start = System.currentTimeMillis()
    // handling rejections here so that we get proper status codes
    mapResponse { resp =>
      val d = System.currentTimeMillis() - start
      logger.info(s"[${resp.status.intValue()}] ${ctx.request.method.name} ${ctx.request.uri} took: ${d}ms")
      resp
    } & handleRejections(rejectionHandler)
  }

  val routes =
    logDuration {
      handleExceptions(exceptionHandler) {
        cacheImages {
          encodeResponse {
            pathPrefix("api") {
              passwordResetRoutes ~
                usersRoutes
            } ~
              getFromResourceDirectory("webapp") ~
              path("") {
                getFromResource("webapp/index.html")
              }
          }
        }
      }
    }
}
