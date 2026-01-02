namespace Falco

open System
open System.Collections.Generic
open System.Net
open Microsoft.AspNetCore.Http
open Microsoft.AspNetCore.Routing
open Microsoft.FSharp.Core.Operators
open Falco.StringPatterns

type RequestValue =
    | RNull
    | RBool of bool
    | RNumber of float
    | RString of string
    | RList of elements : RequestValue list
    | RObject of keyValues : (string * RequestValue) list

module RequestValue =
    let private (|IsFlatKey|_|) (x : string) =
        if not(x.EndsWith("[]")) && not(x.Contains(".")) then Some x
        else None

    let private (|IsListKey|_|) (x : string) =
        if x.EndsWith("[]") then Some (x.Substring(0, x.Length - 2))
        else None

    let private (|IsIndexedListKey|_|) (x : string) =
        if x.EndsWith("]") then
            match Text.RegularExpressions.Regex.Match(x, @".\[(\d+)\]$") with
            | m when Seq.length m.Groups = 2 ->
                let capture = m.Groups[1].Value
                Some (int capture, x.Substring(0, x.Length - capture.Length - 2))
            | _ -> None
        else None

    let rec parse (requestData : IDictionary<string, string seq>) : RequestValue =
        let newRequestAcc () =
            Dictionary<string, RequestValue>()

        let requestAccToValues (x : Dictionary<string, RequestValue>) =
            x |> Seq.map (fun (kvp) -> kvp.Key, kvp.Value) |> List.ofSeq |> RObject

        let requestDatasToAcc (x : (string * RequestValue) list) =
            let acc = newRequestAcc()
            for (key, value) in x do
                acc.TryAdd(key, value) |> ignore
            acc

        let parseRequestPrimitive (x : string) =
            match x with
            | IsNullOrWhiteSpace _ -> RNull
            | IsTrue x
            | IsFalse x -> RBool x
            | IsFloat x -> RNumber x
            | x -> RString x

        let parseRequestPrimitiveList values =
            values
            |> Seq.map parseRequestPrimitive
            |> List.ofSeq
            |> RList

        let parseRequestPrimitiveSingle values =
            values
            |> Seq.tryHead
            |> Option.map parseRequestPrimitive
            |> Option.defaultValue RNull

        let rec parseNested (acc : Dictionary<string, RequestValue>) (keys : string list) (values : string seq) =
            match keys with
            | [] -> ()
            | [IsListKey key] ->
                // list of primitives
                values
                |> parseRequestPrimitiveList
                |> fun x -> acc.TryAdd(key, x) |> ignore

            | [IsIndexedListKey (index, key)] ->
                if acc.ContainsKey key then
                    match acc[key] with
                    | RList requestList ->
                        let lstAccLen = if index >= requestList.Length then index + 1 else requestList.Length
                        let lstAcc : RequestValue array = Array.zeroCreate (lstAccLen)
                        for i = 0 to lstAccLen - 1 do
                            let lstRequestValue =
                                if i <> index then
                                    match List.tryItem i requestList with
                                    | Some x -> x
                                    | None -> RNull
                                else
                                    parseRequestPrimitiveSingle values

                            lstAcc[i] <- lstRequestValue

                        acc[key] <- RList (List.ofArray lstAcc)
                    | _ -> ()
                elif index = 0 then
                    acc.TryAdd(key, RList [ parseRequestPrimitiveSingle values ]) |> ignore
                else
                    let lstAcc : RequestValue array = Array.zeroCreate (index + 1)
                    for i = 0 to index do
                        lstAcc[i] <- if i <> index then RNull else parseRequestPrimitiveSingle values
                    acc.TryAdd(key, RList (List.ofArray lstAcc)) |> ignore

            | [key] ->
                // primitive
                values
                |> parseRequestPrimitiveSingle
                |> fun x -> acc.TryAdd(key, x) |> ignore

            | IsListKey key :: remainingKeys ->
                // list of complex types
                if acc.ContainsKey key then
                    match acc[key] with
                    | RList requestList ->
                        requestList
                        |> Seq.collect (fun requestData ->
                            match requestData with
                            | RObject requestObject ->
                                let requestObjectAcc = requestDatasToAcc requestObject
                                parseNested requestObjectAcc remainingKeys values
                                Seq.singleton (requestObjectAcc |> requestAccToValues)
                            | _ -> Seq.empty)
                        |> List.ofSeq
                        |> RList
                        |> fun x -> acc[key] <- x
                    | _ -> ()
                else
                    values
                    |> Seq.map (fun value ->
                        let listValueAcc = newRequestAcc()
                        parseNested listValueAcc remainingKeys (seq { value })
                        listValueAcc
                        |> requestAccToValues)
                    |> List.ofSeq
                    |> RList
                    |> fun x -> acc.TryAdd(key, x) |> ignore

            | key :: remainingKeys ->
                // complex type
                if acc.ContainsKey key then
                    match acc[key] with
                    | RObject requestObject ->
                        let requestObjectAcc = requestDatasToAcc requestObject
                        parseNested requestObjectAcc remainingKeys values
                        acc[key] <- requestObjectAcc |> requestAccToValues
                    | _ -> ()
                else
                    let requestObjectAcc = newRequestAcc()
                    parseNested requestObjectAcc remainingKeys values
                    acc.TryAdd(key, requestObjectAcc |> requestAccToValues) |> ignore

        let requestAcc = newRequestAcc()

        for kvp in requestData do
            let keys =
                kvp.Key
                |> WebUtility.UrlDecode
                |> fun key -> key.Split('.', StringSplitOptions.RemoveEmptyEntries)
                |> List.ofArray
                |> function
                | [IsFlatKey key] when Seq.length kvp.Value > 1 ->[$"{key}[]"]
                | x -> x

            parseNested requestAcc keys kvp.Value

        requestAcc
        |> requestAccToValues

    let parseString (keyValueString : string) : RequestValue =
        let decoded = WebUtility.UrlDecode keyValueString
        let keyValues = decoded.Split('&')
        let requestDataPairs = Dictionary<string, IList<string>>()
        let addOrSet (acc : Dictionary<string, IList<string>>) key value =
            if acc.ContainsKey key then
                acc[key].Add(value)
            else
                acc.Add(key, List<string>(Seq.singleton value))
            ()

        for (kv : string) in keyValues do
            match List.ofArray (kv.Split('=')) with // preserve empty entries
            | [] -> ()
            | [key] -> addOrSet requestDataPairs key String.Empty
            | [key; value] -> addOrSet requestDataPairs key value
            | key :: values when values.Length = 0 -> addOrSet requestDataPairs key String.Empty
            | key :: value :: _ -> addOrSet requestDataPairs key value

        requestDataPairs
        |> Seq.map (fun kvp -> kvp.Key, kvp.Value :> IEnumerable<string>)
        |> dict
        |> parse

    let parseCookies (cookies : IRequestCookieCollection) : RequestValue =
        cookies
        |> Seq.map (fun kvp -> kvp.Key, seq { kvp.Value })
        |> dict
        |> parse

    let parseHeaders (headers : IHeaderDictionary) : RequestValue =
        headers
        |> Seq.map (fun kvp -> kvp.Key, kvp.Value :> string seq)
        |> dict
        |> parse

    let private routeKeyValues (route : RouteValueDictionary) =
        route
        |> Seq.map (fun kvp ->
            kvp.Key, seq { Convert.ToString(kvp.Value, Globalization.CultureInfo.InvariantCulture) |> WebUtility.UrlDecode })

    let private queryKeyValues (query : IQueryCollection) =
        query
        |> Seq.map (fun kvp -> kvp.Key, kvp.Value :> string seq)

    let parseRoute (route : RouteValueDictionary, query : IQueryCollection) : RequestValue =
        Seq.concat [
            route
            |> routeKeyValues

            query
            |> queryKeyValues ]
        |> dict
        |> parse

    let parseQuery (query : IQueryCollection) : RequestValue =
        query
        |> queryKeyValues
        |> dict
        |> parse

    let parseForm (form : IFormCollection, route : RouteValueDictionary option) : RequestValue =
        let routeKeyValues = route |> Option.map routeKeyValues |> Option.defaultValue Seq.empty

        let formKeyValues =
            form
            |> Seq.map (fun kvp -> kvp.Key, kvp.Value :> string seq)

        Seq.concat [ routeKeyValues; formKeyValues ]
        |> dict
        |> parse
