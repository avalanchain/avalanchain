package com.avalanchain.jwt.jwt.script

import com.avalanchain.jwt.basicChain._
import io.circe.Json

/**
  * Created by Yuriy Habarov on 16/11/2016.
  */
class ScriptFunction(script: Func) {
  import javax.script.ScriptEngineManager
  import javax.script.Invocable

  private val engine = new ScriptEngineManager().getEngineByMimeType("text/javascript")
  engine.eval(s"var f = ($script);")
  engine.eval("function jw(f, json) { var j = JSON.parse(json); var r = f(j); return JSON.stringify(r); }; function jex(json) { return jw(f, json); }");
  private val invocable = engine.asInstanceOf[Invocable]

  def invoke(json: Json) = Json.fromString(invocable.invokeFunction("jex", json.noSpaces).asInstanceOf[JsonStr])
}
object ScriptFunction {
  def apply(script: Func): Json => Json = new ScriptFunction(script).invoke
}

class ScriptPredicate(script: Func) {
  import javax.script.ScriptEngineManager
  import javax.script.Invocable

  private val engine = new ScriptEngineManager().getEngineByMimeType("text/javascript")
  engine.eval(s"var f = ($script);")
  engine.eval("function jw(f, json) { var j = JSON.parse(json); var r = f(j); return r; }; function jex(json) { return jw(f, json); }");
  private val invocable = engine.asInstanceOf[Invocable]

  def invoke(json: Json) = invocable.invokeFunction("jex", json.noSpaces).asInstanceOf[Boolean]
}
object ScriptPredicate {
  def apply(script: Func): Json => Boolean = new ScriptPredicate(script).invoke
}

class ScriptFunction2(script: Func) {
  import javax.script.ScriptEngineManager
  import javax.script.Invocable

  private val engine = new ScriptEngineManager().getEngineByMimeType("text/javascript")
  engine.eval(s"var f = ($script);")
  engine.eval("function jw(f, acc, json) { var j = JSON.parse(json); var aj = JSON.parse(acc); var r = f(acc, j); return JSON.stringify(r); }; function jex(json) { return jw(f, json); }");
  private val invocable = engine.asInstanceOf[Invocable]

  def invoke(accJson: Json, json: Json) = Json.fromString(invocable.invokeFunction("jex", accJson.noSpaces, json.noSpaces).asInstanceOf[JsonStr])
}
object ScriptFunction2 {
  def apply(script: Func): (Json, Json) => Json = new ScriptFunction2(script).invoke
}