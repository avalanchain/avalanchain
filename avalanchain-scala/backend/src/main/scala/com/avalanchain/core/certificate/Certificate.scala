package com.avalanchain.core

import java.util.UUID

import akka.actor.FSM
import akka.actor.FSM.{Event, StateTimeout}
import com.avalanchain.core.Certificate.CertificateCommand.{Add, Invalidate, Refresh, RequestRefresh}
import com.avalanchain.core.Certificate.CertificateValidity._
import com.avalanchain.core.domain._
import com.avalanchain.core.domain.Proofed.Signed
import com.avalanchain.core.principals.RootAdmin
import scorex.crypto.hash.CryptographicHash
import scorex.crypto.signatures.SigningFunctions

/**
  * Created by Yuriy Habarov on 09/10/2016.
  */


object Certificate {
  type CertId = UUID
  type EntityId = UUID // TODO: Move closer to entity definition and relate to it pub key

  final case class CertificateData(id: CertId,
                                   entityId: EntityId,
                                   Issuer: EntityId,
                                   publicKey: SigningPublicKey,
                                   hashAlgorithm: CryptographicHash,
                                   signingAlgorithm: SigningFunctions,
                                   from: ClockTick,
                                   to: ClockTick)

  type Certificate = Signed[CertificateData]
  type SignedCertId = Signed[CertId]

  type CertificateRegistry = HashedRegistry[Certificate]

  sealed trait CertificateCommand extends Product with Serializable with AcRegistryCommand {
    def certId: CertId
  }
  object CertificateCommand {
    final case class Add(certificate: Certificate) extends CertificateCommand { val certId = certificate.value.id }
    final case class Invalidate(signedCertId: SignedCertId) extends CertificateCommand { val certId = signedCertId.value }
    final case class Refresh(certificate: Certificate) extends CertificateCommand { val certId = certificate.value.id }
    final case class RequestRefresh(signedCertId: SignedCertId) extends CertificateCommand { val certId = signedCertId.value }
  }
  type CertificateEvent = AcEvent[CertificateCommand]

  sealed trait CertificateValidity extends Product with Serializable {
    def certId: CertId
  }
  object CertificateValidity {
    final case class NotExists(certId: CertId) extends CertificateValidity
    final case class Valid(certificate: Certificate, from: ClockTick, to: ClockTick) extends CertificateValidity { val certId = certificate.value.id}
    final case class Expired(certificate: Certificate, expiredAt: ClockTick) extends CertificateValidity { val certId = certificate.value.id}
    final case class Invalidated(certId: CertId, invalidationEvent: CertificateEvent) extends CertificateValidity //TODO: change to AcEvent[Invalidate]
    final case class Inconsistent(certId: CertId, reason: String) extends CertificateValidity
  }

  def activeCertificates(root: RootAdmin, certificates: Set[CertificateEvent], tick: ClockTick) = {
    def processCertificateEvents(certId: CertId, certEvents: Set[CertificateEvent]): CertificateValidity = {
      def applyEvents(cv: CertificateValidity, e: CertificateEvent): CertificateValidity =
        (cv, e.command) match {
          case (NotExists(_), Add(cert)) =>
            if (cert.value.from > tick) NotExists(certId)
            else if (cert.value.to < tick) Expired(cert, cert.value.to)
            else Valid(cert, cert.value.from, cert.value.to)
          case (Inconsistent(_, _), _) => cv
          case (Invalidated(_, _), _) => cv
          case (Expired(_, expiredAt), Refresh(cert)) =>
            if (cert.value.from >= tick) Valid(cert, cert.value.from, cert.value.to)
            else cv
          case (Valid(_, _, _), Invalidate(_)) => Invalidated(certId, e)
          case (Valid(_, from, to), Refresh(cert)) => Valid(cert, Math.min(from, cert.value.from), Math.max(to, cert.value.to))
          case (Valid(_, _, _), Add(_)) => cv // ignoring reAdd
          case (_, RequestRefresh(_)) => cv // TODO: Rethink RequestRefresh processing
          // TODO: fix and finish the logic
          case _ => Inconsistent(certId, s"Logically inconsistent Certificate Event ${e} for state ${cv}")
        }
      val events = certEvents.toList.sortBy(_.tick)
      val wrongCertIds = events.map(_.command.certId).filterNot(_ == certId).mkString(", ")
      if (wrongCertIds.isEmpty) events.foldLeft(NotExists(certId).asInstanceOf[CertificateValidity]) (applyEvents)
      else Inconsistent(certId, s"Found events belong to different certificated Id: {wrongCertIds}")

    }

    val certVelidities =
      certificates.
        groupBy(_.command.certId).toSeq.map(h => processCertificateEvents(h._1, h._2))

    certVelidities
  }


//  sealed trait CertificateState
//  object CertificateState {
//    case object NotAddedYet extends CertificateState
//    case object Added extends CertificateState
//    case object Expired extends CertificateState
//    case object Invalid extends CertificateState
//  }
//  class CertificateFSM(certId: SignedCertId) extends FSM[CertificateState, CertificateState] {
//
//    startWith(NotAddedYet, NotAddedYet)
//
//    when(NotAddedYet) {
//      case Event(SetTarget(ref), Uninitialized) =>
//        stay using Todo(ref, Vector.empty)
//    }
//
//    // transition elided ...
//
//    when(Active, stateTimeout = 1 second) {
//      case Event(Flush | StateTimeout, t: Todo) =>
//        goto(Idle) using t.copy(queue = Vector.empty)
//    }
//
//    // unhandled elided ...
//
//    initialize()
//  }
}




