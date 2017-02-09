package com.avalanchain.jwt.api

import javax.ws.rs.Path

import akka.http.scaladsl.model.StatusCodes
import akka.http.scaladsl.server.Directives
import akka.stream.ActorMaterializer
import akka.stream.scaladsl.Sink
import akka.util.Timeout
import com.avalanchain.jwt.basicChain.{ChainDef, ChainDefToken}
import com.avalanchain.jwt.jwt.actors.network.NodeStatus
import com.avalanchain.jwt.jwt.actors.ChainNode
import de.heikoseeberger.akkahttpcirce.CirceSupport
import io.circe.syntax._
import io.circe.{Decoder, Encoder, KeyEncoder}
import io.circe.generic.auto._
import io.swagger.annotations.{Api, ApiOperation, ApiResponse, ApiResponses}

import scala.concurrent.{ExecutionContext, Future}
import scala.util.{Failure, Success}

/**
  * Created by Yuriy on 22/11/2016.
  */
@Path("nodes")
@Api(value = "/Nodes", produces = "application/json")
class NodeService(chainNode: ChainNode, processStarter: () => Unit) (implicit encoder: Encoder[ChainDef], decoder: Decoder[ChainDef], materializer: ActorMaterializer, executor: ExecutionContext)
  extends Directives with CorsSupport with CirceSupport {
  import scala.concurrent.duration._
  import NodeStatus._

  implicit val timeout = Timeout(2 seconds)

  implicit val fooKeyEncoder = new KeyEncoder[NodeStatus.Address] {
    override def apply(addr: NodeStatus.Address): String = addr.asJson.noSpaces
  }

  val route = pathPrefix("nodes") {
    path("getNodes") {
      getNodes
    } ~
    path("newNode") {
      newNode
    }
  }

  @Path("getNodes")
  @ApiOperation(httpMethod = "GET", response = classOf[List[ChainDefToken]], value = "Returns the list of known nodes")
  @ApiResponses(Array(
    new ApiResponse(code = 200, message = "Known Nodes", response = classOf[Map[NodeStatus.Address, NodeStatus]])
  ))
  def getNodes =
    get {
      onSuccess(chainNode.nodesSnapshot()) {
        m => completeWith(instanceOf[Map[NodeStatus.Address, NodeStatus]])(_(m))
      }
//      onComplete(chainNode.nodesSnapshot()) {
//        case Success(m) => completeWith(instanceOf[Map[NodeStatus.Address, NodeStatus]])(_(m))
//        case Failure(e) => failWith(new RuntimeException("Oops."))
//      }
    }

  @Path("newNode")
  @ApiOperation(notes = "Spawns a new node on the same machine with random port", httpMethod = "POST", value = "Spawns a new node")
  @ApiResponses(Array(
    new ApiResponse(code = 200, message = "Current Public Key", response = classOf[String])
  ))
  def newNode =
    post {
      complete {
        processStarter()
        "Node added"
      }
    }
}
