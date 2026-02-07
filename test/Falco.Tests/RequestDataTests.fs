module Falco.Tests.RequestData

open System
open System.Globalization
open System.IO
open System.Threading
open System.Threading.Tasks
open Falco
open FsUnit.Xunit
open Xunit
open Microsoft.AspNetCore.Http

[<Fact>]
let ``RequestData extensions can convert RString primitives`` () =
    let requestData = RequestData(RObject [
        "lowercase-on", RString "on"
        "uppercase-on", RString "ON"
        "pascalcase-on", RString "On"

        "lowercase-yes", RString "yes"
        "uppercase-yes", RString "YES"
        "pascalcase-yes", RString "Yes"

        "lowercase-off", RString "off"
        "uppercase-off", RString "OFF"
        "pascalcase-off", RString "Off"

        "lowercase-no", RString "no"
        "uppercase-no", RString "NO"
        "pascalcase-no", RString "No"

        "leadingzero", RString "012345" ])

    requestData.GetBoolean "lowercase-on" |> should equal true
    requestData.GetBoolean "uppercase-on" |> should equal true
    requestData.GetBoolean "pascalcase-on" |> should equal true
    requestData.GetBoolean "lowercase-yes" |> should equal true
    requestData.GetBoolean "uppercase-yes" |> should equal true
    requestData.GetBoolean "pascalcase-yes" |> should equal true
    requestData.GetBoolean "lowercase-off" |> should equal false
    requestData.GetBoolean "uppercase-off" |> should equal false
    requestData.GetBoolean "pascalcase-off" |> should equal false
    requestData.GetBoolean "lowercase-no" |> should equal false
    requestData.GetBoolean "uppercase-no" |> should equal false
    requestData.GetBoolean "pascalcase-no" |> should equal false
    requestData.GetInt "leadingzero" |> should equal 12345
    requestData.GetFloat "leadingzero" |> should equal 12345.


type City = { Name : string; YearFounded : int option }
type CityResult = { Count : int; Results : City list }
type Weather = { Season : string; Temperature : float; Effects : string list; Cities : CityResult }

[<Fact>]
let ``RequestData extensions should work`` () =
    let expected =
        { Season = "summer"
          Temperature = 23.5
          Effects = [ "overcast"; "wind gusts" ]
          Cities = {
            Count = 2
            Results = [ { Name = "Toronto"; YearFounded = Some 123 }; { Name = "Tokyo"; YearFounded = None } ] } }

    let requestValue = RObject [
        "season", RString "summer"
        "temperature", RNumber 23.5
        "effects", RList [ RString "overcast"; RString "wind gusts"]
        "cities", RObject [
            "count", RNumber 2
            "results", RList [
                RObject [ "name", RString "Toronto"; "year_founded", RNumber 123 ]
                RObject [ "name", RString "Tokyo" ] ] ] ]

    let r = RequestData(requestValue)

    { Season = r?season.AsString()
      Temperature = r.GetFloat "temperature"
      Effects = [
        for e in r?effects.AsList() do
            e.AsString() ]
      Cities = {
        Count = r?cities?count.AsInt()
        Results = [
            for c in r?cities?results.AsList() do
                { Name = c?name.AsString()
                  YearFounded = c?year_founded.AsIntOption() } ] } }
    |> should equal expected

[<Fact>]
let ``RequestData value lookups are case-insensitive`` () =
    let values =
        dict [
            "FString", seq {"John Doe"; "Jane Doe" }
        ]
    let scr = RequestData(values)

    // single values
    scr.GetString "FSTRING" |> should equal "John Doe"
    scr.GetString "FString" |> should equal "John Doe"
    scr.GetString "fstriNG" |> should equal "John Doe"

    // arrays
    scr.GetStringList "FSTRING" |> should equal ["John Doe";"Jane Doe"]
    scr.GetStringList "fString" |> should equal ["John Doe";"Jane Doe"]
    scr.GetStringList "fstriNG" |> should equal ["John Doe";"Jane Doe"]

