#r "../packages/Jint/lib/net45/jint.dll"

open System
open Jint

let engine = (Engine())
engine.SetValue("log", new Action<obj>(Console.WriteLine))
    

#time

for i in 0 .. 999 do 
    engine.Execute(@"
      function hello() { 
        log('Hello World');
      };
      
      hello();
    ") |> ignore

for i in 0 .. 999999 do 
    engine.Execute(@"
      function hello() { 
        2 + 2;
      };
      
      hello();
    ") |> ignore