namespace Falco

open System
open System.Collections.Generic
open System.Globalization

module internal StringUtils =
    /// Checks if string is null or whitespace.
    let strEmpty str =
        String.IsNullOrWhiteSpace(str)

    /// Checks if string is not null or whitespace.
    let strNotEmpty str =
        not(strEmpty str)

    /// Case & culture insensitive string equality.
    let strEquals s1 s2 =
        String.Equals(s1, s2, StringComparison.InvariantCultureIgnoreCase)

    /// Concats strings.
    let strConcat (lst : string seq) =
        // String.Concat uses a StringBuilder when provided an IEnumerable
        // Url: https://github.com/microsoft/referencesource/blob/master/mscorlib/system/string.cs#L161
        String.Concat(lst)

    /// Splits string into substrings based on separator.
    let strSplit (sep : char array) (str : string) =
        str.Split(sep, StringSplitOptions.RemoveEmptyEntries)

module internal StringParser =
    /// Helper to wrap .NET tryParser's.
    let private tryParseWith (tryParseFunc: string -> bool * _) (str : string) =
        let parsedResult = tryParseFunc str
        match parsedResult with
        | true, v    -> Some v
        | false, _   -> None

    let parseBoolean (value : string) =
        match value with
        | x when String.Equals("true", x, StringComparison.OrdinalIgnoreCase) -> Some true
        | x when String.Equals("false", x, StringComparison.OrdinalIgnoreCase) -> Some false
        | v -> tryParseWith Boolean.TryParse v

    let parseInt16 = tryParseWith Int16.TryParse
    let parseInt64 = tryParseWith Int64.TryParse
    let parseInt32 = tryParseWith Int32.TryParse
    let parseFloat = tryParseWith Double.TryParse
    let parseDecimal = tryParseWith (fun x -> Decimal.TryParse(x, NumberStyles.Number ||| NumberStyles.AllowExponent ||| NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture))
    let parseDateTime = tryParseWith (fun x -> DateTime.TryParse(x, null, DateTimeStyles.RoundtripKind))
    let parseDateTimeOffset = tryParseWith (fun x -> DateTimeOffset.TryParse(x, null, DateTimeStyles.RoundtripKind))
    let parseTimeSpan = tryParseWith TimeSpan.TryParse
    let parseGuid = tryParseWith Guid.TryParse

module internal StringPatterns =
    let (|IsBool|_|) = StringParser.parseBoolean

    let (|IsTrue|_|) =
        function
        | IsBool x when x = true -> Some true
        | _ -> None

    let (|IsFalse|_|) =
        function
        | IsBool x when x = false -> Some false
        | _ -> None

    let (|IsNullOrWhiteSpace|_|) (x : string) =
        match String.IsNullOrWhiteSpace x with
        | true -> Some ()
        | false -> None

    let (|IsInt16|_|) = StringParser.parseInt16
    let (|IsInt64|_|) = StringParser.parseInt64
    let (|IsInt32|_|) = StringParser.parseInt32
    let (|IsFloat|_|) (x : string) = StringParser.parseFloat x
    let (|IsDecimal|_|) = StringParser.parseDecimal
    let (|IsDateTime|_|) = StringParser.parseDateTime
    let (|IsDateTimeOffset|_|) = StringParser.parseDateTimeOffset
    let (|IsTimeSpan|_|) = StringParser.parseTimeSpan
    let (|IsGuid|_|) = StringParser.parseGuid
