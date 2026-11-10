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

    let tryGetOrElse name fn inputValue defaultValue requestValue =
        tryGet name fn requestValue |> Option.orElse inputValue |> Option.defaultValue defaultValue

    let tryGetList name fn requestValue =
        requestValue |> tryGet name (fn >> Some) |> Option.defaultValue []

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

/// Provides typed access to parsed request values.
type RequestData(requestValue : RequestValue) =
    /// Creates request data by parsing the provided key/value collection.
    new(requestData : IDictionary<string, string seq>) = RequestData(RequestValue.parse requestData)

    /// Creates request data from a sequence of key/value pairs.
    new(keyValues : (string * string seq) seq) = RequestData(dict keyValues)

    /// Represents empty request data.
    static member Empty = RequestData RNull

    /// Attempts to retrieve a named value, returning `None` if the name is not found.
    member _.TryGet(name : string) : RequestData option =
        tryGet name (RequestData >> Some) requestValue

    /// Retrieves a named value, or empty request data if the name is not found.
    member _.Get(name : string) : RequestData =
        tryGet name (RequestData >> Some) requestValue
        |> Option.defaultValue RequestData.Empty

    /// Returns the request data as key/value pairs, or an empty list if it is not an object.
    member _.AsKeyValues() =
        asOr (asObject >> Option.map (List.map (fun (k, v) -> k, RequestData v))) [] requestValue

    /// Returns the request data as a list, or an empty list if it is not a list.
    member _.AsList() =
        asOr (asList >> Option.map (List.map RequestData)) [] requestValue

    /// Returns the value as a string, or `defaultValue` (or an empty string) if it cannot be converted.
    member _.AsString ?defaultValue = requestValue |> asString |> orDefault defaultValue ""

    /// Returns the value as a non-null string, or `defaultValue` (or an empty string) if it cannot be converted.
    member _.AsStringNonEmpty ?defaultValue = requestValue |> asStringNonEmpty |> orDefault defaultValue ""

    /// Returns the value as a 16-bit integer, or `defaultValue` (or zero) if it cannot be converted.
    member _.AsInt16 ?defaultValue = requestValue |> asInt16 |> orDefault defaultValue 0s

    /// Returns the value as a 32-bit integer, or `defaultValue` (or zero) if it cannot be converted.
    member _.AsInt32 ?defaultValue = requestValue |> asInt32 |> orDefault defaultValue 0

    /// Returns the value as an integer, or `defaultValue` (or zero) if it cannot be converted.
    member x.AsInt ?defaultValue = x.AsInt32(?defaultValue = defaultValue)

    /// Returns the value as a 64-bit integer, or `defaultValue` (or zero) if it cannot be converted.
    member _.AsInt64 ?defaultValue = requestValue |> asInt64 |> orDefault defaultValue 0L

    /// Returns the value as a Boolean, or `defaultValue` (or `false`) if it cannot be converted.
    member _.AsBoolean ?defaultValue = requestValue |> asBoolean |> orDefault defaultValue false

    /// Returns the value as a floating-point number, or `defaultValue` (or zero) if it cannot be converted.
    member _.AsFloat ?defaultValue = requestValue |> asFloat |> orDefault defaultValue 0.

    /// Returns the value as a decimal number, or `defaultValue` (or zero) if it cannot be converted.
    member _.AsDecimal ?defaultValue = requestValue |> asDecimal |> orDefault defaultValue 0.M

    /// Returns the value as a date and time, or `defaultValue` (or `DateTime.MinValue`) if it cannot be converted.
    member _.AsDateTime ?defaultValue = requestValue |> asDateTime |> orDefault defaultValue DateTime.MinValue

    /// Returns the value as a date and time with an offset, or `defaultValue` (or `DateTimeOffset.MinValue`) if it cannot be converted.
    member _.AsDateTimeOffset ?defaultValue = requestValue |> asDateTimeOffset |> orDefault defaultValue DateTimeOffset.MinValue

    /// Returns the value as a time interval, or `defaultValue` (or `TimeSpan.MinValue`) if it cannot be converted.
    member _.AsTimeSpan ?defaultValue = requestValue |> asTimeSpan |> orDefault defaultValue TimeSpan.MinValue

    /// Returns the value as a GUID, or `defaultValue` (or `Guid.Empty`) if it cannot be converted.
    member _.AsGuid ?defaultValue = requestValue |> asGuid |> orDefault defaultValue Guid.Empty

    /// Attempts to return the value as a string.
    member _.AsStringOption() = asString requestValue

    /// Attempts to return the value as a non-null string.
    member _.AsStringNonEmptyOption() = asStringNonEmpty requestValue

    /// Attempts to return the value as a 16-bit integer.
    member _.AsInt16Option() = asInt16 requestValue

    /// Attempts to return the value as a 32-bit integer.
    member _.AsInt32Option() = asInt32 requestValue

    /// Attempts to return the value as an integer.
    member x.AsIntOption() = x.AsInt32Option()

    /// Attempts to return the value as a 64-bit integer.
    member _.AsInt64Option() = asInt64 requestValue

    /// Attempts to return the value as a Boolean.
    member _.AsBooleanOption() = asBoolean requestValue

    /// Attempts to return the value as a floating-point number.
    member _.AsFloatOption() = asFloat requestValue

    /// Attempts to return the value as a decimal number.
    member _.AsDecimalOption() = asDecimal requestValue

    /// Attempts to return the value as a date and time.
    member _.AsDateTimeOption() = asDateTime requestValue

    /// Attempts to return the value as a date and time with an offset.
    member _.AsDateTimeOffsetOption() = asDateTimeOffset requestValue

    /// Attempts to return the value as a time interval.
    member _.AsTimeSpanOption() = asTimeSpan requestValue

    /// Attempts to return the value as a GUID.
    member _.AsGuidOption() = asGuid requestValue

    /// Returns the value as a list of strings.
    member _.AsStringList() = asStringList requestValue

    /// Returns the value as a list of non-null strings.
    member _.AsStringNonEmptyList() = asStringNonEmptyList requestValue

    /// Returns the value as a list of 16-bit integers.
    member _.AsInt16List() = asInt16List requestValue

    /// Returns the value as a list of 32-bit integers.
    member _.AsInt32List() = asInt32List requestValue

    /// Returns the value as a list of integers.
    member _.AsIntList() = asInt32List requestValue

    /// Returns the value as a list of 64-bit integers.
    member _.AsInt64List() = asInt64List requestValue

    /// Returns the value as a list of Booleans.
    member _.AsBooleanList() = asBooleanList requestValue

    /// Returns the value as a list of floating-point numbers.
    member _.AsFloatList() = asFloatList requestValue

    /// Returns the value as a list of decimal numbers.
    member _.AsDecimalList() = asDecimalList requestValue

    /// Returns the value as a list of dates and times.
    member _.AsDateTimeList() = asDateTimeList requestValue

    /// Returns the value as a list of dates and times with offsets.
    member _.AsDateTimeOffsetList() = asDateTimeOffsetList requestValue

    /// Returns the value as a list of GUIDs.
    member _.AsGuidList() = asGuidList requestValue

    /// Returns the value as a list of time intervals.
    member _.AsTimeSpanList() = asTimeSpanList requestValue

    /// Attempts to retrieve a named value as a string.
    member _.TryGetString (name : string) = tryGet name asString requestValue

    /// Attempts to retrieve a named value as a non-null string.
    member _.TryGetStringNonEmpty (name : string) = tryGet name asStringNonEmpty requestValue

    /// Attempts to retrieve a named value as a 16-bit integer.
    member _.TryGetInt16 (name : string) = tryGet name asInt16 requestValue

    /// Attempts to retrieve a named value as a 32-bit integer.
    member _.TryGetInt32 (name : string) = tryGet name asInt32 requestValue

    /// Attempts to retrieve a named value as an integer.
    member _.TryGetInt (name : string) = tryGet name asInt32 requestValue

    /// Attempts to retrieve a named value as a 64-bit integer.
    member _.TryGetInt64 (name : string) = tryGet name asInt64 requestValue

    /// Attempts to retrieve a named value as a Boolean.
    member _.TryGetBoolean (name : string) = tryGet name asBoolean requestValue

    /// Attempts to retrieve a named value as a floating-point number.
    member _.TryGetFloat (name : string) = tryGet name asFloat requestValue

    /// Attempts to retrieve a named value as a decimal number.
    member _.TryGetDecimal (name : string) = tryGet name asDecimal requestValue

    /// Attempts to retrieve a named value as a date and time.
    member _.TryGetDateTime (name : string) = tryGet name asDateTime requestValue

    /// Attempts to retrieve a named value as a date and time with an offset.
    member _.TryGetDateTimeOffset (name : string) = tryGet name asDateTimeOffset requestValue

    /// Attempts to retrieve a named value as a GUID.
    member _.TryGetGuid (name : string) = tryGet name asGuid requestValue

    /// Attempts to retrieve a named value as a time interval.
    member _.TryGetTimeSpan (name : string) = tryGet name asTimeSpan requestValue

    /// Retrieves a named value as a string, or `defaultValue` (or an empty string) if it cannot be converted.
    member _.GetString (name : string, ?defaultValue : String) = tryGetOrElse name asString defaultValue "" requestValue

    /// Retrieves a named value as a non-null string, or `defaultValue` (or an empty string) if it cannot be converted.
    member _.GetStringNonEmpty (name : string, ?defaultValue : String) = tryGetOrElse name asStringNonEmpty defaultValue "" requestValue

    /// Retrieves a named value as a 16-bit integer, or `defaultValue` (or zero) if it cannot be converted.
    member _.GetInt16 (name : string, ?defaultValue : Int16) = tryGetOrElse name asInt16 defaultValue 0s requestValue

    /// Retrieves a named value as a 32-bit integer, or `defaultValue` (or zero) if it cannot be converted.
    member _.GetInt32 (name : string, ?defaultValue : Int32) = tryGetOrElse name asInt32 defaultValue 0 requestValue

    /// Retrieves a named value as an integer, or `defaultValue` (or zero) if it cannot be converted.
    member _.GetInt (name : string, ?defaultValue : Int32) = tryGetOrElse name asInt32 defaultValue 0 requestValue

    /// Retrieves a named value as a 64-bit integer, or `defaultValue` (or zero) if it cannot be converted.
    member _.GetInt64 (name : string, ?defaultValue : Int64) = tryGetOrElse name asInt64 defaultValue 0L requestValue

    /// Retrieves a named value as a Boolean, or `defaultValue` (or `false`) if it cannot be converted.
    member _.GetBoolean (name : string, ?defaultValue : Boolean) = tryGetOrElse name asBoolean defaultValue false requestValue

    /// Retrieves a named value as a floating-point number, or `defaultValue` (or zero) if it cannot be converted.
    member _.GetFloat (name : string, ?defaultValue : float) = tryGetOrElse name asFloat defaultValue 0 requestValue

    /// Retrieves a named value as a decimal number, or `defaultValue` (or zero) if it cannot be converted.
    member _.GetDecimal (name : string, ?defaultValue : Decimal) = tryGetOrElse name asDecimal defaultValue 0M requestValue

    /// Retrieves a named value as a date and time, or `defaultValue` (or `DateTime.MinValue`) if it cannot be converted.
    member _.GetDateTime (name : string, ?defaultValue : DateTime) = tryGetOrElse name asDateTime defaultValue DateTime.MinValue requestValue

    /// Retrieves a named value as a date and time with an offset, or `defaultValue` (or `DateTimeOffset.MinValue`) if it cannot be converted.
    member _.GetDateTimeOffset (name : string, ?defaultValue : DateTimeOffset) = tryGetOrElse name asDateTimeOffset defaultValue DateTimeOffset.MinValue requestValue

    /// Retrieves a named value as a GUID, or `defaultValue` (or `Guid.Empty`) if it cannot be converted.
    member _.GetGuid (name : string, ?defaultValue : Guid) = tryGetOrElse name asGuid defaultValue Guid.Empty requestValue

    /// Retrieves a named value as a time interval, or `defaultValue` (or `TimeSpan.MinValue`) if it cannot be converted.
    member _.GetTimeSpan (name : string, ?defaultValue : TimeSpan) = tryGetOrElse name asTimeSpan defaultValue TimeSpan.MinValue requestValue

    /// Retrieves a named value as a list of strings.
    member _.GetStringList (name : string) = tryGetList name asStringList requestValue

    /// Retrieves a named value as a list of non-null strings.
    member _.GetStringNonEmptyList (name : string) = tryGetList name asStringNonEmptyList requestValue

    /// Retrieves a named value as a list of 16-bit integers.
    member _.GetInt16List (name : string) = tryGetList name asInt16List requestValue

    /// Retrieves a named value as a list of 32-bit integers.
    member _.GetInt32List (name : string) = tryGetList name asInt32List requestValue

    /// Retrieves a named value as a list of integers.
    member _.GetIntList (name : string) = tryGetList name asInt32List requestValue

    /// Retrieves a named value as a list of 64-bit integers.
    member _.GetInt64List (name : string) = tryGetList name asInt64List requestValue

    /// Retrieves a named value as a list of Booleans.
    member _.GetBooleanList (name : string) = tryGetList name asBooleanList requestValue

    /// Retrieves a named value as a list of floating-point numbers.
    member _.GetFloatList (name : string) = tryGetList name asFloatList requestValue

    /// Retrieves a named value as a list of decimal numbers.
    member _.GetDecimalList (name : string) = tryGetList name asDecimalList requestValue

    /// Retrieves a named value as a list of dates and times.
    member _.GetDateTimeList (name : string) = tryGetList name asDateTimeList requestValue

    /// Retrieves a named value as a list of dates and times with offsets.
    member _.GetDateTimeOffsetList (name : string) = tryGetList name asDateTimeOffsetList requestValue

    /// Retrieves a named value as a list of GUIDs.
    member _.GetGuidList (name : string) = tryGetList name asGuidList requestValue

    /// Retrieves a named value as a list of time intervals.
    member _.GetTimeSpanList (name : string) = tryGetList name asTimeSpanList requestValue

[<AutoOpen>]
module RequestDataOperators =
    /// Retrieves the named value using the `?` operator.
    let inline (?) (requestData : RequestData) (name : string) =
        requestData.Get name

/// Represents form fields and uploaded files from an HTTP request.
[<Sealed>]
type FormData(requestValue : RequestValue, files : IFormFileCollection option) =
    inherit RequestData(requestValue)

    /// Represents an empty form, with no fields or files.
    static member Empty = FormData(RequestValue.RNull, None)

    /// The uploaded files included in the form, if any.
    member _.Files = files

    /// Attempts to retrieve a file from the form by name, returning `None` if
    /// the file is not found or if there are no files.
    member _.TryGetFile(name : string) =
        match files, name with
        | _, IsNullOrWhiteSpace _
        | None, _ -> None
        | Some files, name ->
            match files.GetFile name with
            | f when isNull f -> None
            | f -> Some f
