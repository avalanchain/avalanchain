package com.avalanchain.jwt.jwt

import io.swagger.annotations.ApiModel

/**
  * Created by yuriyhabarov on 26/10/2016.
  */
@ApiModel(description = "A User object")
case class UserInfo(firstName: String, lastName: String, emailAddress: String, phoneNumber: String)

