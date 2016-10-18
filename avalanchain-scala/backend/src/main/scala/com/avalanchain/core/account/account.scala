package com.avalanchain.core

import java.util.UUID

import com.avalanchain.core.Certificate.EntityId
import com.avalanchain.core.domain.ChainStream.Proofed.Signed
import com.avalanchain.core.domain.{AcEvent, AcRegistryCommand, SignedEvent, SigningPublicKey}
import com.avalanchain.core.roles.PermissionType.{Allow, Deny, Nested}
import com.avalanchain.core.roles.RoleState.{Deleted, Empty, Invalid, Valid}

/**
  * Created by Yuriy on 10/10/2016.
  */
package object account {
  type AccountId = UUID
  type SignedAccountId = Signed[AccountId]

  sealed trait AccountCommand extends AcRegistryCommand
  final case class Add(accountId: SignedAccountId) extends AccountCommand
  final case class Block(accountId: SignedAccountId) extends AccountCommand
  final case class Invalidate(accountId: SignedAccountId) extends AccountCommand

  type AccountEvent = AcEvent[AccountCommand]
}

package object roles {

  trait Permission
  object Permission {
    final case class StringPermission(name: String) extends Permission
    final case class EntityPermission(name: String, entityId: EntityId) extends Permission
  }

  sealed trait PermissionType
  object PermissionType {
    case object Deny extends PermissionType
    case object Allow extends PermissionType
    case object Nested extends PermissionType
  }

  final case class ACL(permissions: Map[Permission, PermissionType]) {
    def + (that: ACL) = ACL((this.permissions.toSeq ++ that.permissions.toSeq).
                              groupBy(_._1).
                              mapValues(s => s match {
                                case Seq(x) => x._2
                                case Seq(x1, x2) => (x1._2, x2._2) match {
                                  case (Nested, a) => a
                                  case (a, Nested) => a
                                  case (Allow, Allow) => Allow
                                  case _ => Deny
                                }
                              }))
    def - (perm: Permission) = ACL(this.permissions - perm)
    def ? (perm: Permission) = this.permissions.getOrElse(perm, Nested)
  }

  sealed trait HierarchyNode[T] {
    def entity: T
    def acl: ACL
  }
  object HierarchyNode {
    final case class RootNode[T](entity: T, acl: ACL) extends HierarchyNode[T]
    final case class Node[T, P](entity: T, acl: ACL, parent: HierarchyNode[P]) extends HierarchyNode[T]
  }

  type UserId = UUID
  //type SignedUserId = Signed[UserId]

  sealed trait Principal
  //TODO: Claims?

  final case class RootAdmin(publicKey: SigningPublicKey)

  case class UserData(name: String) extends Principal // TODO: Extend UserData with more fields
  final case class User(userId: UserId, data: UserData) extends Principal
  type UserEvent = SignedEvent[UserCommand]

  sealed trait UserCommand extends AcRegistryCommand { def userId: UserId }
  object UserCommand {
    final case class Create(user: User, permissions: ACL) extends UserCommand { val userId = user.userId }
    final case class Update(user: User, oldData: UserData) extends UserCommand { val userId = user.userId }
    final case class Delete(userId: UserId, reason: String) extends UserCommand
    final case class Inactivate(userId: UserId, reason: String) extends UserCommand
    final case class Reactivate(userId: UserId) extends UserCommand
    final case class AddPermissions(userId: UserId, permissions: ACL) extends UserCommand
    final case class RemovePermission(userId: UserId, permission: Permission) extends UserCommand
    final case class AddRole(userId: UserId, roleId: RoleId) extends UserCommand
    final case class RemoveRole(userId: UserId, roleId: RoleId) extends UserCommand
  }

  sealed trait UserState { def userId: UserId }
  object UserState {
    final case class Valid(user: User, permissions: ACL) extends UserState { val userId = user.userId }
    final case class Invalid(userId: UserId) extends UserState
    final case class Deleted(userId: UserId, reason: String) extends UserState
  }
//  def applyUserEvents(userId: UserId, events: List[UserEvent]): Option[UserState] = {
//    events.map(_.value.value).filter(_.userId == userId).foldLeft (None) ((us, c) => (us, c) match {
//      case (Invalid(_), _) => us
//      case (Deleted(_, _), _) => us
//      case (Empty(_), RoleCommand.Create(role, acl)) => Valid(role, acl)
//      case (Empty(_), _) => Invalid(roleId)
//      case (Valid(role, acl), RoleCommand.Update(_, data)) => Valid(role.copy(data = data), acl)
//      case (Valid(_, _), RoleCommand.Delete(_, reason)) => Deleted(roleId, reason)
//      case (Valid(role, acl), RoleCommand.AddPermissions(_, newAcl)) => Valid(role, acl + newAcl)
//      case (Valid(role, acl), RoleCommand.RemovePermission(_, permission)) => Valid(role, acl - permission)
//      case _ => Invalid(roleId)
//    })
//  }


  type RoleId = UUID
  //type SignedRoleId = Signed[RoleId]
  type RoleEvent = SignedEvent[RoleCommand]

  case class RoleData(name: String, description: String) extends Principal
  final case class Role(roleId: RoleId, data: RoleData) extends Principal

  sealed trait RoleCommand extends AcRegistryCommand { def roleId: RoleId }
  object RoleCommand {
    final case class Create(role: Role, permissions: ACL) extends RoleCommand { val roleId = role.roleId }
    final case class Update(roleId: RoleId, data: RoleData) extends RoleCommand
    final case class Delete(roleId: RoleId, reason: String) extends RoleCommand
    final case class AddPermissions(roleId: RoleId, permissions: ACL) extends RoleCommand
    final case class RemovePermission(roleId: RoleId, permission: Permission) extends RoleCommand
  }

  sealed trait RoleState { def roleId: RoleId }
  object RoleState {
    final case class Empty(roleId: RoleId) extends RoleState
    final case class Valid(role: Role, permissions: ACL) extends RoleState { val roleId = role.roleId }
    final case class Invalid(roleId: RoleId) extends RoleState
    final case class Deleted(roleId: RoleId, reason: String) extends RoleState
  }
  def applyRoleEvents(roleId: RoleId, events: List[RoleEvent]): RoleState = {
    events.map(_.value.value).filter(_.roleId == roleId).foldLeft (Empty(roleId).asInstanceOf[RoleState]) ((rs, c) => (rs, c) match {
      case (Invalid(_), _) => rs
      case (Deleted(_, _), _) => rs
      case (Empty(_), RoleCommand.Create(role, acl)) => Valid(role, acl)
      case (Empty(_), _) => Invalid(roleId)
      case (Valid(role, acl), RoleCommand.Update(_, data)) => Valid(role.copy(data = data), acl)
      case (Valid(_, _), RoleCommand.Delete(_, reason)) => Deleted(roleId, reason)
      case (Valid(role, acl), RoleCommand.AddPermissions(_, newAcl)) => Valid(role, acl + newAcl)
      case (Valid(role, acl), RoleCommand.RemovePermission(_, permission)) => Valid(role, acl - permission)
      case _ => Invalid(roleId)
    })
  }
}
