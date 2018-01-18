package com.avalanchain.jwt.jwt.account

import java.security.PublicKey
import java.util.UUID

import com.avalanchain.jwt.KeysDto.PubKey
import com.avalanchain.jwt.basicChain.{ChainRef, JwtPayload, TypedJwtToken}
import io.swagger.annotations.ApiModel

import scala.util.matching.Regex

/**
  * Created by Yuriy on 10/04/2016.
  */

package object permissions {


  import PermissionType._


  sealed trait HierarchyNode[T] {
    def entity: T

    def acl: ACL
  }

  object HierarchyNode {
    final case class RootNode[T](entity: T, acl: ACL) extends HierarchyNode[T]
    final case class Node[T, P](entity: T, acl: ACL, parent: HierarchyNode[P]) extends HierarchyNode[T]
  }


}

