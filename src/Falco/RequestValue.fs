namespace Falco

open System
open System.Collections.Generic
open System.Net
open System.Text.RegularExpressions
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

module internal RequestValueParser =
    let private (|IsFlatKey|_|) (x : string) =
        if not(x.EndsWith "[]") && not(x.Contains(".")) then Some x
        else None

    let private (|IsListKey|_|) (x : string) =
        if x.EndsWith "[]" then Some (x.Substring(0, x.Length - 2))
        else None

    let private indexedListKeyRegex =
        Regex(@".\[(\d+)\]$", Text.RegularExpressions.RegexOptions.Compiled)

    let private (|IsIndexedListKey|_|) (x : string) =

        if x.EndsWith "]" then
            match indexedListKeyRegex.Match x with
            | m when m.Groups.Count = 2 ->
                let capture = m.Groups[1].Value
                Some (int capture, x.Substring(0, x.Length - capture.Length - 2))
            | _ -> None
        else None

    let private extractRequestDataKeys (key : string, isSingle : bool) =
        key
        |> WebUtility.UrlDecode
        |> fun key -> key.Split('.', StringSplitOptions.RemoveEmptyEntries)
        |> List.ofArray
        |> function
        | [IsFlatKey key] when not isSingle ->[$"{key}[]"]
        | x -> x

    let private newRequestAcc () =
        Dictionary<string, RequestValue>()

    let private requestAccToValues (x : Dictionary<string, RequestValue>) =
        x |> Seq.map (fun (kvp) -> kvp.Key, kvp.Value) |> List.ofSeq |> RObject

    let private requestDatasToAcc (x : (string * RequestValue) list) =
        let acc = newRequestAcc()
        for key, value in x do
            acc.TryAdd(key, value) |> ignore
        acc

    let private parseRequestPrimitive (x : string) =
        let decoded = WebUtility.UrlDecode x
        match decoded with
        | IsNullOrWhiteSpace _ -> RNull
        | IsTrue x
        | IsFalse x -> RBool x
        // Don't parse integers with leading zeros (except "0" itself) as floats
        | _ when x.Length > 1 && x.StartsWith '0' && not(x.Contains '.') && not(x.Contains ',') -> RString x
        // Don't parse large numerics as floats
        | _ when x.Length > 15 -> RString x
        | IsFloat x -> RNumber x
        | x -> RString x

    let private parseRequestPrimitiveList values =
        values
        |> Seq.map parseRequestPrimitive
        |> List.ofSeq
        |> RList

    let private parseRequestPrimitiveSingle values =
        values
        |> Seq.tryHead
        |> Option.map parseRequestPrimitive
        |> Option.defaultValue RNull

    let private parseExistingRequestIndexedList index values (requestList : RequestValue list) =
        let requestArray = List.toArray requestList
        let lstAccLen = if index >= requestList.Length then index + 1 else requestList.Length
        let lstAcc : RequestValue array = Array.zeroCreate (lstAccLen)
        for i = 0 to lstAccLen - 1 do
            let lstRequestValue =
                if i <> index then
                    match Array.tryItem i requestArray with
                    | Some x -> x
                    | None -> RNull
                else
                    parseRequestPrimitiveSingle values

            lstAcc[i] <- lstRequestValue

        RList (List.ofArray lstAcc)

    let private parseRequestIndexedList index values =
        let lstAcc : RequestValue array = Array.zeroCreate (index + 1)
        for i = 0 to index do
            lstAcc[i] <- if i <> index then RNull else parseRequestPrimitiveSingle values
        RList (List.ofArray lstAcc)

    let parse (requestData : IDictionary<string, string seq>) : RequestValue =
        let rec parseNested (acc : Dictionary<string, RequestValue>) (keys : string list) (values : string seq) =
            match keys with
            | [] -> ()
            | [IsListKey key] ->
                // list of primitives
                values
                |> parseRequestPrimitiveList
                |> fun x -> acc.TryAdd(key, x) |> ignore

            | [IsIndexedListKey (index, key)] ->
                // indexed list of primitives
                match acc.TryGetValue key with
                | true, RList requestList ->
                    parseExistingRequestIndexedList index values requestList
                    |> fun x -> acc[key] <- x

                | _ when index = 0 ->
                    // first item in indexed list, initialize
                    RList [ parseRequestPrimitiveSingle values ]
                    |> fun x -> acc.TryAdd(key, x) |> ignore

                | _ ->
                    parseRequestIndexedList index values
                    |> fun x -> acc.TryAdd(key, x) |> ignore

            | [key] ->
                // primitive
                values
                |> parseRequestPrimitiveSingle
                |> fun x -> acc.TryAdd(key, x) |> ignore

            | IsListKey key :: remainingKeys ->
                // list of complex types
                match acc.TryGetValue key with
                | true, RList requestList ->
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

                | _ ->
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
                match acc.TryGetValue key with
                | true, RObject requestObject ->
                    let requestObjectAcc = requestDatasToAcc requestObject
                    parseNested requestObjectAcc remainingKeys values
                    acc[key] <- requestObjectAcc |> requestAccToValues

                | _ ->
                    let requestObjectAcc = newRequestAcc()
                    parseNested requestObjectAcc remainingKeys values
                    acc.TryAdd(key, requestObjectAcc |> requestAccToValues) |> ignore

        let requestAcc = newRequestAcc()

        for kvp in requestData do
            let keys = extractRequestDataKeys (kvp.Key, Seq.tryItem 1 kvp.Value = None)
            parseNested requestAcc keys kvp.Value

        requestAcc
        |> requestAccToValues

