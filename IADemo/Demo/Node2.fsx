#load "../.paket/load/net461/main.group.fsx"

#load "lib/ws.fs"

open System
open System.IO
#if INTERACTIVE
let cd = Path.Combine(__SOURCE_DIRECTORY__, "bin/Debug/net461")
System.IO.Directory.SetCurrentDirectory(cd)
#I "bin/Debug/net461"
#endif

