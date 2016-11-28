package com.avalanchain.jwt.api

import javax.ws.rs.Path

import akka.http.scaladsl.model.StatusCodes
import akka.http.scaladsl.server.Directives
import akka.util.Timeout
import com.avalanchain.jwt.basicChain.{ChainDef, ChainDefToken}
import com.avalanchain.jwt.jwt.actors.ChainNode
import de.heikoseeberger.akkahttpcirce.CirceSupport
import io.circe.generic.auto._
import io.circe.{Decoder, Encoder}
import io.swagger.annotations.{Api, ApiOperation, ApiResponse, ApiResponses}

/**
  * Created by Yuriy on 22/11/2016.
  */
@Path("chains")
@Api(value = "/chains", produces = "application/json")
class ChainService(chainNode: ChainNode)(implicit encoder: Encoder[ChainDef], decoder: Decoder[ChainDef])
  extends Directives with CorsSupport with CirceSupport {
  import scala.concurrent.duration._

  implicit val timeout = Timeout(2 seconds)

  //val a = List[ChainDefToken]().asJson

  val route = pathPrefix("chains") {
    path("allChains") {
      allChains
    } ~
    path("newChain") {
      newChain
    }
  }

  @Path("newChain")
  @ApiOperation(value = "Create New Chain", notes = "", nickname = "newchain", httpMethod = "POST")
  @ApiResponses(Array(
    new ApiResponse(code = 201, message = "Chain created", response = classOf[ChainDefToken]),
    new ApiResponse(code = 409, message = "Internal server error")
  ))
  def newChain =
    post {
      onSuccess(chainNode.newChain2()) { node =>
        complete(StatusCodes.Created, node.chainDefToken)
      }
    }

  @Path("allChains")
  @ApiOperation(httpMethod = "GET", response = classOf[List[ChainDefToken]], value = "Returns the list of active chains")
  @ApiResponses(Array(
    new ApiResponse(code = 200, message = "Active Chains", response = classOf[List[ChainDefToken]])
  ))
  def allChains =
    get {
      onSuccess(chainNode.chains()) { chains =>
        completeWith(instanceOf[List[ChainDefToken]])(_ (chains.values.toList))
      }
    }
}
