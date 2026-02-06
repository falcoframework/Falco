namespace Falco.Tests.String

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
