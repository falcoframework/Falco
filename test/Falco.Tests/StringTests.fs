namespace Falco.Tests.String

open System
open System.Collections.Generic
open System.IO
open System.Text
open System.Text.Json
open System.Text.Json.Serialization
open Falco
open FsUnit.Xunit
open NSubstitute
open Xunit
open Microsoft.AspNetCore.Routing
open Microsoft.Net.Http.Headers
open Microsoft.AspNetCore.Http
open Microsoft.Extensions.Primitives

module StringUtils =
    [<Fact>]
    let ``StringUtils.strEmpty should return true for null or empty strings`` () =
        StringUtils.strEmpty null |> should be True
        StringUtils.strEmpty "" |> should be True
        StringUtils.strEmpty "   " |> should be True

    [<Fact>]
    let ``StringUtils.strNotEmpty should return true for non-empty strings`` () =
        StringUtils.strNotEmpty "hello" |> should be True
        StringUtils.strNotEmpty "   " |> should be False

    [<Fact>]
    let ``StringUtils.strEquals should compare strings case-insensitively`` () =
        StringUtils.strEquals "hello" "HELLO" |> should be True
        StringUtils.strEquals "hello" "world" |> should be False

    [<Fact>]
    let ``StringUtils.strConcat should concatenate a sequence of strings`` () =
        StringUtils.strConcat ["hello"; " "; "world"] |> should equal "hello world"
        StringUtils.strConcat [] |> should equal ""

    [<Fact>]
    let ``StringUtils.strSplit should split a string by given separators`` () =
        StringUtils.strSplit [|','; ' '|] "hello, world" |> should equal [|"hello"; "world"|]
        StringUtils.strSplit [|','|] " hello, world" |> should equal [|" hello"; " world"|]
        StringUtils.strSplit [|' '|] "hello world" |> should equal [|"hello"; "world"|]
        StringUtils.strSplit [|','|] "hello world" |> should equal [|"hello world"|]
        StringUtils.strSplit [|','; ' '|] "   " |> should equal [||]

module StringParser =
    [<Fact>]
    let ``StringParser.parseBoolean should parse true/false case-insensitively`` () =
        StringParser.parseBoolean "true" |> should equal (Some true)
        StringParser.parseBoolean "True" |> should equal (Some true)
        StringParser.parseBoolean "TRUE" |> should equal (Some true)
        StringParser.parseBoolean "false" |> should equal (Some false)
        StringParser.parseBoolean "False" |> should equal (Some false)
        StringParser.parseBoolean "FALSE" |> should equal (Some false)
        StringParser.parseBoolean "notabool" |> should equal None
        StringParser.parseBoolean "" |> should equal None

    [<Fact>]
    let ``StringParser.parseInt16 should parse valid int16`` () =
        StringParser.parseInt16 "123" |> should equal (Some 123s)
        StringParser.parseInt16 "-32768" |> should equal (Some -32768s)
        StringParser.parseInt16 "32767" |> should equal (Some 32767s)
        StringParser.parseInt16 "notanint" |> should equal None
        StringParser.parseInt16 "" |> should equal None

    [<Fact>]
    let ``StringParser.parseInt32 should parse valid int32`` () =
        StringParser.parseInt32 "123" |> should equal (Some 123)
        StringParser.parseInt32 "-2147483648" |> should equal (Some -2147483648)
        StringParser.parseInt32 "2147483647" |> should equal (Some 2147483647)
        StringParser.parseInt32 "notanint" |> should equal None
        StringParser.parseInt32 "" |> should equal None

    [<Fact>]
    let ``StringParser.parseInt64 should parse valid int64`` () =
        StringParser.parseInt64 "123" |> should equal (Some 123L)
        StringParser.parseInt64 "-9223372036854775808" |> should equal (Some -9223372036854775808L)
        StringParser.parseInt64 "9223372036854775807" |> should equal (Some 9223372036854775807L)
        StringParser.parseInt64 "notanint" |> should equal None
        StringParser.parseInt64 "" |> should equal None

    [<Fact>]
    let ``StringParser.parseFloat should parse valid floats`` () =
        StringParser.parseFloat "123.45" |> should equal (Some 123.45)
        StringParser.parseFloat "-123.45" |> should equal (Some -123.45)
        StringParser.parseFloat "1e10" |> should equal (Some 1e10)
        StringParser.parseFloat "notafloat" |> should equal None
        StringParser.parseFloat "" |> should equal None

    [<Fact>]
    let ``StringParser.parseDecimal should parse valid decimals`` () =
        StringParser.parseDecimal "123.45" |> should equal (Some 123.45M)
        StringParser.parseDecimal "-123.45" |> should equal (Some -123.45M)
        StringParser.parseDecimal "1e10" |> should equal (Some 1e10M)
        StringParser.parseDecimal "notadecimal" |> should equal None
        StringParser.parseDecimal "" |> should equal None

    [<Fact>]
    let ``StringParser.parseDateTime should parse valid DateTime`` () =
        StringParser.parseDateTime "2021-01-01T12:00:00Z" |> should equal (Some (DateTime(2021, 1, 1, 12, 0, 0, DateTimeKind.Utc)))
        StringParser.parseDateTime "notadatetime" |> should equal None
        StringParser.parseDateTime "" |> should equal None

    [<Fact>]
    let ``StringParser.parseDateTimeOffset should parse valid DateTimeOffset`` () =
        StringParser.parseDateTimeOffset "2021-01-01T12:00:00Z" |> should equal (Some (DateTimeOffset(2021, 1, 1, 12, 0, 0, TimeSpan.Zero)))
        StringParser.parseDateTimeOffset "notadatetimeoffset" |> should equal None
        StringParser.parseDateTimeOffset "" |> should equal None

    [<Fact>]
    let ``StringParser.parseTimeSpan should parse valid TimeSpan`` () =
        StringParser.parseTimeSpan "12:00:00" |> should equal (Some (TimeSpan(12, 0, 0)))
        StringParser.parseTimeSpan "notatimespan" |> should equal None
        StringParser.parseTimeSpan "" |> should equal None

    [<Fact>]
    let ``StringParser.parseGuid should parse valid GUIDs`` () =
        let guidStr = "d3b07384-d9a0-4c19-9a0c-0305e1b1c8f2"
        let guid = Guid.Parse(guidStr)
        StringParser.parseGuid guidStr |> should equal (Some guid)
        StringParser.parseGuid "notaguid" |> should equal None
        StringParser.parseGuid "" |> should equal None
