import java.util.UUID

import akka.actor.{Actor, ActorRef, ActorSelection, ActorSystem, Props}
import akka.persistence.AtLeastOnceDelivery
import akka.stream.ActorMaterializer
import akka.stream.actor.ActorSubscriberMessage.OnNext
import akka.stream.actor.{ActorSubscriber, ActorSubscriberMessage, MaxInFlightRequestStrategy}
import akka.stream.scaladsl.{Flow, Sink, Source}

case class Msg(deliveryId: UUID, pos: Long, s: String)
case class Confirm(deliveryId: UUID)

sealed trait Evt
case class MsgSent(s: String) extends Evt
case class MsgConfirmed(deliveryId: UUID) extends Evt

class ExactlyOnceDeliverySender(destination: ActorSelection, startingPos: Long) extends ActorSubscriber {
  import ActorSubscriberMessage._

  private sealed trait DeliveryState
  private object Sent extends DeliveryState
  private object Confirmed extends DeliveryState
  private object Failed extends DeliveryState


  val MaxQueueSize = 10
  var queue = Map.empty[Int, ActorRef]
  var deliveryStatus = Map.empty[UUID, DeliveryState]
//  val persistence = context.actorOf(Props(new ChainPersistentActor[T](node, chainRef, snapshotInterval, initial)), name = "persistence")

  override val requestStrategy = new MaxInFlightRequestStrategy(max = MaxQueueSize) {
    override def inFlightInternally: Int = queue.size
  }

  def receive = {
    case OnNext((pos: Long, str: String)) =>
      val deliveryId = UUID.randomUUID()
      val msg = Msg(deliveryId, pos, str)
      deliver(msg)
  }

  def deliver(msg: Msg): Unit = {

  }
}
//  override def receive: Receive = {
//    case s: String           => updateState(MsgSent(s))
//    case Confirm(deliveryId) => updateState(MsgConfirmed(deliveryId))
//  }
//
//  def updateState(evt: Evt): Unit = evt match {
//    case MsgSent(s) =>
//      deliver(destination)(deliveryId => Msg(deliveryId, s))
//
//    case MsgConfirmed(deliveryId) => confirmDelivery(deliveryId)
//  }
//}

class ExactlyOnceDeliveryReceiver extends Actor {
  def receive = {
    case Msg(deliveryId, s) =>
      println(s"Received: $s")
      sender() ! Confirm(deliveryId)
  }
}

implicit val system = ActorSystem()
implicit val materializer = ActorMaterializer()

val logicLogic = Flow[String].map(_ + 1)

Source(1 to 10).map(_.toString).via(logicLogic).runForeach(println)

val receiver = system.actorOf(Props[AtLeastOnceDeliveryReceiver])
val selection = system.actorSelection(receiver.path)
val sender = system.actorOf(Props(new AtLeastOnceDeliverySender(selection)))
