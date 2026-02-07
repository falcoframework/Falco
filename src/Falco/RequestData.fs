namespace Falco

open System
open System.Collections.Generic
open Microsoft.AspNetCore.Http
open Microsoft.FSharp.Core.Operators
open Falco.StringPatterns

module private RequestData =
    let epoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)
    let trueValues = HashSet<string>([| "true"; "on"; "yes" |], StringComparer.OrdinalIgnoreCase)
    let falseValues = HashSet<string>([| "false"; "off"; "no" |], StringComparer.OrdinalIgnoreCase)

[<AutoOpen>]
module private RequestValueExtensions =
    let tryGet name fn requestValue =
        match requestValue with
        | RObject props ->
            props
            |> List.tryFind (fun (k, _) -> String.Equals(k, name, StringComparison.OrdinalIgnoreCase))
            |> Option.bind (fun (_, v) -> fn v)
        | _ -> None

    let orDefault maybe defaultValue opt =
        match opt, maybe with
        | Some x, _ -> x
        | None, Some v -> v
        | _ -> defaultValue

    let asOr asFn defaultValue requestValue =
        asFn requestValue |> Option.defaultWith (fun _ -> defaultValue)

    let bindList bind requestValue =
        match requestValue with
        | RList slist -> List.choose bind slist
        | RNull | RObject _ -> []
        | v -> bind v |> Option.toList

    let asObject requestValue =
        match requestValue with
        | RObject properties -> Some properties
        | _ -> None

    let asList requestValue =
        match requestValue with
        | RList a -> Some a
        | _ -> None

    let asRequestPrimitive requestValue =
        let rec asPrimitive requestValue =
            match requestValue with
            | RNull
            | RBool _
            | RNumber _
            | RString _ -> Some requestValue
            | RList lst -> List.tryHead lst |> Option.bind asPrimitive
            | _ -> None

        asPrimitive requestValue

    let asString requestValue =
        match asRequestPrimitive requestValue with
        | Some (RNull) -> Some ""
        | Some (RBool b) -> Some (if b then "true" else "false")
        | Some (RNumber n) -> Some (string n)
        | Some (RString s) -> Some s
        | _ -> None

    let asStringNonEmpty requestValue =
        match asRequestPrimitive requestValue with
        | Some (RBool b) -> Some (if b then "true" else "false")
        | Some (RNumber n) -> Some (string n)
        | Some (RString s) -> Some s
        | _ -> None

    let asInt16 requestValue =
        match asRequestPrimitive requestValue with
        | Some (RNumber x) when x >= float Int16.MinValue && x <= float Int16.MaxValue -> Some (Convert.ToInt16 x)
        | Some (RString x) -> StringParser.parseInt16 x
        | _ -> None

    let asInt32 requestValue =
        match asRequestPrimitive requestValue with
        | Some (RNumber x) when x >= float Int32.MinValue && x <= float Int32.MaxValue -> Some (Convert.ToInt32 x)
        | Some (RString x) -> StringParser.parseInt32 x
        | _ -> None

    let asInt64 requestValue =
        match asRequestPrimitive requestValue with
        | Some (RNumber x) when x >= float Int64.MinValue && x <= float Int64.MaxValue -> Some (Convert.ToInt64 x)
        | Some (RString x) -> StringParser.parseInt64 x
        | _ -> None

    let asBoolean requestValue =
        match asRequestPrimitive requestValue with
        | Some (RBool x) when x -> Some true
        | Some (RBool x) when not x -> Some false
        | Some (RNumber x) when x = 0. -> Some false
        | Some (RNumber x) when x = 1. -> Some true
        | Some (RString x) when RequestData.trueValues.Contains x -> Some true
        | Some (RString x) when RequestData.falseValues.Contains x -> Some false
        | _ -> None

    let asFloat requestValue =
        match asRequestPrimitive requestValue with
        | Some (RNumber x) -> Some x
        | Some (RString x) -> StringParser.parseFloat x
        | _ -> None

    let asDecimal requestValue =
        match asRequestPrimitive requestValue with
        | Some (RNumber x) -> Some (decimal x)
        | Some (RString x) -> StringParser.parseDecimal x
        | _ -> None

    let asDateTime requestValue =
        match asRequestPrimitive requestValue with
        | Some (RNumber n) when n >= float Int64.MinValue && n <= float Int64.MaxValue ->
            Some (RequestData.epoch.AddMilliseconds(n))
        | Some (RString s) ->
            StringParser.parseDateTime s
        | _ -> None

    let asDateTimeOffset requestValue =
        match asRequestPrimitive requestValue with
        | Some (RNumber n) when n >= float Int64.MinValue && n <= float Int64.MaxValue ->
            Some (DateTimeOffset.FromUnixTimeMilliseconds(Convert.ToInt64 n))
        | Some (RString s) ->
            StringParser.parseDateTimeOffset s
        | _ -> None

    let asTimeSpan requestValue =
        match asRequestPrimitive requestValue with
        | Some (RString s) -> StringParser.parseTimeSpan s
        | _ -> None

    let asGuid requestValue =
        match asRequestPrimitive requestValue with
        | Some (RString s) -> StringParser.parseGuid s
        | _ -> None

    let asStringList requestValue =
        bindList asString requestValue

    let asStringNonEmptyList requestValue =
        bindList asStringNonEmpty requestValue

    let asInt16List requestValue =
        bindList asInt16 requestValue

    let asInt32List requestValue =
        bindList asInt32 requestValue

    let asInt64List requestValue =
        bindList asInt64 requestValue

    let asBooleanList requestValue =
        bindList asBoolean requestValue

    let asFloatList requestValue =
        bindList asFloat requestValue

    let asDecimalList requestValue =
        bindList asDecimal requestValue

    let asDateTimeList requestValue =
        bindList asDateTime requestValue

    let asDateTimeOffsetList requestValue =
        bindList asDateTimeOffset requestValue

    let asTimeSpanList requestValue =
        bindList asTimeSpan requestValue

    let asGuidList requestValue =
        bindList asGuid requestValue

