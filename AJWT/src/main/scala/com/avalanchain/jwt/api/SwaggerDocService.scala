package com.avalanchain.jwt.api

import akka.actor.ActorSystem
import akka.stream.ActorMaterializer
import com.github.swagger.akka.model.Info
import com.github.swagger.akka.{HasActorSystem, SwaggerHttpService}

import scala.reflect.runtime.{universe => ru}


/**
  * Created by Yuriy on 22/11/2016.
  */
class SwaggerDocService(system: ActorSystem, httpInterface: String, httpPort: Int) extends SwaggerHttpService with HasActorSystem {
  override implicit val actorSystem: ActorSystem = system
  override implicit val materializer: ActorMaterializer = ActorMaterializer()
  override val apiTypes = Seq(ru.typeOf[NodeService], ru.typeOf[ChainService], ru.typeOf[AdminService], ru.typeOf[UsersService])
  override val host = s"$httpInterface:$httpPort" //the url of your api, not swagger's json endpoint
  override val basePath = "/v1"    //the basePath for the API you are exposing
  override val apiDocsPath = "api-docs" //where you want the swagger-json endpoint exposed
  override val info = Info(version = "1.0") //provides license and other description details
}
