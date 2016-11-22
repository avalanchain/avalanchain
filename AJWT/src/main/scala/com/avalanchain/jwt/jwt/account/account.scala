package com.avalanchain.jwt.jwt.account

import java.security.PublicKey
import java.util.UUID

import com.avalanchain.jwt.basicChain.{ChainRef, JwtPayload, TypedJwtToken}
import com.avalanchain.jwt.jwt.account.permissions.{Permission, UserId}
import io.swagger.annotations.ApiModel

import scala.util.matching.Regex

/**
  * Created by Yuriy on 10/04/2016.
  */
package object account {
  type AccountId = UUID
  //type SignedAccountId = Signed[AccountId]

  sealed trait AccountCommand extends JwtPayload.Sym { def accountId: AccountId }
  final case class Add(accountId: AccountId) extends AccountCommand
  final case class Block(accountId: AccountId) extends AccountCommand
  final case class Invalidate(accountId: AccountId) extends AccountCommand

  type AccountEvent = TypedJwtToken[AccountCommand]

  sealed trait AccountState { def accountId: AccountId }
  object AccountState {

//    final case class Empty(userId: UserId) extends AccountState
//    final case class Valid(user: User, roles: Set[RoleId], permissions: ACL) extends AccountState { val userId = user.userId }
//    final case class Invalid(userId: UserId, reason: String) extends AccountState
//    final case class Deleted(userId: UserId, reason: String) extends AccountState

//    def applyUserEvents(userId: UserId, events: List[UserEvent], roleValidator: RoleId => Boolean): AccountState = {
//      events.map(_.value.value).filter(_.userId == userId).foldLeft (Empty(userId).asInstanceOf[UserState]) ((us, c) => (us, c) match {
//        case (Invalid(_, _), _) => us
//        case (Deleted(_, _), _) => us
//        case (Empty(_), Create(user, roles, acl)) =>
//          val validRoles = roles.filter(roleValidator)
//          Valid(user, validRoles, acl) // TODO: Add warning on invalid roles
//        case (Empty(_), _) => Invalid(userId, "The only allowed event for an empty User State is 'Create'")
//        case (Valid(user, roles, acl), Update(_, data)) => Valid(user.copy(data = data), roles, acl)
//        case (Valid(_, _, _), Delete(_, reason)) => Deleted(userId, reason)
//        case (Valid(user, roles, acl), AddPermissions(_, newAcl)) => Valid(user, roles, acl + newAcl)
//        case (Valid(user, roles, acl), RemovePermission(_, permission)) => Valid(user, roles, acl - permission)
//        case (Valid(user, roles, acl), AddRole(userId, roleId)) =>
//          if (roleValidator(roleId)) Valid(user, roles + roleId, acl)
//          else Invalid(userId, s"Invalid role Id '$roleId'")
//        case (Valid(user, roles, acl), RemoveRole(userId, roleId)) => Valid(user, roles - roleId, acl)
//        case (state, event) => Invalid(userId, s"Event '$event' is not allowed for User State '${state.getClass.getName}'")
//      })
//    }
  }

}

