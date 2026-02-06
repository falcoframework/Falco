namespace Falco

open System
open System.Collections.Generic
open Microsoft.AspNetCore.Http
open Microsoft.FSharp.Core.Operators
open Falco.StringPatterns

[<AutoOpen>]
module RequestDataExtensions =
    module internal RequestValue =
        let private epoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)

        let inline private floatInRange min max (f : float) =
            let _min = float min
            let _max = float max
            f >= _min && f <= _max

        let asObject requestValue =
            match requestValue with
            | RObject properties -> Some properties
            | _ -> None

        let asList requestValue =
            match requestValue with
            | RList a -> Some a
            | _ -> None

        let private asRequestPrimitive requestValue =
            let rec asPrimitive requestValue =
                match requestValue with
                | RNull
                | RBool _
                | RNumber _
                | RString _ -> Some requestValue
                | RList lst -> List.tryHead lst |> Option.bind asPrimitive
                | _ -> None

            asPrimitive requestValue

        let asString (requestValue : RequestValue) =
            match asRequestPrimitive requestValue with
            | Some (RNull) -> Some ""
            | Some (RBool b) -> Some (if b then "true" else "false")
            | Some (RNumber n) -> Some (string n)
            | Some (RString s) -> Some s
            | _ -> None

        let asStringNonEmpty (requestValue : RequestValue) =
            match asRequestPrimitive requestValue with
            | Some (RBool b) -> Some (if b then "true" else "false")
            | Some (RNumber n) -> Some (string n)
            | Some (RString s) -> Some s
            | _ -> None

        let asInt16 (requestValue : RequestValue) =
            match asRequestPrimitive requestValue with
            | Some (RNumber x) when floatInRange Int16.MinValue Int16.MaxValue x -> Some (Convert.ToInt16 x)
            | Some (RString x) -> StringParser.parseInt16 x
            | _ -> None

        let asInt32 (requestValue : RequestValue) =
            match asRequestPrimitive requestValue with
            | Some (RNumber x) when floatInRange Int32.MinValue Int32.MaxValue x -> Some (Convert.ToInt32 x)
            | Some (RString x) -> StringParser.parseInt32 x
            | _ -> None

        let asInt f = asInt32 f

        let asInt64 (requestValue : RequestValue) =
            match asRequestPrimitive requestValue with
            | Some (RNumber x) when floatInRange Int64.MinValue Int64.MaxValue x -> Some (Convert.ToInt64 x)
            | Some (RString x) -> StringParser.parseInt64 x
            | _ -> None


        let private trueValues = HashSet<string>(seq { "true"; "on"; "yes" }, StringComparer.OrdinalIgnoreCase)
        let private falseValues = HashSet<string>(seq { "false"; "off"; "no" }, StringComparer.OrdinalIgnoreCase)

        let asBoolean (requestValue : RequestValue) =
            match asRequestPrimitive requestValue with
            | Some (RBool x) when x -> Some true
            | Some (RBool x) when not x -> Some false
            | Some (RNumber x) when x = 0. -> Some false
            | Some (RNumber x) when x = 1. -> Some true
            | Some (RString x) when trueValues.Contains x -> Some true
            | Some (RString x) when falseValues.Contains x -> Some false
            | _ -> None

        let asFloat (requestValue : RequestValue) =
            match asRequestPrimitive requestValue with
            | Some (RNumber x) -> Some x
            | Some (RString x) -> StringParser.parseFloat x
            | _ -> None

        let asDecimal (requestValue : RequestValue) =
            match asRequestPrimitive requestValue with
            | Some (RNumber x) -> Some (decimal x)
            | Some (RString x) -> StringParser.parseDecimal x
            | _ -> None

        let asDateTime (requestValue : RequestValue) =
            match asRequestPrimitive requestValue with
            | Some (RNumber n) when floatInRange Int64.MinValue Int64.MaxValue n ->
                Some (epoch.AddMilliseconds(n))
            | Some (RString s) ->
                StringParser.parseDateTime s
            | _ -> None

        let asDateTimeOffset (requestValue : RequestValue) =
            match asRequestPrimitive requestValue with
            | Some (RNumber n) when floatInRange Int64.MinValue Int64.MaxValue n ->
                Some (DateTimeOffset.FromUnixTimeMilliseconds(Convert.ToInt64 n))
            | Some (RString s) ->
                StringParser.parseDateTimeOffset s
            | _ -> None

        let asTimeSpan (requestValue : RequestValue) =
            match asRequestPrimitive requestValue with
            | Some (RString s) -> StringParser.parseTimeSpan s
            | _ -> None

        let asGuid (requestValue : RequestValue) =
            match asRequestPrimitive requestValue with
            | Some (RString s) -> StringParser.parseGuid s
            | _ -> None

        let private bindList bind requestValue =
            // accumulate successful parses
            match requestValue with
            | RList slist ->
                slist
                |> List.fold (fun (acc : List<'U>) sv ->
                    match bind sv with
                    | Some b ->
                        acc.Add(b) |> ignore
                        acc
                    | None -> acc) (List<'U>())
                |> List.ofSeq
            | RNull
            | RObject _ -> []
            | v -> v |> bind |> Option.map List.singleton |> Option.defaultValue []

        let asStringList (requestValue : RequestValue) =
            bindList asString requestValue

        let asStringNonEmptyList (requestValue : RequestValue) =
            bindList asStringNonEmpty requestValue

        let asInt16List (requestValue : RequestValue) =
            bindList asInt16 requestValue

        let asInt32List (requestValue : RequestValue) =
            bindList asInt32 requestValue

        let asIntList f =
            asInt32List f

        let asInt64List (requestValue : RequestValue) =
            bindList asInt64 requestValue

        let asBooleanList (requestValue : RequestValue) =
            bindList asBoolean requestValue

        let asFloatList (requestValue : RequestValue) =
            bindList asFloat requestValue

        let asDecimalList (requestValue : RequestValue) =
            bindList asDecimal requestValue

        let asDateTimeList (requestValue : RequestValue) =
            bindList asDateTime requestValue

        let asDateTimeOffsetList (requestValue : RequestValue) =
            bindList asDateTimeOffset requestValue

        let asTimeSpanList (requestValue : RequestValue) =
            bindList asTimeSpan requestValue

        let asGuidList (requestValue : RequestValue) =
            bindList asGuid requestValue

    type RequestData(requestValue : RequestValue) =
        new(requestData : IDictionary<string, string seq>) = RequestData(RequestValue.parse requestData)
        new(keyValues : (string * string seq) seq) = RequestData(dict keyValues)


        static member Empty = RequestData RNull

        member _.AsKeyValues() = RequestValue.asObject requestValue |> Option.map (List.map (fun (k, v) -> k, RequestData v)) |> Option.defaultValue []
        member _.AsList() = RequestValue.asList requestValue |> Option.map (List.map RequestData) |> Option.defaultValue []
        member _.AsString ?defaultValue = RequestValue.asString requestValue |> Option.defaultWith (fun _ -> defaultArg defaultValue "")
        member _.AsStringNonEmpty ?defaultValue = RequestValue.asStringNonEmpty requestValue |> Option.defaultWith (fun _ -> defaultArg defaultValue "")
        member _.AsInt16 ?defaultValue = RequestValue.asInt16 requestValue |> Option.defaultWith (fun _ -> defaultArg defaultValue 0s)
        member _.AsInt32 ?defaultValue = RequestValue.asInt32 requestValue |> Option.defaultWith (fun _ -> defaultArg defaultValue 0)
        member x.AsInt ?defaultValue = x.AsInt32(?defaultValue = defaultValue)
        member _.AsInt64 ?defaultValue = RequestValue.asInt64 requestValue |> Option.defaultWith (fun _ -> defaultArg defaultValue 0L)
        member _.AsBoolean ?defaultValue = RequestValue.asBoolean requestValue |> Option.defaultWith (fun _ -> defaultArg defaultValue false)
        member _.AsFloat ?defaultValue = RequestValue.asFloat requestValue |> Option.defaultWith (fun _ -> defaultArg defaultValue 0.)
        member _.AsDecimal ?defaultValue = RequestValue.asDecimal requestValue |> Option.defaultWith (fun _ -> defaultArg defaultValue 0.M)
        member _.AsDateTime ?defaultValue = RequestValue.asDateTime requestValue |> Option.defaultWith (fun _ -> defaultArg defaultValue DateTime.MinValue)
        member _.AsDateTimeOffset ?defaultValue = RequestValue.asDateTimeOffset requestValue |> Option.defaultWith (fun _ -> defaultArg defaultValue DateTimeOffset.MinValue)
        member _.AsTimeSpan ?defaultValue = RequestValue.asTimeSpan requestValue |> Option.defaultWith (fun _ -> defaultArg defaultValue TimeSpan.MinValue)
        member _.AsGuid ?defaultValue = RequestValue.asGuid requestValue |> Option.defaultWith (fun _ -> defaultArg defaultValue Guid.Empty)

        member _.AsStringOption() = RequestValue.asString requestValue
        member _.AsStringNonEmptyOption() = RequestValue.asStringNonEmpty requestValue
        member _.AsInt16Option() = RequestValue.asInt16 requestValue
        member _.AsInt32Option() = RequestValue.asInt32 requestValue
        member x.AsIntOption() = x.AsInt32Option()
        member _.AsInt64Option() = RequestValue.asInt64 requestValue
        member _.AsBooleanOption() = RequestValue.asBoolean requestValue
        member _.AsFloatOption() = RequestValue.asFloat requestValue
        member _.AsDecimalOption() = RequestValue.asDecimal requestValue
        member _.AsDateTimeOption() = RequestValue.asDateTime requestValue
        member _.AsDateTimeOffsetOption() = RequestValue.asDateTimeOffset requestValue
        member _.AsTimeSpanOption() = RequestValue.asTimeSpan requestValue
        member _.AsGuidOption() = RequestValue.asGuid requestValue

        member _.AsStringList() = RequestValue.asStringList requestValue
        member _.AsStringNonEmptyList() = RequestValue.asStringNonEmptyList requestValue
        member _.AsInt16List() = RequestValue.asInt16List requestValue
        member _.AsInt32List() = RequestValue.asInt32List requestValue
        member _.AsIntList() = RequestValue.asIntList requestValue
        member _.AsInt64List() = RequestValue.asInt64List requestValue
        member _.AsBooleanList() = RequestValue.asBooleanList requestValue
        member _.AsFloatList() = RequestValue.asFloatList requestValue
        member _.AsDecimalList() = RequestValue.asDecimalList requestValue
        member _.AsDateTimeList() = RequestValue.asDateTimeList requestValue
        member _.AsDateTimeOffsetList() = RequestValue.asDateTimeOffsetList requestValue
        member _.AsGuidList() = RequestValue.asGuidList requestValue
        member _.AsTimeSpanList() = RequestValue.asTimeSpanList requestValue

        member _.TryGet(name : string) : RequestData option =
            match requestValue with
            | RObject props ->
                props
                |> List.tryFind (fun (k, _) -> String.Equals(k, name, StringComparison.OrdinalIgnoreCase))
                |> Option.map (fun (_, v) -> RequestData v)
            | _ -> None

        member x.Get(name : string) : RequestData =
            match x.TryGet name with
            | Some v -> v
            | None -> RequestData.Empty

        member x.TryGetString (name : string) = x.TryGet name |> Option.bind _.AsStringOption()
        member x.TryGetStringNonEmpty (name : string) = x.TryGet name |> Option.bind _.AsStringNonEmptyOption()
        member x.TryGetInt16 (name : string) = x.TryGet name |> Option.bind _.AsInt16Option()
        member x.TryGetInt32 (name : string) = x.TryGet name |> Option.bind _.AsInt32Option()
        member x.TryGetInt (name : string) = x.TryGet name |> Option.bind _.AsIntOption()
        member x.TryGetInt64 (name : string) = x.TryGet name |> Option.bind _.AsInt64Option()
        member x.TryGetBoolean (name : string) = x.TryGet name |> Option.bind _.AsBooleanOption()
        member x.TryGetFloat (name : string) = x.TryGet name |> Option.bind _.AsFloatOption()
        member x.TryGetDecimal (name : string) = x.TryGet name |> Option.bind _.AsDecimalOption()
        member x.TryGetDateTime (name : string) = x.TryGet name |> Option.bind _.AsDateTimeOption()
        member x.TryGetDateTimeOffset (name : string) = x.TryGet name |> Option.bind _.AsDateTimeOffsetOption()
        member x.TryGetGuid (name : string) = x.TryGet name |> Option.bind _.AsGuidOption()
        member x.TryGetTimeSpan (name : string) = x.TryGet name |> Option.bind _.AsTimeSpanOption()

        member x.GetString (name : string, ?defaultValue : String) = x.TryGetString name |> Option.defaultWith (fun () -> defaultArg defaultValue "")
        member x.GetStringNonEmpty (name : string, ?defaultValue : String) = x.TryGetStringNonEmpty name |> Option.defaultWith (fun () -> defaultArg defaultValue "")
        member x.GetInt16 (name : string, ?defaultValue : Int16) = x.TryGetInt16 name |> Option.defaultWith (fun () -> defaultArg defaultValue 0s)
        member x.GetInt32 (name : string, ?defaultValue : Int32) = x.TryGetInt32 name |> Option.defaultWith (fun () -> defaultArg defaultValue 0)
        member x.GetInt (name : string, ?defaultValue : Int32) = x.TryGetInt name |> Option.defaultWith (fun () -> defaultArg defaultValue 0)
        member x.GetInt64 (name : string, ?defaultValue : Int64) = x.TryGetInt64 name |> Option.defaultWith (fun () -> defaultArg defaultValue 0L)
        member x.GetBoolean (name : string, ?defaultValue : Boolean) = x.TryGetBoolean name |> Option.defaultWith (fun () -> defaultArg defaultValue false)
        member x.GetFloat (name : string, ?defaultValue : float) = x.TryGetFloat name |> Option.defaultWith (fun () -> defaultArg defaultValue 0)
        member x.GetDecimal (name : string, ?defaultValue : Decimal) = x.TryGetDecimal name |> Option.defaultWith (fun () -> defaultArg defaultValue 0M)
        member x.GetDateTime (name : string, ?defaultValue : DateTime) = x.TryGetDateTime name |> Option.defaultWith (fun () -> defaultArg defaultValue DateTime.MinValue)
        member x.GetDateTimeOffset (name : string, ?defaultValue : DateTimeOffset) = x.TryGetDateTimeOffset name |> Option.defaultWith (fun () -> defaultArg defaultValue DateTimeOffset.MinValue)
        member x.GetGuid (name : string, ?defaultValue : Guid) = x.TryGetGuid name |> Option.defaultWith (fun () -> defaultArg defaultValue Guid.Empty)
        member x.GetTimeSpan (name : string, ?defaultValue : TimeSpan) = x.TryGetTimeSpan name |> Option.defaultWith (fun () -> defaultArg defaultValue TimeSpan.MinValue)

        member x.GetStringList (name : string) = x.TryGet name |> Option.map _.AsStringList() |> Option.defaultValue []
        member x.GetStringNonEmptyList (name : string) = x.TryGet name |> Option.map _.AsStringNonEmptyList() |> Option.defaultValue []
        member x.GetInt16List (name : string) = x.TryGet name |> Option.map _.AsInt16List() |> Option.defaultValue []
        member x.GetInt32List (name : string) = x.TryGet name |> Option.map _.AsInt32List() |> Option.defaultValue []
        member x.GetIntList (name : string) = x.TryGet name |> Option.map _.AsIntList() |> Option.defaultValue []
        member x.GetInt64List (name : string) = x.TryGet name |> Option.map _.AsInt64List() |> Option.defaultValue []
        member x.GetBooleanList (name : string) = x.TryGet name |> Option.map _.AsBooleanList() |> Option.defaultValue []
        member x.GetFloatList (name : string) = x.TryGet name |> Option.map _.AsFloatList() |> Option.defaultValue []
        member x.GetDecimalList (name : string) = x.TryGet name |> Option.map _.AsDecimalList() |> Option.defaultValue []
        member x.GetDateTimeList (name : string) = x.TryGet name |> Option.map _.AsDateTimeList() |> Option.defaultValue []
        member x.GetDateTimeOffsetList (name : string) = x.TryGet name |> Option.map _.AsDateTimeOffsetList() |> Option.defaultValue []
        member x.GetGuidList (name : string) = x.TryGet name |> Option.map _.AsGuidList() |> Option.defaultValue []
        member x.GetTimeSpanList (name : string) = x.TryGet name |> Option.map _.AsTimeSpanList() |> Option.defaultValue []

    let inline (?) (requestData : RequestData) (name : string) =
        requestData.Get name

[<Sealed>]
type FormData(requestValue : RequestValue, files : IFormFileCollection option) =
    inherit RequestData(requestValue)

    member _.Files = files

    member _.TryGetFile(name : string) =
        match files, name with
        | _, IsNullOrWhiteSpace _
        | None, _ -> None
        | Some files, name ->
            match files.GetFile name with
            | f when isNull f -> None
            | f -> Some f