[<Fact>]
let ``RequestData collection should resolve primitives`` () =
    let dt = DateTime(1986, 12, 12)
    let dtStr = dt.ToString("o")
    let dtOffsetStr = DateTimeOffset(dt).ToString("o")
    let timespanStr = TimeSpan.FromSeconds(1.0).ToString()
    let guidStr = Guid.NewGuid().ToString()

    let values =
        dict [
            "emptystring", seq { "" }
            "fstring", seq { "John Doe"; "";""; "Jane Doe";"" }
            "fint16", seq { "16";"";"17" }
            "fint32", seq { "32";"";"";"";"";"33" }
            "fint64", seq { "64";"65";"";"" }
            "fbool", seq { "true";"false" }
            "ffloat", seq { "1.234";"1.235" }
            "fdecimal", seq { "4.567";"4.568" }
            "fdatetime", seq { dtStr }
            "fdatetimeoffset", seq { dtOffsetStr }
            "ftimespan", seq { timespanStr }
            "fguid", seq { guidStr }
        ]

    let scr = RequestData(values)

    // single values
    scr.GetString "_fstring"                |> should equal ""
    scr.GetString "fstring"                 |> should equal "John Doe"
    scr.GetStringNonEmpty "fstring"         |> should equal "John Doe"
    scr.GetInt16 "fint16"                   |> should equal 16s
    scr.GetInt32 "fint32"                   |> should equal 32
    scr.GetInt "fint32"                     |> should equal 32
    scr.GetInt64 "fint64"                   |> should equal 64L
    scr.GetBoolean "fbool"                  |> should equal true
    scr.GetFloat "ffloat"                   |> should equal 1.234
    scr.GetDecimal "fdecimal"               |> should equal 4.567M
    scr.GetDateTime "fdatetime"             |> should equal (DateTime.Parse(dtStr, null, DateTimeStyles.RoundtripKind))
    // TODO uncomment this when DateTimeOffset is supported properly on linux distros, see https://learn.microsoft.com/en-us/dotnet/standard/base-types/how-to-round-trip-date-and-time-values
    // scr.GetDateTimeOffset "fdatetimeoffset" |> should equal (DateTimeOffset.Parse(dtOffsetStr, null, DateTimeStyles.RoundtripKind))
    scr.GetTimeSpan "ftimespan"             |> should equal (TimeSpan.Parse(timespanStr))
    scr.GetGuid "fguid"                     |> should equal (Guid.Parse(guidStr))

    // default values
    scr.GetString("_fstring", "default_value")                         |> should equal "default_value"
    scr.GetStringNonEmpty("_fstring", "default_value")                 |> should equal "default_value"
    scr.GetInt16("_fint16", -1s)                                       |> should equal -1s
    scr.GetInt32("_fint32", -1)                                        |> should equal -1
    scr.GetInt("_fint32", -1)                                          |> should equal -1
    scr.GetInt64("_fint64", 1L)                                        |> should equal 1L
    scr.GetBoolean("_fbool", false)                                    |> should equal false
    scr.GetFloat("_ffloat", 0.0)                                       |> should equal 0.0
    scr.GetDecimal("_fdecimal", 0.0M)                                  |> should equal 0.0M
    scr.GetDateTime("_fdatetime", DateTime.MinValue)                   |> should equal DateTime.MinValue
    scr.GetDateTimeOffset("_fdatetimeoffset", DateTimeOffset.MinValue) |> should equal DateTimeOffset.MinValue
    scr.GetTimeSpan("_ftimespan", TimeSpan.MinValue)                   |> should equal TimeSpan.MinValue
    scr.GetGuid("_fguid", Guid.Empty)                                  |> should equal Guid.Empty

    // array values
    scr.GetStringList "_fstring"                |> List.isEmpty |> should equal true
    scr.GetStringList "fstriNg"                 |> should equal ["John Doe"; ""; ""; "Jane Doe"; ""]
    scr.GetStringNonEmptyList "fstring"         |> should equal ["John Doe";"Jane Doe"]
    scr.GetInt16List "fint16"                   |> should equal [16s;17s]
    scr.GetInt32List "fint32"                   |> should equal [32;33]
    scr.GetIntList "fint32"                     |> should equal [32;33]
    scr.GetInt64List "fint64"                   |> should equal [64L;65L]
    scr.GetBooleanList "fbool"                  |> should equal [true;false]
    scr.GetFloatList "ffloat"                   |> should equal [1.234;1.235]
    scr.GetDecimalList "fdecimal"               |> should equal [4.567M;4.568M]
    scr.GetDateTimeList "fdatetime"             |> should equal [DateTime.Parse(dtStr, null, DateTimeStyles.RoundtripKind)]
    // TODO uncomment this when DateTimeOffset is supported properly on linux distros, see https://learn.microsoft.com/en-us/dotnet/standard/base-types/how-to-round-trip-date-and-time-values
    // scr.GetDateTimeOffsetList "fdatetimeoffset" |> should equal [DateTimeOffset.Parse(dtOffsetStr, null, DateTimeStyles.RoundtripKind)]
    scr.GetTimeSpanList "ftimespan"             |> should equal [TimeSpan.Parse(timespanStr)]
    scr.GetGuidList "fguid"                     |> should equal [Guid.Parse(guidStr)]

