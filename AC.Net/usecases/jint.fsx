#r "../packages/FSharp.Interop.Dynamic/lib/portable-net45+sl50+win/FSharp.Interop.Dynamic.dll"
#r "../packages/Jint/lib/net45/jint.dll"

open System
open FSharp.Interop.Dynamic
open Jint

let engine = (Engine())
engine.SetValue("log", new Action<obj>(Console.WriteLine))
    

#time

let f = engine.Execute("""function A(a, b) { 
                            log('a + b = ' + (a + b));
                          }""").GetValue("A")

let ret = f.Invoke ([|1.; 2.|] |> Array.map (Native.JsValue))

let f1 = engine.Json.Put("val1", Native.JsValue(2.), true)

engine.Json.Get("val1")

let json = """{
              "CONTRATE": 0,
              "SALINC": 0,
              "RETAGE": 55.34,
              "MARSTATUS": "single",
              "SPOUSEDOB": "1970-01-01",
              "VIEWOPTION": "pension"
            }"""

let func1 = """function (a) { 
              var ret = (a + 1);
              log('a + 1 = ' + ret);
              return ret;
            }"""

let func2 = """function (a) { 
              var ret = (a.CONTRATE + 1);
              log('a + 1 = ' + ret);
              a.CONTRATE2 = a.CONTRATE + 1;
              return a;
            }"""

// let caller =  sprintf """(func(par0))()"""         

// let input = Jint.JsonParser(engine).Parse(json);

let result = engine.
              SetValue("par0", 2.).
              Execute("""var func = """ + func1 +
                      """;
                      (func(par0));""").
              GetCompletionValue()

let funcMap = 
    engine.
        SetValue("par0", json).
        Execute("""var func = """ + func2 +
                """;
                var json = JSON.parse(par0);
                var ret = (function(j) { return func(j); })(json);
                var retJson = JSON.stringify(ret);
                // log(retJson);
                retJson""").
            GetCompletionValue()

engine.Execute("""retJson""").GetCompletionValue()


// for i in 0 .. 999 do 
//     engine.Execute(@"
//       function hello() { 
//         log('Hello World');
//       };
      
//       hello();
//     ") |> ignore

// for i in 0 .. 999999 do 
//     engine.Execute(@"
//       function hello() { 
//         2 + 2;
//       };
      
//       hello();
//     ") |> ignore