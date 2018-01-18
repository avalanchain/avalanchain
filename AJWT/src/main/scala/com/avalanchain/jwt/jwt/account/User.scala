package com.avalanchain.jwt.jwt.account

import com.avalanchain.jwt.basicChain.JwtPayload
import Principal.{RoleId, UserEvent, UserId}
import com.avalanchain.jwt.jwt.account.UserCommand._

/**
  * Created by Yuriy on 06/12/2016.
  */

sealed trait UserCommand extends JwtPayload.Sym { def userId: UserId }
object UserCommand {
  final case class Create(user: User, roles: Set[RoleId], permissions: ACL) extends UserCommand { val userId = user.userId }
  final case class Update(user: User, newData: UserData) extends UserCommand { val userId = user.userId }
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
      case (Valid(user, roles, acl), Update(_, newData)) => Valid(user.copy(data = newData), roles, acl)
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
