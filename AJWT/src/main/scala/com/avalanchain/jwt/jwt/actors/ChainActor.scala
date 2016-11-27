package com.avalanchain.jwt.jwt.actors

import java.security.KeyPair

import akka.actor.{Actor, ActorLogging, ActorRefFactory}
import com.avalanchain.jwt.basicChain.JwtAlgo.{ES512, HS512}
import com.avalanchain.jwt.basicChain._
import io.circe.Json

import scala.util.Try

/**
  * Created by Yuriy Habarov on 26/11/2016.
  */
//class ChainActor (val chainDefToken: ChainDefToken, val keyPair: KeyPair)(implicit actorRefFactory: ActorRefFactory) extends Actor with ActorLogging {

//    if (chainDefToken.payload.isEmpty) throw new RuntimeException(s"Inconsistent ChainDefToken: '$chainDefToken'")
//    val chainRef = ChainRef(chainDefToken)
//    def status = ChainStatus.Created
//
//    //    def pos: Position = state.pos
//    //    def current: Option[FrameToken] = state.frame
//
//    private var state = currentState.getOrElse(ChainState(None, FrameRef(chainRef.sig), -1))
//
//    def add(v: Json): Try[Unit] = {
//      val newPos = state.pos + 1
//      val frameToken: TypedJwtToken[Frame] = chainDefToken.payload.get.algo match {
//        case HS512 => TypedJwtToken[FSym](FSym(chainRef, newPos, state.lastRef, v), state.lastRef.sig).asInstanceOf[TypedJwtToken[Frame]]
//        case ES512 => TypedJwtToken[FAsym](FAsym(chainRef, newPos, state.lastRef, v, keyPair.getPublic), keyPair.getPrivate).asInstanceOf[TypedJwtToken[Frame]]
//      }
//      tokenStorage.add(frameToken).map(_ => {
//        state = ChainState(Some(frameToken), FrameRef(frameToken), newPos)
//      })
//    }
//    //
//    //    def sink() =
//    //      PersistentFrameSink(chainRef)(actorRefFactory, 5 seconds)
//  }
//  object Chain {
//
//  }

//}
