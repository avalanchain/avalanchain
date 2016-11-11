package com.avalanchain.jwt

import akka.actor.{ActorContext, ActorLogging, ActorRefFactory, Props}
import akka.persistence.{PersistentActor, SnapshotOffer}
import akka.stream.actor.{ActorSubscriber, MaxInFlightRequestStrategy}
import akka.stream.actor.ActorSubscriberMessage.OnNext
import akka.stream.scaladsl.Sink
import com.avalanchain.jwt.basicChain.{ChainState, FrameRef, FrameToken}

/**
  * Created by Yuriy on 10/11/2016.
  */
package object actors {

  object ChainPersistentActorSubscriber {
    object GetState
    object PrintState

    def props(pid: String) = Props(new ChainPersistentActorSubscriber(pid))
  }
  class ChainPersistentActorSubscriber(val pid: String) extends PersistentActor with ActorSubscriber with ActorLogging {

    val snapshotInterval: Int = 100
    val maxInFlight: Int = 10

    override def persistenceId = pid

    private var state: ChainState = ChainState(None, new FrameRef(""), -1) // TODO: Replace "" with chainRef.sig
    //saveFrame(state)

    private def applyToken(frameToken: FrameToken): ChainState = {
      ChainState(Some(frameToken), FrameRef(frameToken), frameToken.payload.get.pos)
    }

    private def updateState(chainState: ChainState): Unit = {
      state = chainState
      if (log.isDebugEnabled) {
        log.debug(s"State updated to $state, inFlight: ${inFlight}")
      }
    }

    private var inFlight = 0

    def currentState = state

    val receiveRecover: Receive = {
      case evt: ChainState => updateState(evt)
      case SnapshotOffer(_, snapshot: ChainState) => state = snapshot
    }

    def save(data: FrameToken) = {
      if (log.isDebugEnabled) {
        log.debug(s"Received $data, inFlight: ${inFlight}")
      }
      inFlight += 1
      persist(data) (e => {
        updateState(applyToken(e))
        if (e.payload.get.pos != 0 && e.payload.get.pos % snapshotInterval == 0) {saveSnapshot(currentState)}
        inFlight -= 1
      })
    }

    val receiveCommand: Receive = {
      case OnNext(data: FrameToken) => save(data)

      //    case ChainPersistentActor.PrintState => println(s"State: $state")
      //    case ChainPersistentActor.GetState => sender() ! state
      case "print" => println(s"State: $state")
      case "state" => sender() ! state
      case a => log.info(s"Ignored '$a'")
    }

    private def inf() = inFlight

    override val requestStrategy = new MaxInFlightRequestStrategy(max = maxInFlight) {
      override def inFlightInternally: Int = inFlight
    }

    log.info(s"Persistent Actor with id: '${persistenceId}' created.")
  }

  object ChainPersistentActor {
    trait Command
    object GetState extends Command
    object PrintState extends Command

    trait WithAck
    object Init extends WithAck
    object Ack extends WithAck
    object Complete extends WithAck

    def props(pid: String) = Props(new ChainPersistentActor(pid))
  }

  class ChainPersistentActor(val pid: String, val snapshotInterval: Int = 100) extends PersistentActor with ActorLogging {
    import ChainPersistentActor._

    override def persistenceId = pid

    private var state: ChainState = ChainState(None, new FrameRef(""), -1) // TODO: Replace "" with chainRef.sig

    private def applyToken(frameToken: FrameToken): ChainState = {
      ChainState(Some(frameToken), FrameRef(frameToken), frameToken.payload.get.pos)
    }

    private def updateState(chainState: ChainState): Unit = {
      state = chainState
      if (log.isDebugEnabled) {
        log.debug(s"State updated to $state")
      }
    }

    def currentState = state

    val receiveRecover: Receive = {
      case evt: ChainState => updateState(evt)
      case SnapshotOffer(_, snapshot: ChainState) => state = snapshot
    }

    def save(data: FrameToken) = {
      if (log.isDebugEnabled) {
        log.debug(s"Received $data")
      }
      persist(data) (e => {
        updateState(applyToken(e))
        if (e.payload.get.pos != 0 && e.payload.get.pos % snapshotInterval == 0) {saveSnapshot(currentState)}
        sender() ! Ack
      })
    }

    val receiveCommand: Receive = {
      case Init => sender() ! Ack
      case (data: FrameToken) => save(data)
      //case OnNext(data: FrameToken) => save(data)
      case Complete =>

      case ChainPersistentActor.PrintState | "print" => println(s"State: $state")
      case ChainPersistentActor.GetState | "state" => sender() ! state
      case a => {
        log.info(s"Ignored '${a}' '${a.getClass}'")
        sender() ! Ack
      }
    }

    log.info(s"Persistent Actor with id: '${persistenceId}' created.")
  }

  def PersistentSink[T](pid: String)(implicit actorRefFactory: ActorRefFactory) =
    Sink.actorRefWithAck[T](actorRefFactory.actorOf(ChainPersistentActor.props(pid), pid),
      ChainPersistentActor.Init, ChainPersistentActor.Ack, ChainPersistentActor.Complete)
}
