package com.avalanchain.core

import com.avalanchain.core.domain.ChainStream.Hash
import com.avalanchain.core.domain.ExecutionPolicy

/**
  * Created by Yuriy Habarov on 19/04/2016.
  */
package object confirmation {
  type ValueId = String
  type ConfirmationStateChangedNotifier = Hash => Unit
  case class Confirmation (nodeId: String, valueId: ValueId, value: Hash, notifier: ConfirmationStateChangedNotifier)

  sealed trait ConfirmationResult {
    val confirmation: Confirmation
  }
  object ConfirmationResult {
    final case class InvalidConfirmation(confirmation: Confirmation) extends ConfirmationResult
    final case class ConfirmedSame(confirmation: Confirmation) extends ConfirmationResult
    final case class ConfirmedDifferent(confirmation: Confirmation, expectedHash: Hash) extends ConfirmationResult
    final case class NotConfirmedYet(confirmation: Confirmation) extends ConfirmationResult
  }

  type PolicyChecker = ExecutionPolicy => List[Confirmation] => Option[Hash]
  class ConfirmationCounter (val policy: ExecutionPolicy, validator: Confirmation => Boolean, policyChecker: PolicyChecker) {
    private var _confirmations = List.empty[Confirmation]
    private var _invalidConfirmations = List.empty[Confirmation]
    private var _pendingConfirmations = List.empty[Confirmation]
    private var _confirmedValue: Option[Hash] = None

    private def notifyDependents(): Unit = {
      _confirmedValue match {
        case Some(v) => _confirmations.foreach(c => c.notifier(v))
        case _ =>
      }
    }

    def confirmations = _confirmations
    def invalidConfirmations = _invalidConfirmations
    def pendingConfirmations = _pendingConfirmations
    def confirmedValue = _confirmedValue

    def addConfirmation(confirmation: Confirmation) = {
      if (!validator(confirmation)) {
        _invalidConfirmations = confirmation :: _invalidConfirmations
        ConfirmationResult.InvalidConfirmation(confirmation)
      }
      else {
        _confirmations = confirmation :: _confirmations
        _confirmedValue match {
          case Some(v) if v.equals(confirmation.value) => ConfirmationResult.ConfirmedSame(confirmation)
          case Some(v) => ConfirmationResult.ConfirmedDifferent(confirmation, v)
          case None => {
            _confirmedValue = policyChecker(policy) (_confirmations)
            _confirmedValue match {
              case Some(v) =>
                _confirmations = _pendingConfirmations
                _pendingConfirmations = List.empty[Confirmation]
                notifyDependents()
                if (v.equals(confirmation.value)) ConfirmationResult.ConfirmedSame(confirmation)
                else ConfirmationResult.ConfirmedDifferent(confirmation, v) // Shouldn't ever happen really because of policyChecker call
              case None =>
                _pendingConfirmations = confirmation :: _pendingConfirmations
                ConfirmationResult.NotConfirmedYet(confirmation)
            }
          }
        }
      }
    }
  }
}
