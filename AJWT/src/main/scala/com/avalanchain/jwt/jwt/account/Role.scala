package com.avalanchain.jwt.jwt.account

import com.avalanchain.jwt.basicChain.JwtPayload
import Principal.{RoleEvent, RoleId}
import com.avalanchain.jwt.jwt.account.RoleCommand._

/**
  * Created by Yuriy on 06/12/2016.
  */

sealed trait RoleCommand extends JwtPayload.Sym { def roleId: RoleId }
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

