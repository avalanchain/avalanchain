package com.avalanchain.jwt.jwt.account.principals

import java.security.PublicKey
import java.util.UUID

import com.avalanchain.jwt.basicChain.{ChainRef, JwtPayload, TypedJwtToken}
import com.avalanchain.jwt.jwt.account.permissions.{Permission, UserId}
import io.swagger.annotations.ApiModel

import scala.util.matching.Regex
import com.avalanchain.jwt.jwt.account.permissions.ACL

/**
  * Created by Yuriy Habarov on 28/11/2016.
  */
