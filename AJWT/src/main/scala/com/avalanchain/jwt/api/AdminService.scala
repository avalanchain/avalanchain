package com.avalanchain.jwt.api

import java.security.KeyPair
import javax.ws.rs.Path

import akka.http.scaladsl.server.Directives
import com.avalanchain.jwt.jwt.CurveContext
import com.avalanchain.jwt.KeysDto._
import de.heikoseeberger.akkahttpcirce.CirceSupport
import io.swagger.annotations.{Api, ApiOperation, ApiResponse, ApiResponses}
import io.circe.generic.auto._
import io.circe.{Decoder, Encoder}


/**
  * Created by Yuriy on 22/11/2016.
  */
@Path("admin")
@Api(value = "/admin", produces = "application/json")
class AdminService(processStarter: () => Unit)
  extends Directives with CorsSupport /*with ACJsonSupport*/ with CirceSupport {

  val route = pathPrefix("admin") {
    path("newKeys") {
      newKeys
    } ~
    path("getKeys") {
      getKeys
    } ~
    path("key") {
      key
    } ~
    path("newNode") {
      newNode
    }
  }

  @Path("newKeys")
  @ApiOperation(notes = "Copy key from screen", httpMethod = "GET", response = classOf[Keys], value = "Returns a newly generated key pair")
  @ApiResponses(Array(
    new ApiResponse(code = 200, message = "New keys generated", response = classOf[Keys])
  ))
  def newKeys =
    get {
      complete {
        val keys = CurveContext.newKeys()
        toKeysDto(keys)
      }
    }

  @Path("getKeys")
  @ApiOperation(notes = "Copy key from screen", httpMethod = "GET", value = "Returns key pair currently used")
  @ApiResponses(Array(
    new ApiResponse(code = 200, message = "Current Key Pair", response = classOf[Keys])
  ))
  def getKeys =
    get {
      complete {
        val keys = CurveContext.savedKeys()
        toKeysDto(keys)
      }
    }

  @Path("key")
  @ApiOperation(notes = "Copy key from screen", httpMethod = "GET", value = "Returns current Public Key in use")
  @ApiResponses(Array(
    new ApiResponse(code = 200, message = "Current Public Key", response = classOf[PubKey])
  ))
  def key =
    get {
      complete {
        val keys: KeyPair = CurveContext.savedKeys()
        toPubKeyDto(keys.getPublic)
      }
    }

  @Path("newNode")
  @ApiOperation(notes = "Spawns a new node on the same machine with random port", httpMethod = "GET", value = "Spawns a new node")
  @ApiResponses(Array(
    new ApiResponse(code = 200, message = "Current Public Key", response = classOf[String])
  ))
  def newNode =
    get {
      complete {
        processStarter()
        "Node added"
      }
    }
}