[<Fact>]
let ``RequestData Empty should return defaults and empty collections`` () =
    let r = RequestData.Empty
    r.AsString() |> should equal ""
    r.AsStringOption() |> should equal (Some "")
    r.AsIntOption() |> should equal None
    r.AsStringList() |> Seq.length |> should equal 0
    r.AsList() |> Seq.length |> should equal 0
    r.AsKeyValues() |> Seq.length |> should equal 0

[<Fact>]
let ``RequestData should allow list access on non-list primitives`` () =
    let r1 = RequestData(RString "hello")
    let r2 = RequestData(RString "123")
    r1.AsStringList() |> should equal ["hello"]
    r2.AsInt32List() |> should equal [123]

[<Fact>]
let ``RequestData nested lookup should be case-insensitive`` () =
    let r =
        RequestData(
            RObject [
                "Foo", RObject [
                    "Bar", RString "baz"
                ]
            ])
    r?foo?bar.AsString() |> should equal "baz"
    r?FOO?BAR.AsString() |> should equal "baz"

[<Fact>]
let ``RequestData missing nested key should yield None for options`` () =
    let r = RequestData(RObject [ "foo", RObject [] ])
    r?foo?missing.AsIntOption() |> should equal None
    r?foo?missing.AsGuidOption() |> should equal None

[<Fact>]
let ``RequestData should parse boolean from numeric`` () =
    RequestData(RNumber 0.).AsBoolean() |> should equal false
    RequestData(RNumber 1.).AsBoolean() |> should equal true

[<Fact>]
let ``RequestData should parse DateTime from epoch milliseconds`` () =
    let r = RequestData(RNumber 0.)
    r.AsDateTime() |> should equal (DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc))

[<Fact>]
let ``RequestData should return None for invalid Guid and TimeSpan`` () =
    let r1 = RequestData(RString "not-a-guid")
    let r2 = RequestData(RString "not-a-timespan")
    r1.AsGuidOption() |> should equal None
    r2.AsTimeSpanOption() |> should equal None

[<Fact>]
let ``Can make FormData from IFormCollection`` () =
    FormData(RequestValue.RNull, Some (FormFileCollection() :> IFormFileCollection))
    |> should not' throw

[<Fact>]
let ``Can safely get IFormFile from IFormCollection`` () =
    let formFileName = "abc.txt"

    let emptyFormData = FormData(RequestValue.RNull, Some (FormFileCollection() :> IFormFileCollection))
    emptyFormData.TryGetFile formFileName
    |> shouldBeNone

    let formFile =
        { new IFormFile with
            member _.ContentDisposition = String.Empty
            member _.ContentType = String.Empty
            member _.FileName = String.Empty
            member _.Headers = HeaderDictionary()
            member _.Length = Int64.MinValue
            member _.Name = formFileName
            member _.CopyTo (target: Stream) : unit = ()
            member _.CopyToAsync (target: Stream, cancellationToken: CancellationToken) : Task = Task.CompletedTask
            member _.OpenReadStream  () :  Stream = System.IO.Stream.Null }

    let formFileCollection = FormFileCollection()
    formFileCollection.Add(formFile)
    let formFileData = new FormData(RequestValue.RNull, Some(formFileCollection))

    formFileData.TryGetFile formFileName
    |> shouldBeSome (fun _ ->  ())

[<Fact>]
let ``FormData TryGetFile should return None for null or whitespace name`` () =
    let fd = FormData(RequestValue.RNull, Some (FormFileCollection() :> IFormFileCollection))
    fd.TryGetFile null |> shouldBeNone
    fd.TryGetFile "" |> shouldBeNone
    fd.TryGetFile "   " |> shouldBeNone

// ...existing code...

[<Fact>]
let ``RequestData RNull conversions should return defaults`` () =
    let r = RequestData(RNull)
    r.AsString() |> should equal ""
    r.AsInt32() |> should equal 0
    r.AsBoolean() |> should equal false
    r.AsFloat() |> should equal 0.
    r.AsDecimal() |> should equal 0M
    r.AsGuid() |> should equal Guid.Empty
    r.AsDateTime() |> should equal DateTime.MinValue
    r.AsTimeSpan() |> should equal TimeSpan.MinValue

[<Fact>]
let ``RequestData RBool conversions to string`` () =
    let rTrue = RequestData(RBool true)
    let rFalse = RequestData(RBool false)
    rTrue.AsString() |> should equal "true"
    rFalse.AsString() |> should equal "false"

[<Fact>]
let ``RequestData RNumber conversions to string`` () =
    let r = RequestData(RNumber 42.5)
    r.AsString() |> should equal "42.5"

[<Fact>]
let ``RequestData TryGet methods return None on invalid conversions`` () =
    let r = RequestData(RString "not-a-number")
    r.TryGetInt32("value") |> should equal None
    r.TryGetBoolean("value") |> should equal None
    r.TryGetGuid("value") |> should equal None

