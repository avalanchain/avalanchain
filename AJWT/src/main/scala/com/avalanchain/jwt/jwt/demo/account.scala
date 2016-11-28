package com.avalanchain.jwt.jwt.demo

import java.util.UUID

import com.avalanchain.jwt.KeysDto.PubKey
import com.avalanchain.jwt.basicChain.{JwtPayload, TypedJwtToken}

/**
  * Created by Yuriy Habarov on 28/11/2016.
  */
package object account {
  type AccountId = UUID
  //type SignedAccountId = Signed[AccountId]

  case class Account(accountId: AccountId, pubAcc: PubKey, pub: PubKey) extends JwtPayload.Asym

  sealed trait AccountCommand extends JwtPayload.Sym { def accountId: AccountId }
  final case class Add(accountId: AccountId) extends AccountCommand
  final case class Block(accountId: AccountId) extends AccountCommand
  final case class Invalidate(accountId: AccountId) extends AccountCommand

  type AccountEvent = TypedJwtToken[AccountCommand]

  type PaymentAmount = BigDecimal
  type PaymentBalances = Map[AccountId, PaymentAmount]

  case class AccountState(account: Account, balance: PaymentAmount) extends JwtPayload.Sym
  type AccountStates = Map[AccountId, AccountState]

  trait PaymentAttempt
  case class PaymentTransaction(from: AccountId, to: AccountId) extends PaymentAttempt
  trait PaymentRejection extends PaymentAttempt
  object PaymentRejection {
    case class WrongSignature(sig: String) extends PaymentRejection
    case class FromAccountNotExists(account: AccountId) extends PaymentRejection
    case class ToAccountMissing(account: AccountId) extends PaymentRejection
    case class UnexpectedNonPositiveAmount(amount: PaymentAmount) extends PaymentRejection
    case class NotEnoughFunds(available: PaymentAmount, expected: PaymentAmount) extends PaymentRejection
  }


}
