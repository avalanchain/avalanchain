namespace TypeShape.Tools

module Isomorph =

    open System
    open TypeShape.Core
    open TypeShape.Core.Utils

    //----------------------------------------------
    // Generic Isomorphism and Projection derivation

    type Iso<'a, 'b> = Iso of ('a -> 'b) * ('b -> 'a)
    and Proj<'a, 'b> = Proj of ('a -> 'b)


    let refl = Iso(id,id)
    let sym (Iso(f,g)) = Iso(g,f)
    let trans (Iso(f,g)) (Iso(f',g')) = Iso(f' << f, g << g')

    let convert1 (Iso (f,_)) x = f x
    let convert2 (Iso (_,g)) y = g y
    let project (Proj f) x = f x

    //----------------------------------
    // TypeShape - driven iso generation

    let rec mkIso<'a, 'b> () : Iso<'a, 'b> =
        let (Proj f), (Proj g) = mkProj<'a, 'b> (), mkProj<'b, 'a> ()
        Iso(f,g)

    and mkProj<'a, 'b> () : Proj<'a, 'b> =
        use ctx = new TypeGenerationContext()
        mkProjCached<'a, 'b> ctx

    and private mkProjCached<'a, 'b> (ctx : TypeGenerationContext) : Proj<'a, 'b> =
        match ctx.InitOrGetCachedValue<Proj<'a, 'b>>(fun c -> Proj(fun a -> let (Proj c) = c.Value in c a)) with
        | Cached(value = r) -> r
        | NotCached t ->
            let p = mkProjAux<'a, 'b> ctx
            ctx.Commit t p

    and private mkProjAux<'a, 'b> (ctx : TypeGenerationContext) : Proj<'a,'b> =
        let notProj() = failwithf "Type '%O' is not projectable to '%O'" typeof<'a> typeof<'b>

        let mkMemberProj (candidates : IShapeWriteMember<'a>[]) (target : IShapeWriteMember<'b>) =
            match candidates |> Array.tryFind (fun c -> c.Label = target.Label) with
            | None -> notProj()
            | Some source ->
                source.Accept { new IWriteMemberVisitor<'a, ('a -> 'b -> 'b)> with
                  member __.Visit (src : ShapeWriteMember<'a, 'F>) =
                    target.Accept { new IWriteMemberVisitor<'b, ('a -> 'b -> 'b)> with
                      member __.Visit (tgt : ShapeWriteMember<'b, 'G>) =
                        let (Proj conv) = mkProjCached<'F, 'G> ctx
                        fun (a:'a) (b:'b) -> 
                            let f = src.Project a
                            tgt.Inject b (conv f) } }

        match shapeof<'a>, shapeof<'b> with
        | s, s' when s.Type = s'.Type -> Proj(unbox<'a -> 'b> (fun (a:'a) -> a))
        | Shape.FSharpRecord (:? ShapeFSharpRecord<'a> as ar), 
          Shape.FSharpRecord (:? ShapeFSharpRecord<'b> as br) ->
            let memberProjs = br.Fields |> Array.map (mkMemberProj ar.Fields)
            fun (a:'a) ->
                let mutable b = br.CreateUninitialized()
                for m in memberProjs do b <- m a b
                b
            |> Proj

        | Shape.FSharpUnion (:? ShapeFSharpUnion<'a> as ar),
          Shape.FSharpUnion (:? ShapeFSharpUnion<'b> as br) ->

            let mkUnionCaseProj (source : ShapeFSharpUnionCase<'a>) =
                match br.UnionCases |> Array.tryFind (fun candidate -> candidate.CaseInfo.Name = source.CaseInfo.Name) with
                | Some target -> target.Fields |> Array.map (mkMemberProj source.Fields)
                | None -> notProj()

            let unionCaseMappers = ar.UnionCases |> Array.map mkUnionCaseProj

            fun (a:'a) ->
                let tag = ar.GetTag a
                let mutable b = br.UnionCases.[tag].CreateUninitialized()
                for m in unionCaseMappers.[tag] do b <- m a b
                b
            |> Proj

        | _ -> notProj()


