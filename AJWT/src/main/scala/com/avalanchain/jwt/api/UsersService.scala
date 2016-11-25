package com.avalanchain.jwt.api

import javax.ws.rs.Path

import akka.http.scaladsl.model.StatusCodes
import akka.http.scaladsl.server.Directives
import akka.util.Timeout
import com.avalanchain.jwt.jwt.account.principals.{User, UserData}
import de.heikoseeberger.akkahttpcirce.CirceSupport
import io.swagger.annotations._

import scala.concurrent.ExecutionContext
import scala.concurrent.duration._

import io.circe.generic.auto._
import io.circe.{Decoder, Encoder}

/**
  * Created by Yuriy on 22/11/2016.
  */
@Path("/users")
@Api(value = "/users", produces = "application/json")
class UsersService(getUsers: () => List[User], userExists: UserData => Boolean, addUser: UserData => User)
                  (implicit executionContext: ExecutionContext)
  extends Directives with CorsSupport /*with ACJsonSupport*/ with CirceSupport {

    implicit val timeout = Timeout(2 seconds)

    val route = pathPrefix("users") {
      getall ~ add
    }

    @ApiOperation(value = "Add User", notes = "", nickname = "addUser", httpMethod = "POST")
    @ApiImplicitParams(Array(
      new ApiImplicitParam(name = "body", value = "\"User\" to add", required = true,
        /*dataType = "com.avalanchain.jwt.jwt.account.principals.UserData", */ paramType = "body")
    ))
    @ApiResponses(Array(
      new ApiResponse(code = 201, message = "User added", response = classOf[User]),
      new ApiResponse(code = 409, message = "Internal server error")
    ))
    def add =
      post {
        entity(as[UserData]) { user =>
          val exists = userExists(user)
          if (exists) complete(StatusCodes.Conflict, None)
          else {
            val newUser = addUser(user)
            complete(StatusCodes.Created, Some(newUser))
          }
        }
      }

    @ApiOperation(httpMethod = "GET", response = classOf[List[User]], value = "Returns the list of registered Users")
    @ApiResponses(Array(
      new ApiResponse(code = 200, message = "Users registered", response = classOf[List[User]])
    ))
    def getall =
      get {
        completeWith(instanceOf[List[User]])(_(getUsers()))
      }
  }
