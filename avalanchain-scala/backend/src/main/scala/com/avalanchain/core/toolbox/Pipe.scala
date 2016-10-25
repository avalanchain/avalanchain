package com.avalanchain.core.toolbox

/**
  * Created by Yuriy Habarov on 22/10/2016.
  */
//class Pipe[A](a: A) {
//  def |> [B](f: A => B) = f(a)
//}
//object Pipe {
//  def apply[A](v: A) = new Pipe(v)
//}
//object PipeOps {
//  implicit def toPipe[A](a: A) = Pipe(a)
//}

object Pipe {
  implicit def toPipe[A](x : A) = new {
    def |> [B](f : A => B) = f(x)
  }
  implicit def not (b: Boolean) = !b
}

//implicit class Pipe[T](val v: T) {
//  def |>[U](f: T => U) = f(v)
//}
