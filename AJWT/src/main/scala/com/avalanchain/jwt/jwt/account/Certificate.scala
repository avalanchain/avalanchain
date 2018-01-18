package com.avalanchain.jwt.jwt.account

import java.security.PublicKey
import java.util.UUID

import com.avalanchain.jwt.KeysDto.PubKey
import com.avalanchain.jwt.basicChain.{JwtAlgo, JwtPayload, Position, TypedJwtToken}
import com.avalanchain.jwt.jwt.account.Certificate.CertificateCommand._
import com.avalanchain.jwt.jwt.account.Certificate.CertificateValidity._
import com.avalanchain.jwt.jwt.account.Principal.UserId


/**
  * Created by Yuriy on 06/12/2016.
  */
object Certificate {
  type ClockTick = Position
  type CertId = UUID
  type EntityId = UUID // TODO: Move closer to entity definition and relate to it pub key

  final case class CertificateData(id: CertId,
                                   entityId: EntityId,
                                   issuer: UserId,
                                   pub: PubKey,
//                                   hashAlgorithm: CryptographicHash,
//                                   signingAlgorithm: String,
                                   jwtAlgo: JwtAlgo,
                                   from: ClockTick,
                                   to: ClockTick) extends JwtPayload.Asym

  type Certificate = TypedJwtToken[CertificateData]
  //type Certificate = (Proofed, CertificateData)
  //type SignedCertId = Signed[CertId]

  //type CertificateRegistry = HashedRegistry[Certificate]

  sealed trait CertificateCommand extends Product with Serializable with JwtPayload.Asym {
    val certId: CertId
    val tick: ClockTick
  }
  object CertificateCommand {
    final case class Add(cert: Certificate, pub: PubKey, tick: ClockTick) extends CertificateCommand { val certId = cert.payload.get.entityId }
    final case class Invalidate(certId: CertId, pub: PubKey, tick: ClockTick) extends CertificateCommand
    final case class Refresh(cert: Certificate, pub: PubKey, tick: ClockTick) extends CertificateCommand { val certId = cert.payload.get.entityId }
    final case class RequestRefresh(certId: CertId, pub: PubKey, tick: ClockTick) extends CertificateCommand
  }
  type CertificateEvent = TypedJwtToken[CertificateCommand]

  sealed trait CertificateValidity extends Product with Serializable {
    def certId: CertId
  }
  object CertificateValidity {
    final case class NotExists(certId: CertId) extends CertificateValidity
    final case class Valid(certificate: Certificate, from: ClockTick, to: ClockTick) extends CertificateValidity { val certId = certificate.payload.get.entityId}
    final case class Expired(certificate: Certificate, expiredAt: ClockTick) extends CertificateValidity { val certId = certificate.payload.get.entityId}
    final case class Invalidated(certId: CertId, invalidationEvent: CertificateEvent) extends CertificateValidity //TODO: change to AcEvent[Invalidate]
    final case class Inconsistent(certId: CertId, reason: String) extends CertificateValidity
  }

  def activeCertificates(root: RootAdmin, certificates: Set[CertificateEvent], tick: ClockTick) = {
    def processCertificateEvents(certId: CertId, certEvents: Set[CertificateEvent]): CertificateValidity = {
      def applyEvents(cv: CertificateValidity, e: CertificateEvent): CertificateValidity =
        (cv, e.payload.get) match {
          case (NotExists(_), Add(cert, _, _)) =>
            if (cert.payload.get.from > tick) NotExists(certId)
            else if (cert.payload.get.to < tick) Expired(cert, cert.payload.get.to)
            else Valid(cert, cert.payload.get.from, cert.payload.get.to)
          case (Inconsistent(_, _), _) => cv
          case (Invalidated(_, _), _) => cv
          case (Expired(_, expiredAt), Refresh(cert, _, _)) =>
            if (cert.payload.get.from >= tick) Valid(cert, cert.payload.get.from, cert.payload.get.to)
            else cv
          case (Valid(_, _, _), Invalidate(_, _, _)) => Invalidated(certId, e)
          case (Valid(_, from, to), Refresh(cert, _, _)) => Valid(cert, (from :: cert.payload.get.from :: Nil).min, (to :: cert.payload.get.to :: Nil).max)
          case (Valid(_, _, _), Add(_, _, _)) => cv // ignoring reAdd
          case (_, RequestRefresh(_, _, _)) => cv // TODO: Rethink RequestRefresh processing
          // TODO: fix and finish the logic
          case _ => Inconsistent(certId, s"Logically inconsistent Certificate Event ${e} for state ${cv}")
        }
      val events = certEvents.toList.sortBy(_.payload.get.tick)
      val wrongCertIds = events.map(_.payload.get.certId).filterNot(_ == certId).mkString(", ")
      if (wrongCertIds.isEmpty) events.foldLeft(NotExists(certId).asInstanceOf[CertificateValidity]) (applyEvents)
      else Inconsistent(certId, s"Found events belong to different certificated Id: {wrongCertIds}")

    }

    val certVelidities =
      certificates.
        groupBy(_.payload.get.certId).toSeq.map(h => processCertificateEvents(h._1, h._2))

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





