package com.avalanchain.jwt.jwt.account

import com.avalanchain.jwt.jwt.account.PermissionType.{Allow, Deny, Nested}

/**
  * Created by Yuriy on 06/12/2016.
  */
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