type RequestData(requestValue : RequestValue) =
    new(requestData : IDictionary<string, string seq>) = RequestData(RequestValue.parse requestData)
    new(keyValues : (string * string seq) seq) = RequestData(dict keyValues)

    static member Empty = RequestData RNull

    member _.TryGet(name : string) : RequestData option =
        tryGet name (RequestData >> Some) requestValue

    member _.Get(name : string) : RequestData =
        tryGet name (RequestData >> Some) requestValue
        |> Option.defaultValue RequestData.Empty

    member _.AsKeyValues() =
        asOr (asObject >> Option.map (List.map (fun (k, v) -> k, RequestData v))) [] requestValue

    member _.AsList() =
        asOr (asList >> Option.map (List.map RequestData)) [] requestValue

    member _.AsString ?defaultValue = requestValue |> asString |> orDefault defaultValue ""
    member _.AsStringNonEmpty ?defaultValue = requestValue |> asStringNonEmpty |> orDefault defaultValue ""
    member _.AsInt16 ?defaultValue = requestValue |> asInt16 |> orDefault defaultValue 0s
    member _.AsInt32 ?defaultValue = requestValue |> asInt32 |> orDefault defaultValue 0
    member x.AsInt ?defaultValue = x.AsInt32(?defaultValue = defaultValue)
    member _.AsInt64 ?defaultValue = requestValue |> asInt64 |> orDefault defaultValue 0L
    member _.AsBoolean ?defaultValue = requestValue |> asBoolean |> orDefault defaultValue false
    member _.AsFloat ?defaultValue = requestValue |> asFloat |> orDefault defaultValue 0.
    member _.AsDecimal ?defaultValue = requestValue |> asDecimal |> orDefault defaultValue 0.M
    member _.AsDateTime ?defaultValue = requestValue |> asDateTime |> orDefault defaultValue DateTime.MinValue
    member _.AsDateTimeOffset ?defaultValue = requestValue |> asDateTimeOffset |> orDefault defaultValue DateTimeOffset.MinValue
    member _.AsTimeSpan ?defaultValue = requestValue |> asTimeSpan |> orDefault defaultValue TimeSpan.MinValue
    member _.AsGuid ?defaultValue = requestValue |> asGuid |> orDefault defaultValue Guid.Empty

    member _.AsStringOption() = asString requestValue
    member _.AsStringNonEmptyOption() = asStringNonEmpty requestValue
    member _.AsInt16Option() = asInt16 requestValue
    member _.AsInt32Option() = asInt32 requestValue
    member x.AsIntOption() = x.AsInt32Option()
    member _.AsInt64Option() = asInt64 requestValue
    member _.AsBooleanOption() = asBoolean requestValue
    member _.AsFloatOption() = asFloat requestValue
    member _.AsDecimalOption() = asDecimal requestValue
    member _.AsDateTimeOption() = asDateTime requestValue
    member _.AsDateTimeOffsetOption() = asDateTimeOffset requestValue
    member _.AsTimeSpanOption() = asTimeSpan requestValue
    member _.AsGuidOption() = asGuid requestValue

    member _.AsStringList() = asStringList requestValue
    member _.AsStringNonEmptyList() = asStringNonEmptyList requestValue
    member _.AsInt16List() = asInt16List requestValue
    member _.AsInt32List() = asInt32List requestValue
    member _.AsIntList() = asInt32List requestValue
    member _.AsInt64List() = asInt64List requestValue
    member _.AsBooleanList() = asBooleanList requestValue
    member _.AsFloatList() = asFloatList requestValue
    member _.AsDecimalList() = asDecimalList requestValue
    member _.AsDateTimeList() = asDateTimeList requestValue
    member _.AsDateTimeOffsetList() = asDateTimeOffsetList requestValue
    member _.AsGuidList() = asGuidList requestValue
    member _.AsTimeSpanList() = asTimeSpanList requestValue

    member x.TryGetString (name : string) = tryGet name asString requestValue
    member x.TryGetStringNonEmpty (name : string) = tryGet name asStringNonEmpty requestValue
    member x.TryGetInt16 (name : string) = tryGet name asInt16 requestValue
    member x.TryGetInt32 (name : string) = tryGet name asInt32 requestValue
    member x.TryGetInt (name : string) = tryGet name asInt32 requestValue
    member x.TryGetInt64 (name : string) = tryGet name asInt64 requestValue
    member x.TryGetBoolean (name : string) = tryGet name asBoolean requestValue
    member x.TryGetFloat (name : string) = tryGet name asFloat requestValue
    member x.TryGetDecimal (name : string) = tryGet name asDecimal requestValue
    member x.TryGetDateTime (name : string) = tryGet name asDateTime requestValue
    member x.TryGetDateTimeOffset (name : string) = tryGet name asDateTimeOffset requestValue
    member x.TryGetGuid (name : string) = tryGet name asGuid requestValue
    member x.TryGetTimeSpan (name : string) = tryGet name asTimeSpan requestValue

    member x.GetString (name : string, ?defaultValue : String) = requestValue |> tryGet name asString |> orDefault defaultValue ""
    member x.GetStringNonEmpty (name : string, ?defaultValue : String) = requestValue |> tryGet name asStringNonEmpty |> orDefault defaultValue ""
    member x.GetInt16 (name : string, ?defaultValue : Int16) = requestValue |> tryGet name asInt16 |> orDefault defaultValue 0s
    member x.GetInt32 (name : string, ?defaultValue : Int32) = requestValue |> tryGet name asInt32 |> orDefault defaultValue 0
    member x.GetInt (name : string, ?defaultValue : Int32) = requestValue |> tryGet name asInt32 |> orDefault defaultValue 0
    member x.GetInt64 (name : string, ?defaultValue : Int64) = requestValue |> tryGet name asInt64 |> orDefault defaultValue 0L
    member x.GetBoolean (name : string, ?defaultValue : Boolean) = requestValue |> tryGet name asBoolean |> orDefault defaultValue false
    member x.GetFloat (name : string, ?defaultValue : float) = requestValue |> tryGet name asFloat |> orDefault defaultValue 0
    member x.GetDecimal (name : string, ?defaultValue : Decimal) = requestValue |> tryGet name asDecimal |> orDefault defaultValue 0M
    member x.GetDateTime (name : string, ?defaultValue : DateTime) = requestValue |> tryGet name asDateTime |> orDefault defaultValue DateTime.MinValue
    member x.GetDateTimeOffset (name : string, ?defaultValue : DateTimeOffset) = requestValue |> tryGet name asDateTimeOffset |> orDefault defaultValue DateTimeOffset.MinValue
    member x.GetGuid (name : string, ?defaultValue : Guid) = requestValue |> tryGet name asGuid |> orDefault defaultValue Guid.Empty
    member x.GetTimeSpan (name : string, ?defaultValue : TimeSpan) = requestValue |> tryGet name asTimeSpan |> orDefault defaultValue TimeSpan.MinValue

    member _.GetStringList (name : string) = requestValue |> tryGet name (asStringList >> Some) |> orDefault None []
    member _.GetStringNonEmptyList (name : string) = requestValue |> tryGet name (asStringNonEmptyList >> Some) |> orDefault None []
    member _.GetInt16List (name : string) = requestValue |> tryGet name (asInt16List >> Some) |> orDefault None []
    member _.GetInt32List (name : string) = requestValue |> tryGet name (asInt32List >> Some) |> orDefault None []
    member _.GetIntList (name : string) = requestValue |> tryGet name (asInt32List >> Some) |> orDefault None []
    member _.GetInt64List (name : string) = requestValue |> tryGet name (asInt64List >> Some) |> orDefault None []
    member _.GetBooleanList (name : string) = requestValue |> tryGet name (asBooleanList >> Some) |> orDefault None []
    member _.GetFloatList (name : string) = requestValue |> tryGet name (asFloatList >> Some) |> orDefault None []
    member _.GetDecimalList (name : string) = requestValue |> tryGet name (asDecimalList >> Some) |> orDefault None []
    member _.GetDateTimeList (name : string) = requestValue |> tryGet name (asDateTimeList >> Some) |> orDefault None []
    member _.GetDateTimeOffsetList (name : string) = requestValue |> tryGet name (asDateTimeOffsetList >> Some) |> orDefault None []
    member _.GetGuidList (name : string) = requestValue |> tryGet name (asGuidList >> Some) |> orDefault None []
    member _.GetTimeSpanList (name : string) = requestValue |> tryGet name (asTimeSpanList >> Some) |> orDefault None []

[<AutoOpen>]
module RequestDataOperators =
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