package object permissions {

  trait Permission

  object Permission {
    final case class StringPermission(name: String) extends Permission
    final case class EntityPermission(name: String, entityId: ChainRef) extends Permission
    final case class EntityRegexPermission(name: String, entityIdPattern: String /*Regex*/) extends Permission // TODO: ???
  }

  sealed trait PermissionType

  object PermissionType {
    case object Deny extends PermissionType
    case object Allow extends PermissionType
    case object Nested extends PermissionType
  }

  import PermissionType._

  final case class ACL(permissions: Map[Permission, PermissionType]) {
    def +(that: ACL) = ACL((this.permissions.toSeq ++ that.permissions.toSeq).
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

    def -(perm: Permission) = ACL(this.permissions - perm)

    def ?(perm: Permission) = this.permissions.getOrElse(perm, Nested)
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

}

package object principals {
  import com.avalanchain.jwt.jwt.account.permissions.ACL

  sealed trait Principal
  //TODO: Claims?

  final case class RootAdmin(publicKey: PublicKey)

  @ApiModel(description = "A User Data object")
  case class UserData(userName: String, firstName: String, lastName: String, emailAddress: String, ipAddress: String)

  @ApiModel(description = "A User object")
  final case class User(userId: UserId, data: UserData) extends Principal

  sealed trait UserCommand extends JwtPayload.Sym { def userId: UserId }
  object UserCommand {
    final case class Create(user: User, roles: Set[RoleId], permissions: ACL) extends UserCommand { val userId = user.userId }
    final case class Update(user: User, oldData: UserData) extends UserCommand { val userId = user.userId }
    final case class Delete(userId: UserId, reason: String) extends UserCommand
    final case class Inactivate(userId: UserId, reason: String) extends UserCommand
    final case class Reactivate(userId: UserId) extends UserCommand
    final case class AddPermissions(userId: UserId, permissions: ACL) extends UserCommand
    final case class RemovePermission(userId: UserId, permission: Permission) extends UserCommand
    final case class AddRole(userId: UserId, roleId: RoleId) extends UserCommand
    final case class RemoveRole(userId: UserId, roleId: RoleId) extends UserCommand
  }
  type UserEvent = TypedJwtToken[UserCommand]

  sealed trait UserState { def userId: UserId }
  object UserState {
    import com.avalanchain.jwt.jwt.account.principals.UserCommand._

    final case class Empty(userId: UserId) extends UserState
    final case class Valid(user: User, roles: Set[RoleId], permissions: ACL) extends UserState { val userId = user.userId }
    final case class Invalid(userId: UserId, reason: String) extends UserState
    final case class Deleted(userId: UserId, reason: String) extends UserState

    def applyUserEvents(userId: UserId, events: List[UserEvent], roleValidator: RoleId => Option[(Role, ACL)]): UserState = {
      events.map(_.payload.get).filter(_.userId == userId).foldLeft (Empty(userId).asInstanceOf[UserState]) ((us, c) => (us, c) match {
        case (Invalid(_, _), _) => us
        case (Deleted(_, _), _) => us
        case (Empty(_), Create(user, roles, acl)) =>
          val validRoles = roles.filter(roleValidator(_).nonEmpty)
          Valid(user, validRoles, acl) // TODO: Add warning on invalid roles
        case (Empty(_), _) => Invalid(userId, "The only allowed event for an empty User State is 'Create'")
        case (Valid(user, roles, acl), Update(_, data)) => Valid(user.copy(data = data), roles, acl)
        case (Valid(_, _, _), Delete(_, reason)) => Deleted(userId, reason)
        case (Valid(user, roles, acl), AddPermissions(_, newAcl)) => Valid(user, roles, acl + newAcl)
        case (Valid(user, roles, acl), RemovePermission(_, permission)) => Valid(user, roles, acl - permission)
        case (Valid(user, roles, acl), AddRole(userId, roleId)) =>
          roleValidator(roleId) match {
            case Some(role) => Valid (user, roles + roleId, acl + role._2)
            case None => Invalid (userId, s"Invalid role Id '$roleId'")
          }
        case (Valid(user, roles, acl), RemoveRole(userId, roleId)) => Valid(user, roles - roleId, acl)
        case (state, event) => Invalid(userId, s"Event '$event' is not allowed for User State '${state.getClass.getName}'")
      })
    }
  }


  type RoleId = UUID
  //type SignedRoleId = Signed[RoleId]

  case class RoleData(name: String, description: String) extends Principal
  final case class Role(roleId: RoleId, data: RoleData) extends Principal

  sealed trait RoleCommand extends JwtPayload.Sym { def roleId: RoleId }
  object RoleCommand {
    final case class Create(role: Role, permissions: ACL) extends RoleCommand { val roleId = role.roleId }
    final case class Update(roleId: RoleId, data: RoleData) extends RoleCommand
    final case class Delete(roleId: RoleId, reason: String) extends RoleCommand
    final case class AddPermissions(roleId: RoleId, permissions: ACL) extends RoleCommand
    final case class RemovePermission(roleId: RoleId, permission: Permission) extends RoleCommand
  }
  type RoleEvent = TypedJwtToken[RoleCommand]

  sealed trait RoleState { def roleId: RoleId }
  object RoleState {
    import com.avalanchain.jwt.jwt.account.principals.RoleCommand._

    final case class Empty(roleId: RoleId) extends RoleState
    final case class Valid(role: Role, permissions: ACL) extends RoleState { val roleId = role.roleId }
    final case class Invalid(roleId: RoleId, reason: String) extends RoleState
    final case class Deleted(roleId: RoleId, reason: String) extends RoleState

    def applyRoleEvents(roleId: RoleId, events: List[RoleEvent]): RoleState = {
    events.map(_.payload.get).filter(_.roleId == roleId).foldLeft (Empty(roleId).asInstanceOf[RoleState]) ((rs, c) => (rs, c) match {
        case (Invalid(_, _), _) => rs
        case (Deleted(_, _), _) => rs
        case (Empty(_), Create(role, acl)) => Valid(role, acl)
        case (Empty(_), _) => Invalid(roleId, "The only allowed event for an empty Role State is 'Create'")
        case (Valid(role, acl), Update(_, data)) => Valid(role.copy(data = data), acl)
        case (Valid(_, _), Delete(_, reason)) => Deleted(roleId, reason)
        case (Valid(role, acl), AddPermissions(_, newAcl)) => Valid(role, acl + newAcl)
        case (Valid(role, acl), RemovePermission(_, permission)) => Valid(role, acl - permission)
        case (state, event) => Invalid(roleId, s"Event '$event' is not allowed for User State '${state.getClass.getName}'")
      })
    }
  }
}
