package com.avalanchain.jwt

/**
  * Created by Yuriy on 10/11/2016.
  */
package object utils {
  def time[R](block: => R): R = {
    val t0 = System.nanoTime()
    val result = block    // call-by-name
    val t1 = System.nanoTime()
    println(s"Elapsed time: ${(t1 - t0)} ns or ${(t1 - t0)/1000000} ms")
    result
  }

  object Pipe {
    implicit def toPipe[A](x : A) = new {
      def |> [B](f : A => B) = f(x)
    }
    implicit def not (b: Boolean) = !b
  }
}
