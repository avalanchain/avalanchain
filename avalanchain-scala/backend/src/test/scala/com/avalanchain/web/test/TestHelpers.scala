package com.avalanchain.web.test

import java.time.{OffsetDateTime, ZoneOffset}

import scala.util.Random

trait TestHelpers {

  val createdOn = OffsetDateTime.of(2015, 6, 3, 13, 25, 3, 0, ZoneOffset.UTC)

  private val random = new scala.util.Random
  private val characters = "abcdefghijklmnopqrstuvwxyz0123456789"
  def randomString(length: Int = 10) = Stream.continually(random.nextInt(characters.length)).map(characters).take(length).mkString
}
