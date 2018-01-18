package com.avalanchain.jwt.jwt.account

import com.avalanchain.jwt.basicChain.ChainRef

/**
  * Created by Yuriy on 06/12/2016.
  */
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

