package com.avalanchain.core.certificate

import java.util.UUID

import com.avalanchain.core.certificate.Certificate.{CertId, EntityId}
import com.avalanchain.core.domain.ChainStream.{Signed, SigningPublicKey}
import com.avalanchain.core.domain.{AcRegistryAction, AcRegistryEvent, HashedRegistry}
import scorex.crypto.hash.CryptographicHash
import scorex.crypto.signatures.SigningFunctions

/**
  * Created by Yuriy Habarov on 09/10/2016.
  */

final case class CertificateData(id: CertId, entityId: EntityId, Issuer: EntityId, publicKey: SigningPublicKey, hashAlgorithm: CryptographicHash, signingAlgorithm: SigningFunctions)

object Certificate {
  type CertId = UUID
  type EntityId = UUID // TODO: Move closer to entity definition and relate to it pub key
  type Certificate = Signed[CertificateData]

  type CertificateRegistry = HashedRegistry[Certificate]

  sealed trait CertificateAction extends AcRegistryAction

  type CertificateEvent = AcRegistryEvent[CertificateAction]

}