module RequestValue =
    /// Parses the provided request data into a `RequestValue` structure.
    ///
    /// - `requestData`: A dictionary where the key is a string representing the request data key (e.g., form field name, query parameter name) and the value is a sequence of strings representing the values associated with that key. This allows for handling multiple values for the same key, such as in the case of checkboxes or multi-select form fields.
    let parse (requestData : IDictionary<string, string seq>) : RequestValue =
        RequestValueParser.parse requestData

    /// Parses a URL-encoded key-value string (e.g., "key1=value1&key2=value2") into a `RequestValue` structure.
    ///
    /// - `keyValueString`: A URL-encoded string containing key-value pairs, where keys and values are separated by '=' and pairs are separated by '&'. Keys without values (e.g., "key1&key2=value2") are treated as having an empty string value. The keys and values are URL-decoded before parsing.
    let parseString (keyValueString : string) : RequestValue =
        let requestDataPairs = Dictionary<string, IList<string>>()

        let addOrSet (acc : Dictionary<string, IList<string>>) key value =
            if acc.ContainsKey key then
                acc[key].Add value
            else
                acc.Add(key, List<string>(Seq.singleton value))
            ()

        for kv in keyValueString.Split '&' do
            // Handle keys without values (e.g. "key1&key2=value2") by treating them as having an empty string value
            match kv.IndexOf '=' with
            | -1 ->
                addOrSet requestDataPairs kv String.Empty
            | idx ->
                let key = kv.Substring(0, idx)
                let value = if idx + 1 < kv.Length then kv.Substring(idx + 1) else String.Empty
                addOrSet requestDataPairs key value

        requestDataPairs
        |> Seq.map (fun kvp -> kvp.Key, kvp.Value :> IEnumerable<string>)
        |> dict
        |> parse

    /// Parses the cookies from the request into a `RequestValue` structure.
    ///
    /// - `cookies`: An `IRequestCookieCollection` containing the cookies from the HTTP request. Each cookie's key and value are URL-decoded before parsing. Multiple cookies with the same key are not expected, as cookie keys are unique within a request, but if they do occur, they will be treated as multiple values for the same key.
    let parseCookies (cookies : IRequestCookieCollection) : RequestValue =
        cookies
        |> Seq.map (fun kvp -> kvp.Key, seq { kvp.Value })
        |> dict
        |> parse

    /// Parses the headers from the request into a `RequestValue` structure.
    ///
    /// - `headers`: An `IHeaderDictionary` containing the headers from the HTTP request. Each header's key and value(s) are URL-decoded before parsing. Headers can have multiple values for the same key, and all values will be included in the resulting `RequestValue`.
    let parseHeaders (headers : IHeaderDictionary) : RequestValue =
        headers
        |> Seq.map (fun kvp -> kvp.Key, kvp.Value :> string seq)
        |> dict
        |> parse

    let private routeKeyValues (route : RouteValueDictionary) =
        route
        |> Seq.map (fun kvp ->
            kvp.Key, seq { Convert.ToString(kvp.Value, Globalization.CultureInfo.InvariantCulture) })

    let private queryKeyValues (query : IQueryCollection) =
        query
        |> Seq.map (fun kvp -> kvp.Key, kvp.Value :> string seq)

    /// Parses the route values and query string from the request into a `RequestValue` structure.
    ///
    /// - `route`: A `RouteValueDictionary` containing the route values from the HTTP request. Each route value's key and value are URL-decoded before parsing. Route values are typically defined in the route template and can be used to capture dynamic segments of the URL.
    /// - `query`: An `IQueryCollection` containing the query string parameters from the HTTP request. Each query parameter's key and value(s) are URL-decoded before parsing. Query parameters can have multiple values for the same key, and all values will be included in the resulting `RequestValue`.
    let parseRoute (route : RouteValueDictionary, query : IQueryCollection) : RequestValue =
        Seq.concat [
            route
            |> routeKeyValues

            query
            |> queryKeyValues ]
        |> dict
        |> parse

    /// Parses the query string from the request into a `RequestValue` structure.
    ///
    /// - `query`: An `IQueryCollection` containing the query string parameters from the HTTP request. Each query parameter's key and value(s) are URL-decoded before parsing. Query parameters can have multiple values for the same key, and all values will be included in the resulting `RequestValue`.
    let parseQuery (query : IQueryCollection) : RequestValue =
        query
        |> queryKeyValues
        |> dict
        |> parse

    /// Parses the form data from the request into a `RequestValue` structure.
    ///
    /// - `form`: An `IFormCollection` containing the form data from the HTTP request. Each form field's key and value(s) are URL-decoded before parsing. Form fields can have multiple values for the same key (e.g., in the case of checkboxes or multi-select fields), and all values will be included in the resulting `RequestValue`. The `route` parameter is optional and can be used to include route values in the parsing process, which is useful when you want to combine route values with form data.
    /// - `route`: An optional `RouteValueDictionary` containing the route values from the HTTP request. Each route value's key and value are URL-decoded before parsing. Route values are typically defined in the route template and can be used to capture dynamic segments of the URL. If provided, the route values will be combined with the form data during parsing, allowing you to access both sets of values in the resulting `RequestValue`.
    let parseForm (form : IFormCollection, route : RouteValueDictionary option) : RequestValue =
        let routeKeyValues = route |> Option.map routeKeyValues |> Option.defaultValue Seq.empty

        let formKeyValues =
            form
            |> Seq.map (fun kvp -> kvp.Key, kvp.Value :> string seq)

        Seq.concat [ routeKeyValues; formKeyValues ]
        |> dict
        |> parse
