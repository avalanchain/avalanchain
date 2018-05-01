namespace TypeShape.Tools

module Json =

    open System
    open System.Text
    open System.Text.RegularExpressions
    open FParsec
    open TypeShape.Core
    open TypeShape.Core.Utils

    // Toy Json Serializer for F# values adapted from 
    // http://www.quanttec.com/fparsec/tutorial.html#parsing-json

    type Parser<'T> = Parser<'T, unit>

    type JsonPickler<'T> =
        {
            Printer : StringBuilder -> 'T -> unit
            Parser : Parser<'T>
        }

    [<AutoOpen>]
    module PrinterImpl =
        
        let [<Literal>] nullStr = "null"

        let inline append (sb : StringBuilder) (t : string) = sb.Append t |> ignore

        let private escapeChars = Regex("[\n\r\"]", RegexOptions.Compiled)
        let private matchev = 
            MatchEvaluator(fun m -> 
                match m.Value with 
                | "\n" -> "\\n" 
                | "\r" -> "\\r" 
                | "\"" -> "\\\"" 
                | v -> v)

        let escapeStr (text:string) =
            let escaped = escapeChars.Replace(text, matchev)
            "\"" + escaped + "\""

        let printField (sb : StringBuilder) (label : string) printer value =
            append sb (escapeStr label)
            append sb " : "
            printer sb value

    [<AutoOpen>]
    module ParserImpl =
        let spaced p = between spaces spaces p

        let inline delay (f : unit -> 'T) : Parser<'T> =
            fun _ -> Reply(f())

        let (<*>) (f : Parser<'T -> 'S>) (t : Parser<'T>) : Parser<'S> = 
            fun stream ->
                let tr = t stream
                if tr.Status <> Ok then Reply(tr.Status, tr.Error) else
                let fr = f stream
                if fr.Status <> Ok then Reply(tr.Status, tr.Error) else
                Reply(fr.Result tr.Result)

        let nullLiteral t : Parser<'T> = stringReturn "null" t
        let boolLiteral : Parser<bool> =
            (stringReturn "true"  true) <|> (stringReturn "false" false)

        let numLiteral : Parser<float> = pfloat

        let stringLiteral : Parser<string> =
            let escape =  anyOf "\"\\/bfnrt"
                          |>> function
                              | 'b' -> "\b"
                              | 'f' -> "\u000C"
                              | 'n' -> "\n"
                              | 'r' -> "\r"
                              | 't' -> "\t"
                              | c   -> string c // every other char is mapped to itself

            let unicodeEscape =
                /// converts a hex char ([0-9a-fA-F]) to its integer number (0-15)
                let hex2int c = (int c &&& 15) + (int c >>> 6)*9

                pchar 'u' >>. pipe4 hex hex hex hex (fun h3 h2 h1 h0 ->
                    (hex2int h3)*4096 + (hex2int h2)*256 + (hex2int h1)*16 + hex2int h0
                    |> char |> string
                )

            let escapedCharSnippet = pchar '\\' >>. (escape <|> unicodeEscape)
            let normalCharSnippet  = manySatisfy (fun c -> c <> '"' && c <> '\\')

            between (pchar '\"') (pchar '\"')
                    (stringsSepBy normalCharSnippet escapedCharSnippet)

        let jsonArray parser =
            between (pchar '[') (pchar ']') (sepBy (spaced parser) (pchar ','))

        let jsonArraySimple parser =
            between (pchar '[') (pchar ']') (spaced parser)

        let jsonObj parser =
            between (pchar '{') (pchar '}') (spaced parser)

        let jsonField label parser = spaced (pstring (escapeStr label)) >>. pchar ':' >>. spaced parser


    /// Generates a json pickler for supplied type
    let rec genPickler<'T> () : JsonPickler<'T> =
        let ctx = new TypeGenerationContext()
        genPicklerCached<'T> ctx
        
    and genPicklerCached<'T> (ctx : TypeGenerationContext) : JsonPickler<'T> =
        // create a delayed uninitialized instance for recursive type definitions
        let delay (c : Cell<JsonPickler<'T>>) : JsonPickler<'T> =
            { Parser = fun s -> c.Value.Parser s ;
              Printer = fun sb -> c.Value.Printer sb }

        match ctx.InitOrGetCachedValue<JsonPickler<'T>> delay with
        | Cached(value = f) -> f
        | NotCached t ->
            let p = genPicklerAux<'T> ctx
            ctx.Commit t p
        
    and private genPicklerAux<'T> (ctx : TypeGenerationContext) : JsonPickler<'T> =
        let mkPickler
            (printer : StringBuilder -> 'a -> unit)
            (parser : Parser<'a>) : JsonPickler<'T> =
            { Printer = unbox printer ; Parser = spaced(unbox parser) }

        let mkMemberPickler hideLabels (shape : IShapeWriteMember<'Class>) =
            shape.Accept { new IWriteMemberVisitor<'Class, (StringBuilder -> 'Class -> unit) * Parser<'Class -> 'Class>> with

                member __.Visit (shape : ShapeWriteMember<'Class, 'Field>) =
                    let fP = genPicklerCached<'Field> ctx
                    let printer sb c = 
                        let field = shape.Project c
                        if hideLabels then fP.Printer sb field
                        else printField sb shape.Label fP.Printer field

                    let parser =    if hideLabels then spaced fP.Parser
                                    else jsonField shape.Label fP.Parser 
                    let parser = parser |>> fun f c -> shape.Inject c f
                    printer, parser
            }

        let combineMemberPicklers hideLabels (init : Parser<'Class>) (members : IShapeWriteMember<'Class> []) =
            let printers, parsers = members |> Array.map (mkMemberPickler hideLabels) |> Array.unzip
            let printer sb (c : 'Class) =
                for i = 0 to members.Length - 1 do
                    if i > 0 then append sb ", "
                    printers.[i] sb c

            let parser =
                match Array.toList parsers with
                | [] -> init
                | hd :: tl -> List.fold (fun acc p -> (pchar ',' >>. p) <*> acc) (hd <*> init) tl

            mkPickler printer parser

        let withObjBracketsPrinter(printer : StringBuilder -> 'T -> unit) = fun sb t -> append sb "{" ; printer sb t ; append sb "}"
        let withObjBrackets (p : JsonPickler<'T>) = { Parser = jsonObj p.Parser ; Printer = withObjBracketsPrinter p.Printer }

        let withArrayBracketsPrinter(printer : StringBuilder -> 'T -> unit) = fun sb t -> append sb "[" ; printer sb t ; append sb "]"
        let withArrayBrackets (p : JsonPickler<'T>) = { Parser = jsonArraySimple p.Parser ; Printer = withArrayBracketsPrinter p.Printer }
       
        match shapeof<'T> with
        | Shape.Unit -> mkPickler (fun sb _ -> append sb nullStr) (nullLiteral ())
        | Shape.Bool -> mkPickler (fun sb b -> append sb (if b then "true" else "false")) boolLiteral
        | Shape.Byte -> mkPickler (fun sb b -> append sb (b |> float |> string)) (numLiteral |>> byte)
        | Shape.Int32 -> mkPickler (fun sb i -> append sb (string i)) (numLiteral |>> int)
        | Shape.Int64 -> mkPickler (fun sb i -> append sb (string i)) (numLiteral |>> int64)
        | Shape.UInt32 -> mkPickler (fun sb i -> append sb (string i)) (numLiteral |>> uint32)
        | Shape.UInt64 -> mkPickler (fun sb i -> append sb (string i)) (numLiteral |>> uint64)
        | Shape.Double -> mkPickler (fun sb f -> append sb (string f)) (numLiteral |>> float)
        | Shape.Decimal -> mkPickler (fun sb f -> append sb (string f)) (numLiteral |>> decimal)
        | Shape.String -> mkPickler (fun sb str -> append sb (escapeStr str)) stringLiteral
        | Shape.Guid -> mkPickler (fun sb (guid: Guid) -> append sb (guid.ToString("N") |> escapeStr)) (stringLiteral |>> Guid.Parse)
        | Shape.DateTime -> mkPickler (fun sb (dt: DateTime) -> append sb (dt.ToString("o") |> escapeStr)) (stringLiteral |>> DateTime.Parse)
        | Shape.DateTimeOffset -> mkPickler (fun sb (dt: DateTimeOffset) -> append sb (dt.ToString("o") |> escapeStr)) (stringLiteral |>> DateTimeOffset.Parse)
        | Shape.FSharpOption s ->
            s.Accept {
                new IFSharpOptionVisitor<JsonPickler<'T>> with
                    member __.Visit<'t> () =
                        let tP = genPicklerCached<'t> ctx
                        let printer (sb : StringBuilder) (inp : 't option) =
                            match inp with
                            | None -> append sb nullStr
                            | Some t -> tP.Printer sb t

                        let nP = nullLiteral None
                        let sP = tP.Parser |>> Some
                        let parser = nP <|> sP
                        mkPickler printer parser
            }

        | Shape.FSharpList s ->
            s.Accept {
                new IFSharpListVisitor<JsonPickler<'T>> with
                    member __.Visit<'t> () =
                        let eP = genPicklerCached<'t> ctx
                        let printer sb (ts : 't list) =
                            append sb "["
                            match ts with
                            | [] -> ()
                            | hd :: tl ->
                                eP.Printer sb hd
                                for t in tl do 
                                    append sb ", "
                                    eP.Printer sb t

                            append sb "]"

                        let parser = jsonArray eP.Parser

                        mkPickler printer parser
            }

        | Shape.Array s when s.Rank = 1 ->
            s.Accept {
                new IArrayVisitor<JsonPickler<'T>> with
                    member __.Visit<'t> _ =
                        let eP = genPicklerCached<'t> ctx
                        let printer sb (ts : 't []) =
                            append sb "["
                            if ts.Length > 0 then
                                eP.Printer sb ts.[0]
                                for i in 1 .. ts.Length - 1 do
                                    append sb ", "
                                    eP.Printer sb ts.[i]

                            append sb "]"

                        let parser = jsonArray eP.Parser |>> Array.ofList

                        mkPickler printer parser
            }

        | Shape.FSharpMap s ->
            s.Accept {
                new IFSharpMapVisitor<JsonPickler<'T>> with
                    member __.Visit<'k,'v when 'k : comparison> () =
                        if typeof<'k> <> typeof<string> then failwithf "Type '%O' is not supported" typeof<'T>
                        let vp = genPicklerCached<'v> ctx
                        let printer sb (m : Map<string, 'v>) =
                            append sb "{"
                            let mutable first = true
                            for kv in m do
                                if first then first <- false else append sb ", "
                                append sb (escapeStr kv.Key)
                                append sb " : "
                                vp.Printer sb kv.Value

                            append sb "}"

                        let parser =
                            let keyValue = stringLiteral .>> spaced (pchar ':') .>>. vp.Parser
                            sepBy keyValue (spaced (pchar ',')) |>> Map.ofList |> jsonObj

                        mkPickler printer parser
            }

        | Shape.Tuple (:? ShapeTuple<'T> as shape) ->
            combineMemberPicklers true (delay shape.CreateUninitialized) shape.Elements
            |> withArrayBrackets

        | Shape.FSharpRecord (:? ShapeFSharpRecord<'T> as shape) ->
            combineMemberPicklers false (delay shape.CreateUninitialized) shape.Fields
            |> withObjBrackets

        | Shape.FSharpUnion (:? ShapeFSharpUnion<'T> as shape) ->
            let mkUnionCaseInfo (case : ShapeFSharpUnionCase<'T>) =
                //let hasFields = case.Fields.Length > 0
                let pickler = combineMemberPicklers true (delay case.CreateUninitialized) case.Fields
                let parser = (*if hasFields then spaced (pchar ',') >>. pickler.Parser else *) pickler.Parser
                let printer sb t = pickler.Printer sb t
                case, printer, parser

            let caseInfos = shape.UnionCases |> Array.map mkUnionCaseInfo
            //let labelParser label = spaced (pstring (escapeStr label))
            let parsers = 
                caseInfos 
                |> Array.map (fun (case,_,parser) -> 
                                match case.Fields.Length with
                                | 0 -> spaced (pstring (escapeStr case.CaseInfo.Name)) >>. parser
                                | 1 -> attempt(jsonObj (jsonField case.CaseInfo.Name parser))
                                | _ -> attempt(jsonObj (jsonField case.CaseInfo.Name (jsonArraySimple parser)))
                                )
            //let parsers = caseInfos |> Array.filter (fun (case,_,_) -> case.Fields.Length = 0) |> Array.map (fun (case,_,parser) -> spaced (pstring (escapeStr case.CaseInfo.Name)) >>. parser) 
            //let oneFieldParsers = caseInfos |> Array.filter (fun (case,_,_) -> case.Fields.Length = 1) |> Array.map (fun (case,_,parser) -> attempt(jsonObj (jsonField case.CaseInfo.Name parser)))
            //let defaultFieldParsers = caseInfos |> Array.filter (fun (case,_,_) -> case.Fields.Length = 1) |> Array.map (fun (case,_,parser) -> attempt(jsonObj (jsonField case.CaseInfo.Name parser)))
            
            // let zeroFieldParsers = shape.UnionCases |> Array.filter (fun case -> case.Fields.Length = 0) |> Array.map (fun case -> labelParser case.CaseInfo.Name)
            // let oneFieldParsers = caseInfos |> Array.filter (fun (case,_,_) -> case.Fields.Length = 1) |> Array.map (fun (case,_,parser) -> jsonField case.CaseInfo.Name fP.Parser |>> fun f c -> shape.Inject c f)

            {
                Printer = 
                    fun (sb:StringBuilder) (t:'T) ->
                        let tag = shape.GetTag t
                        let case, printer, _ = caseInfos.[tag]
                        let label = case.CaseInfo.Name
                        match case.Fields.Length with 
                        | 0 -> append sb (escapeStr label); printer sb t
                        //| 1 -> printField sb label printer t
                        | 1 -> withObjBracketsPrinter (fun sb t -> printField sb label printer t) sb t
                        | _ -> withObjBracketsPrinter (fun sb t -> printField sb label (withArrayBracketsPrinter printer) t) sb t

                //Parser = choice oneFieldParsers 
                Parser = choice parsers 
                    // jsonField "__case" stringLiteral >>= 
                    //     fun tag -> 
                    //         let t = shape.GetTag tag 
                    //         let _,_,parser = caseInfos.[t]
                    //         parser

            } //|> withObjBrackets

        | _ -> failwithf "unsupported type '%O'" typeof<'T>

    //-----------------------------------
    // Serialization functions

    let inline serialize (pickler : JsonPickler<'T>) (value : 'T) : string =
        let sb = new StringBuilder()
        pickler.Printer sb value
        sb.ToString()

    let inline deserialize (pickler : JsonPickler<'T>) (json : string) : 'T =
        match run pickler.Parser json with
        | Success(r,_,_) -> r
        | Failure(msg,_,_) -> failwithf "Parse error: %s" msg


