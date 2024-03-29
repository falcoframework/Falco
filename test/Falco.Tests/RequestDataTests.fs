module Falco.Tests.RequestData

open System
open System.IO
open System.Threading
open System.Threading.Tasks
open Falco
open FsUnit.Xunit
open Xunit
open Microsoft.AspNetCore.Http

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
let ``Inline RequestData from form collection should resolve primitives`` () =
    let now = DateTime.Now.ToString()
    let offsetNow = DateTimeOffset.Now.ToString()
    let timespan = TimeSpan.FromSeconds(1.0).ToString()
    let guid = Guid.NewGuid().ToString()

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
            "fdatetime", seq { now }
            "fdatetimeoffset", seq { offsetNow }
            "ftimespan", seq { timespan }
            "fguid", seq { guid }
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
    scr.GetDateTime "fdatetime"             |> should equal (DateTime.Parse(now))
    scr.GetDateTimeOffset "fdatetimeoffset" |> should equal (DateTimeOffset.Parse(offsetNow))
    scr.GetTimeSpan "ftimespan"             |> should equal (TimeSpan.Parse(timespan))
    scr.GetGuid "fguid"                     |> should equal (Guid.Parse(guid))

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
    scr.GetDateTimeList "fdatetime"             |> should equal [DateTime.Parse(now)]
    scr.GetDateTimeOffsetList "fdatetimeoffset" |> should equal [DateTimeOffset.Parse(offsetNow)]
    scr.GetTimeSpanList "ftimespan"             |> should equal [TimeSpan.Parse(timespan)]
    scr.GetGuidList "fguid"                     |> should equal [Guid.Parse(guid)]
