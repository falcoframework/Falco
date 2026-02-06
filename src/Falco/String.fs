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
        str.Split(sep)

module internal StringParser =
    /// Helper to wrap .NET tryParser's.
    let tryParseWith (tryParseFunc: string -> bool * _) (str : string) =
        let parsedResult = tryParseFunc str
        match parsedResult with
        | true, v    -> Some v
        | false, _   -> None

    let parseNonEmptyString x =
        if StringUtils.strEmpty x then
            None
        else
            Some x

    let parseBoolean (value : string) =
        let v = value

        match v with
        | x when String.Equals("true", x, StringComparison.OrdinalIgnoreCase) -> Some true
        | x when String.Equals("false", x, StringComparison.OrdinalIgnoreCase) -> Some false
        | v -> tryParseWith Boolean.TryParse v

    let parseInt16 = tryParseWith Int16.TryParse
    let parseInt64 = tryParseWith Int64.TryParse
    let parseInt32 = tryParseWith Int32.TryParse
    let parseFloat = tryParseWith Double.TryParse
    let parseDecimal = tryParseWith Decimal.TryParse
    let parseDateTime = tryParseWith (fun x -> DateTime.TryParse(x, null, DateTimeStyles.RoundtripKind))
    let parseDateTimeOffset = tryParseWith (fun x -> DateTimeOffset.TryParse(x, null, DateTimeStyles.RoundtripKind))
    let parseTimeSpan = tryParseWith TimeSpan.TryParse
    let parseGuid = tryParseWith Guid.TryParse

    /// Attempts to parse, or failwith message.
    let parseOrFail parser msg v =
        match parser v with
        | Some v -> v
        | None   -> failwith msg

    /// Attempts to parse array, returns none for failure.
    let tryParseSeq (parser : string -> 'b option) seq =
        seq
        |> Seq.fold (fun (acc : List<'b>) (a : string) ->
            // accumulate successful parses
            match parser a with
            | Some b ->
                acc.Add(b) |> ignore
                acc
            | None -> acc) (List<'b>())
        |> Seq.cast

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

    let (|IsInt16|_|) = StringParser.parseInt16
    let (|IsInt64|_|) = StringParser.parseInt64
    let (|IsInt32|_|) = StringParser.parseInt32

    let (|IsFloat|_|) (x : string) =
        // Don't parse integers with leading zeros (except "0" itself) as floats
        if x.Length > 1
            && x.StartsWith("0")
            && not(x.Contains('.'))
            && not(x.Contains(',')) then
            None
        // Don't parse large integers as floats - they lose precision
        // Float64 has ~15-16 digits of precision, so integers with more than
        // 15 significant digits would lose precision when stored as float
        elif not(x.Contains('.')) && not(x.Contains(',')) && not(x.Contains('e')) && not(x.Contains('E')) then
            // Count significant digits (excluding leading minus sign)
            let digits = if x.StartsWith("-") then x.Substring(1) else x
            if digits.Length > 15 then
                None
            else
                StringParser.parseFloat x
        else
            StringParser.parseFloat x

    let (|IsDecimal|_|) = StringParser.parseDecimal
    let (|IsDateTime|_|) = StringParser.parseDateTime
    let (|IsDateTimeOffset|_|) = StringParser.parseDateTimeOffset
    let (|IsTimeSpan|_|) = StringParser.parseTimeSpan
    let (|IsGuid|_|) = StringParser.parseGuid

    let (|IsNullOrWhiteSpace|_|) (x : string) =
        match String.IsNullOrWhiteSpace x with
        | true -> Some ()
        | false -> None
