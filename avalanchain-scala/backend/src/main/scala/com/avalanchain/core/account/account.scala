package com.avalanchain.core

import java.util.UUID

import com.avalanchain.core.domain.ChainStream.Proofed.Signed
import com.avalanchain.core.domain.{AcEvent, AcRegistryCommand, SigningPublicKey}
import com.avalanchain.core.roles.PermissionType.{Allow, Deny, Nested}

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
  final case class Permission(name: String)
  sealed trait PermissionType
  object PermissionType {
    final case object Deny extends PermissionType
    final case object Allow extends PermissionType
    final case object Nested extends PermissionType
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
  type SignedUserId = Signed[UserId]

  sealed trait Principal
  //TODO: Claims?

  final case class RootAdmin(publicKey: SigningPublicKey)

  final case class UserData(id: UserId, name: String) extends Principal
  type User = Signed[UserData]
  sealed trait UserCommand extends AcRegistryCommand
  object UserCommand {
    final case class Add(user: User) extends UserCommand
    final case class AddPermissions(userId: UserId, permissions: ACL) extends UserCommand
    final case class RemovePermission(userId: UserId, permission: Permission) extends UserCommand
    final case class Rename(user: User, oldName: String) extends UserCommand
  }

  type RoleId = UUID
  type SignedRoleId = Signed[RoleId]

  final case class RoleData(id: RoleId, name: String) extends Principal
  type Role = Signed[RoleData]

  sealed trait RoleCommand extends AcRegistryCommand
  object RoleCommand {
    final case class Add(role: Role) extends RoleCommand
    final case class AddPermission(certId: SignedRoleId) extends RoleCommand
  }
}
