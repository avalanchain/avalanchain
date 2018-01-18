import java.nio.charset.{Charset, StandardCharsets}
import java.nio.file.{Files, Path, Paths, StandardOpenOption}
import java.security.{PrivateKey, PublicKey}

import pdi.jwt._
import java.time.Instant
import java.util.UUID
import java.time.OffsetDateTime
import java.time.format.DateTimeFormatter
import java.util.concurrent.ConcurrentHashMap

import com.avalanchain.jwt.jwt.{CurveContext, UserInfo}
import io.circe._
import io.circe.generic.auto._
import io.circe.parser._
import io.circe.syntax._

import scala.collection.immutable._
import scala.util.{Success, Try}
import collection.JavaConverters._
import CurveContext._
import akka.NotUsed
import akka.actor.ActorSystem
import akka.persistence.inmemory.query.scaladsl.InMemoryReadJournal
import akka.persistence.query.PersistenceQuery
import akka.stream.{ActorMaterializer, OverflowStrategy, SinkShape}
import akka.stream.scaladsl.{BroadcastHub, Flow, Keep, MergeHub, RunnableGraph, Sink, Source}
import com.avalanchain.jwt.KeysDto._
import com.avalanchain.jwt.basicChain._
import com.typesafe.config.ConfigFactory
import io.circe.generic.JsonCodec
import org.joda.time.DateTime
import org.joda.time.format.ISODateTimeFormat
import pdi.jwt.exceptions.JwtLengthException

import scala.collection.mutable.ArrayBuffer
import scala.concurrent.Await
import scala.concurrent.ExecutionContext.Implicits.global
import collection.JavaConverters._
import com.avalanchain.jwt.utils._

import scala.collection.mutable

implicit val system = ActorSystem("test", ConfigFactory.parseString(AkkaConfigs.PersLevelDb))
//implicit val system = ActorSystem("mySystem")
implicit val materializer = ActorMaterializer()

//Source(0 until 200).to(Sink.foreach(println)).run()

val result = time { 1 to 1000000 sum }

import scala.concurrent.duration._
import scala.concurrent._

val res = time {
  val r = Source(0 until 1000000).runWith(Sink.fold(0)(_+_))
  Await.result(r, 10 seconds)
}




val fts = new FileTokenStorage(Paths.get("""C:\tmp\AJWT\"""), UUID.randomUUID().toString, 1000, 1 second, system, materializer)
val cr = new ChainRegistry(savedKeys(), fts)
val nc = cr.newChain(JwtAlgo.HS512)

case class Data(int: Int, string: String, double: Double)

def runSimple() =
  Future {
    time {
      val r = Source(0 until 100000)
        .map(i => (i, nc.add(Data(i, s"Number ${i}", i * Math.PI).asJson)))
        .map(i => {
          if (i._1 % 100 == 99) println(i._1); i
        })
        .runWith(Sink.ignore)
      Await.result(r, 120 seconds)
    }
  }

runSimple()

def runPeriodic() =
  Future {
    time {
        var idx = 0
        def getIdx = {idx += 1; idx }
        val r = Source.tick(10 microseconds, 10 microseconds, getIdx)
        .map(i => (i, nc.add(Data(i, s"Number ${i}", i * Math.PI).asJson)))
        .map(i => {
          if (i._1 % 100 == 99) println(i._1); i
        })
        .runWith(Sink.ignore)
      Await.result(r, 120 seconds)
    }
  }

runPeriodic()

//fts.getFrom(0).runForeach(e => println(s"Token: '${e}'"))
   //println(s"Token count: ${fts.getFrom(0).toList.length}")

Future {
    fts.getFromSnapshot(1000, 2000).runForeach(e => println(s"Token: '${e}'"))
}


Future {
  val fts1 = new FileTokenStorage(Paths.get("""C:\tmp\AJWT\"""), UUID.randomUUID().toString, 1000, 1 second, system, materializer)
  val cr1 = new ChainRegistry(savedKeys(), fts1)
  val nc1 = cr1.newChain(JwtAlgo.HS512)

  fts.getFromSnapshot(0, 2000).map(e => nc1.add(e.payload.get.v)).runWith(Sink.ignore)
}


Future {
  val fts1 = new FileTokenStorage(Paths.get("""C:\tmp\AJWT\"""), UUID.randomUUID().toString + "_filter", 1000, 1 second, system, materializer)
  val cr1 = new ChainRegistry(savedKeys(), fts1)
  val nc1 = cr1.newChain(JwtAlgo.HS512)

  fts.getFrom(0, 2000).filter(e => e.payload.get.pos % 2 == 0).map(e => nc1.add(e.payload.get.v)).runWith(Sink.ignore)
}


Future {
  val fts1 = new FileTokenStorage(Paths.get("""C:\tmp\AJWT\"""), UUID.randomUUID().toString + "_filter2", 1000, 1 second, system, materializer)
  val cr1 = new ChainRegistry(savedKeys(), fts1)
  val nc1 = cr1.newChain(JwtAlgo.HS512)

  fts.getFrom(0, 2000).filter(e => e.payload.get.pos % 20 == 0).map(e => nc1.add(e.payload.get.v)).runWith(Sink.ignore)
}











//Future {
//  println("Starting parallel")
//  runMain()
//  println("End parallel")
//}



//val fts = new MapFrameTokenStorage()
//val cr = new ChainRegistry(savedKeys(), fts)
//val nc = cr.newChain()
//println(s"Token count: ${fts.frameTokens.toList.length}")
//
//case class Data(int: Int, string: String, double: Double)
//
//time {
//  val r = Source(0 until 1000000)
//    .map(i => (i, nc.add(Data(i, i.toString, i).asJson)))
//    .map(i => { if (i._1 % 100000 == 99999) println(i._1); i })
//    .runWith(Sink.ignore)
//  Await.result(r, 120 seconds)
//}
//
//println(s"Token count: ${fts.frameTokens.toList.length}")
//fts.frameTokens.foreach(e => println(s"Tokens key: '${e._1}', val: '${e._2}'"))






////type Hash = String
////type Sig = String
////
////sealed trait Shackle { val }
////object Shackle {
////  case class Seed(key: PubKey)
////  case class Frame(pos: Position, hash: Hash)
////}
//
//
//
//val user1 = UserInfo("John", "Smith", "john.smith@e.co.uk", "07711223344")
//
//case class T1(userInfo: UserInfo, str: String)
//val t1 = T1(user1, "asdasdasdas,mb,mb,mb,mb,mb,mbdasdasd")
//
//val content = t1.asJson.spaces2
//
//val token1 = Jwt.encode(content, privateKeyEC, JwtAlgorithm.ES512)
////JwtOptions.DEFAULT.
//
////val jwt = JwtToken(token1)
//
//Jwt.decode(token1, publicKeyEC, Seq(JwtAlgorithm.ES512))
//
//val aa = token1.split('.')
//println(aa)
//
//
//val (content2, token2) = encodeUser(privateKeyEC, user1)
//
////val user2 = decodeUser(publicKeyEC)(token1).get
//val userJson = Jwt.decodeRawAll(token1, publicKeyEC, Seq(JwtAlgorithm.ES512)).get
//val jsonHeader = userJson._1
//val jsonBody = userJson._2
//val signature = userJson._3
//
//signature.length
//
//
//val token3 = Jwt.encode(content, signature, JwtAlgorithm.HS512)
////JwtOptions.DEFAULT.
//
//val t3 = Jwt.decode(token3, signature, Seq(JwtAlgorithm.HS512))