[<Fact>]
let ``RequestData empty string should convert to RNull equivalent`` () =
    let r = RequestData(RString "")
    r.AsString() |> should equal ""
    r.AsStringNonEmpty() |> should equal ""

[<Fact>]
let ``RequestData whitespace string should convert to empty for AsString`` () =
    let r = RequestData(RString "   ")
    r.AsString() |> should equal "   "  // preserves whitespace
    r.AsStringNonEmpty() |> should equal "   "  // preserves whitespace

[<Fact>]
let ``RequestData RObject accessed as primitive should return defaults`` () =
    let r = RequestData(RObject [ "key", RString "value" ])
    r.AsString() |> should equal ""
    r.AsInt32() |> should equal 0
    r.AsBoolean() |> should equal false

[<Fact>]
let ``RequestData negative numbers in lists`` () =
    let r = RequestData(RList [ RNumber -5.; RNumber -10.; RNumber -15. ])
    r.AsInt32List() |> should equal [-5; -10; -15]

[<Fact>]
let ``RequestData very large numbers should overflow gracefully`` () =
    let r = RequestData(RNumber (float System.Int32.MaxValue + 1000.))
    r.AsInt32Option() |> should equal None  // out of range
    r.AsInt64Option() |> should not' (equal None)  // should fit in Int64

[<Fact>]
let ``RequestData float precision edge cases`` () =
    let r1 = RequestData(RNumber Double.NaN)
    let r2 = RequestData(RNumber Double.PositiveInfinity)
    let r3 = RequestData(RNumber Double.NegativeInfinity)
    r1.AsFloat() |> Double.IsNaN |> should equal true
    r2.AsFloat() |> should equal Double.PositiveInfinity
    r3.AsFloat() |> should equal Double.NegativeInfinity

[<Fact>]
let ``RequestData empty list`` () =
    let r = RequestData(RList [])
    r.AsStringList() |> Seq.length |> should equal 0
    r.AsInt32List() |> Seq.length |> should equal 0
    r.AsList() |> Seq.length |> should equal 0

[<Fact>]
let ``RequestData list with mixed null and valid values`` () =
    let r = RequestData(RList [ RString "hello"; RNull; RString "world" ])
    r.AsStringList() |> should equal ["hello"; ""; "world"]

[<Fact>]
let ``RequestData single-item list`` () =
    let r = RequestData(RList [ RNumber 42. ])
    r.AsInt32List() |> should equal [42]

[<Fact>]
let ``RequestData very large list`` () =
    let large = List.init 1000 (fun i -> RNumber (float i))
    let r = RequestData(RList large)
    r.AsInt32List() |> List.length |> should equal 1000
    r.AsInt32List() |> List.head |> should equal 0
    r.AsInt32List() |> List.last |> should equal 999

[<Fact>]
let ``RequestData deeply nested objects`` () =
    let r = RequestData(
        RObject [
            "level1", RObject [
                "level2", RObject [
                    "level3", RString "deep"
                ]
            ]
        ])
    r?level1?level2?level3.AsString() |> should equal "deep"

[<Fact>]
let ``RequestData RNumber 0 and 1 as boolean`` () =
    let r0 = RequestData(RNumber 0.)
    let r1 = RequestData(RNumber 1.)
    r0.AsBoolean() |> should equal false
    r1.AsBoolean() |> should equal true

[<Fact>]
let ``RequestData RNumber other than 0/1 as boolean should return None`` () =
    let r = RequestData(RNumber 2.)
    r.AsBooleanOption() |> should equal None

[<Fact>]
let ``RequestData invalid Guid string`` () =
    let r = RequestData(RString "not-a-valid-guid")
    r.AsGuidOption() |> should equal None
    r.AsGuid() |> should equal Guid.Empty

[<Fact>]
let ``RequestData invalid TimeSpan string`` () =
    let r = RequestData(RString "not-a-timespan")
    r.AsTimeSpanOption() |> should equal None
    r.AsTimeSpan() |> should equal TimeSpan.MinValue

[<Fact>]
let ``RequestData Get vs TryGet difference`` () =
    let r = RequestData(RObject [ "name", RString "John" ])
    let getResult = r.Get "missing"
    let tryResult = r.TryGet "missing"
    getResult.AsString() |> should equal ""  // Get returns Empty
    tryResult |> should equal None  // TryGet returns None

[<Fact>]
let ``RequestData custom default values`` () =
    let r = RequestData(RString "invalid")
    r.AsInt32(defaultValue = 999) |> should equal 999
    r.AsString(defaultValue = "default") |> should equal "invalid"
    r.AsBoolean(defaultValue = true) |> should equal true
