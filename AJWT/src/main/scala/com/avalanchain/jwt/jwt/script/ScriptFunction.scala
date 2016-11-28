package com.avalanchain.jwt.jwt.script

import com.avalanchain.jwt.basicChain._
import com.avalanchain.jwt.utils.Pipe._
import io.circe.Json

import scala.util.control.NonFatal

/**
  * Created by Yuriy Habarov on 16/05/2016.
  */
class ScriptFunction(script: Func) {
  import javax.script.ScriptEngineManager
  import javax.script.Invocable

  private val engine = new ScriptEngineManager().getEngineByMimeType("text/javascript")
  engine.eval(s"var f = ($script);")
  engine.eval("function jw(ff, json) { var j = JSON.parse(json); var r = ff(j); return JSON.stringify(r); }; function jex(json) { return jw(f, json); }");
  private val invocable = engine.asInstanceOf[Invocable]

  def invoke(json: Json) = {
    try {
      val ret = invocable.invokeFunction("jex", json.asString.getOrElse("{}")).asInstanceOf[String]
      ret |> (Json.fromString)
    } catch {
      case NonFatal(ex) =>
        println(ex)
        s"""{ exception: ${ex.getMessage} }""" |> Json.fromString
    }
  }
}
object ScriptFunction {
  def apply(script: Func): Json => Json = {
    try {
      new ScriptFunction(script).invoke
    } catch {
      case NonFatal(ex) =>
        println(s"Exception during function compilation: ${ex.getMessage}")
        throw ex
    }
  }
}

class ScriptPredicate(script: Func) {
  import javax.script.ScriptEngineManager
  import javax.script.Invocable

  private val engine = new ScriptEngineManager().getEngineByMimeType("text/javascript")
  engine.eval(s"var f = ($script);")
  engine.eval("function jw(f, json) { var j = JSON.parse(json); var r = f(j); return r; }; function jex(json) { return jw(f, json); }");
  private val invocable = engine.asInstanceOf[Invocable]

  def invoke(json: Json) = {
    try {
      invocable.invokeFunction("jex", json.asString.getOrElse("{}")).asInstanceOf[Boolean]
    } catch {
      case NonFatal(ex) =>
        println(ex)
        false
    }
  }
}
object ScriptPredicate {
  def apply(script: Func): Json => Boolean = {
    try {
      new ScriptPredicate(script).invoke
    } catch {
      case NonFatal(ex) =>
        println(s"Exception during function compilation: ${ex.getMessage}")
        throw ex
    }
  }
}

class ScriptFunction2(script: Func) {
  import javax.script.ScriptEngineManager
  import javax.script.Invocable

  private val engine = new ScriptEngineManager().getEngineByMimeType("text/javascript")
  engine.eval(s"var f = ($script);")
  engine.eval("function jw(f, acc, json) { var j = JSON.parse(json); var aj = JSON.parse(acc); var r = f(acc, j); return r); }; function jex(json) { return jw(f, json); }");
  private val invocable = engine.asInstanceOf[Invocable]

  def invoke(accJson: Json, json: Json) = {
    try {
      invocable.invokeFunction("jex", accJson.asString.getOrElse("{}"), json.asString.getOrElse("{}")).asInstanceOf[String] |> (Json.fromString)
    } catch {
      case NonFatal(ex) =>
        println(ex)
        s"""{ exception: ${ex.getMessage} }""" |> Json.fromString
    }
  }
}
object ScriptFunction2 {
  def apply(script: Func): (Json, Json) => Json = {
    try {
      new ScriptFunction2(script).invoke
    } catch {
      case NonFatal(ex) =>
        println(s"Exception during function compilation: ${ex.getMessage}")
        throw ex
    }
  }
}