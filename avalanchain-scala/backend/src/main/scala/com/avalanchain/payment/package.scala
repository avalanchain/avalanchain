package com.avalanchain

import java.time.Instant
import java.util.UUID

import com.avalanchain.core.domain.ChainStream.{Hash, Signature, SigningPublicKey}
import com.avalanchain.core.domain._

import scala.annotation.tailrec
import scala.concurrent.Future
import scala.util.Random

/**
  * Created by mytut on 19/04/2016.
  */
package object payment {

  case class PaymentAccountRef(address: String)

  //type HashedPT = SignedProof<PaymentTransaction>
  // TODO: Change to proper ref
  type PaymentAmount = BigDecimal
  type PaymentBalances = Map[PaymentAccountRef, PaymentAmount]

  trait PaymentAttempt
  case class PaymentTransaction(from: PaymentAccountRef, to: List[(PaymentAccountRef, PaymentAmount)]) extends PaymentAttempt
  trait PaymentRejection extends PaymentAttempt
  object PaymentRejection {
    case class WrongHash(hash: Hash) extends PaymentRejection
    case class WrongSignature(signature: Signature) extends PaymentRejection
    case class FromAccountNotExists(account: PaymentAccountRef) extends PaymentRejection
    case class ToAccountsMissing() extends PaymentRejection
    case class UnexpectedNonPositiveAmount(amount: PaymentAmount) extends PaymentRejection
    case class UnexpectedNonPositiveTotal(amount: PaymentAmount) extends PaymentRejection
    case class NotEnoughFunds(available: PaymentAmount, expected: PaymentAmount) extends PaymentRejection
  }

  //val pt = PaymentTransaction(PaymentAccountRef(""), List((PaymentAccountRef(""), BigDecimal(22))))

  case class StoredTransaction(payment: PaymentAttempt, balances: PaymentBalances, timeStamp: Instant)
  case class PaymentAccount(ref: PaymentAccountRef, publicKey: SigningPublicKey, name: String, cryptoContext: CryptoContext)

  trait TransactionStorage {
    def all: (PaymentBalances, List[StoredTransaction])
    // Initial balances + transactions
    def submit: PaymentTransaction => StoredTransaction
    def accountState: PaymentAccountRef => (Option[PaymentAmount], Seq[StoredTransaction])
    // Initial balances + account transactions
    def paymentBalances: PaymentBalances
    def accounts: List[PaymentAccount]
    def newAccount: PaymentAccount
  }

  def signatureChecker(transaction: PaymentTransaction) = transaction // TODO: Add check

  def applyTransaction(balances: PaymentBalances)(transaction: PaymentTransaction): StoredTransaction = {
    def reject(reason: PaymentRejection) = StoredTransaction(reason, balances, Instant.now())
    val total = transaction.to.map(_._2).sum
    balances.get(transaction.from) match {
      case None => reject(PaymentRejection.FromAccountNotExists(transaction.from))
      case Some(_) if total <= 0 => reject(PaymentRejection.UnexpectedNonPositiveTotal(total))
      case Some(value) =>
        value match {
          case v if v < total => reject(PaymentRejection.NotEnoughFunds(total, v))
          case v =>
            @tailrec def applyTos(blns: PaymentBalances, tos: List[(PaymentAccountRef, PaymentAmount)]): StoredTransaction = {
              tos match {
                case Nil => StoredTransaction(transaction, blns, Instant.now())
                case t :: _ if t._2 < 0 => reject(PaymentRejection.UnexpectedNonPositiveAmount(t._2))
                case t :: ts =>
                  val accountRef = t._1
                  val existingBalance = blns.get(accountRef)
                  val newToBlns = existingBalance match {
                    case None => blns.updated(accountRef, t._2)
                    case Some(eb) => blns.updated(accountRef, t._2 + eb)
                  }
                  val newFromBlns = newToBlns.updated(transaction.from, value - total)
                  applyTos(newFromBlns, ts)
              }
            }
            applyTos(balances, transaction.to)
        }
    }
  }

  val simpleContext = new CryptoContext {override def signer[T]: Signer[T] = ???
    override def signingPublicKey: SigningPublicKey = "SigningPublicKey"
    override def serializer[T]: Serializer[T] = t => t.toString
    override def hasher[T]: Hasher[T] = t => HashedValue("Hash", t.toString(), t)
  }

  def newAccount(name: String, cryptoContext: CryptoContext) = {
    val accountRef = PaymentAccountRef(name)
    PaymentAccount(accountRef, cryptoContext.signingPublicKey, name, cryptoContext)
  }

  def tradingBotAction (storage: TransactionStorage) = {
    val balances = storage.paymentBalances.toArray

    val fromAcc = balances(Random.nextInt(balances.length))
    val toAcc = balances(Random.nextInt(balances.length))

    storage.submit (PaymentTransaction(fromAcc._1, List((toAcc._1, (Random.nextDouble() * fromAcc._2 )))))
  }


  val accounts = List.tabulate(200)(_ => newAccount (UUID.randomUUID().toString(), simpleContext))
  val balances = accounts.map(a => (a.ref, 1000)).toMap
  val transactionStorage = TransactionStorage(accounts, balances)
  //let bot = tradingBot (transactionStorage) (new Random())
}
