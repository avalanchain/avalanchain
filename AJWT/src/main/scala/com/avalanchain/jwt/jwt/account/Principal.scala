package com.avalanchain.jwt.jwt.account

import java.security.PublicKey
import java.util.UUID

import com.avalanchain.jwt.KeysDto.PubKey
import com.avalanchain.jwt.basicChain.TypedJwtToken
import com.avalanchain.jwt.jwt.account.Principal.{RoleId, UserId}
import io.swagger.annotations.ApiModel

/**
  * Created by Yuriy Habarov on 28/11/2016.
  */

sealed trait Principal
//TODO: Claims?
object Principal {
  type UserId = UUID
  //type SignedUserId = Signed[UserId]

  type RoleId = UUID
  //type SignedRoleId = Signed[RoleId]

  type UserEvent = TypedJwtToken[UserCommand]
  type RoleEvent = TypedJwtToken[RoleCommand]
}

@ApiModel(description = "A Superuser")
final case class RootAdmin(pubKey: PubKey)

@ApiModel(description = "A User Data object")
case class UserData(userName: String, firstName: String, lastName: String, emailAddress: String, ipAddress: String)
@ApiModel(description = "A User object")
final case class User(userId: UserId, data: UserData) extends Principal

@ApiModel(description = "A Role Data object")
case class RoleData(name: String, description: String)
@ApiModel(description = "A Role object")
final case class Role(roleId: RoleId, data: RoleData) extends Principal
