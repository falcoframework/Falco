module Falco.Tests.RequestValue

open Falco
open FsUnit.Xunit
open Xunit

[<Fact>]
let ``RequestValue should return empty RObject for incomplete request body`` () =
    let expected = RObject []
    ""
    |> RequestValue.parseString
    |> should equal expected

[<Fact>]
let ``RequestValue should parse simple pair`` () =
    let expected = RObject [ "name", RString "john doe" ]

    "name=john%20doe"
    |> RequestValue.parseString
    |> should equal expected

[<Theory>]
[<InlineData("true", true)>]
[<InlineData("TRUE", true)>]
[<InlineData("True", true)>]
[<InlineData("false", false)>]
[<InlineData("FALSE", false)>]
[<InlineData("False", false)>]
let ``RequestValue should parse RBool`` (input, expected) =
    let expected = RObject [ "code", RBool expected ]

    $"code={input}"
    |> RequestValue.parseString
    |> should equal expected

[<Theory>]
[<InlineData("on")>]
[<InlineData("ON")>]
[<InlineData("On")>]
[<InlineData("off")>]
[<InlineData("OFF")>]
[<InlineData("Off")>]
[<InlineData("012345")>]
let ``RequestValue should parse RString`` (input) =
    let expected = RObject [ "code", RString input ]

    $"code={input}"
    |> RequestValue.parseString
    |> should equal expected


[<Theory>]
[<InlineData("0", 0.)>]
[<InlineData("1", 1.)>]
[<InlineData("-1", -1.)>]
[<InlineData("0.123456", 0.123456)>]
let ``RequestValue should parse RNumber`` (input, expected) =
    let expected = RObject [ "code", RNumber expected ]

    $"code={input}"
    |> RequestValue.parseString
    |> should equal expected

[<Fact>]
let ``RequestValue should parse int with leading zero as string`` () =
    let expected = RObject [ "code", RString "0123456" ]

    "code=0123456"
    |> RequestValue.parseString
    |> should equal expected

[<Fact>]
let ``RequestValue should parse large int64 values as string to preserve precision`` () =
    // Int64.MaxValue = 9223372036854775807 has 19 digits, exceeds float64 precision
    let expected = RObject [ "id", RString "9223372036854775807" ]

    "id=9223372036854775807"
    |> RequestValue.parseString
    |> should equal expected

[<Fact>]
let ``RequestValue should parse 15 digit integers as RNumber`` () =
    // 15 digits is within float64 precision
    let expected = RObject [ "id", RNumber 123456789012345. ]

    "id=123456789012345"
    |> RequestValue.parseString
    |> should equal expected

[<Fact>]
let ``RequestValue should parse 16+ digit integers as RString`` () =
    // 16+ digits exceeds float64 precision
    let expected = RObject [ "id", RString "1234567890123456" ]

    "id=1234567890123456"
    |> RequestValue.parseString
    |> should equal expected

[<Fact>]
let ``RequestValue should parse multiple simple pairs`` () =
    let expected = RObject [
        "season", RString "summer"
        "orders", RNumber 2 ]

    "season=summer&orders=2"
    |> RequestValue.parseString
    |> should equal expected

[<Fact>]
let ``RequestValue should parse explicit list`` () =
    let expected = RObject [
        "season", RList [ RString "summer"; RString "winter" ] ]
    "season[]=summer&season[]=winter"
    |> RequestValue.parseString
    |> should equal expected

[<Fact>]
let ``RequestValue should parse indexed list`` () =
    let expected = RObject [
        "season", RList [ RString "summer"; RString "winter" ] ]
    "season[0]=summer&season[1]=winter"
    |> RequestValue.parseString
    |> should equal expected

[<Fact>]
let ``RequestValue should parse out-of-order indexed list`` () =
    let expected = RObject [
        "season", RList [ RString "summer"; RString "winter" ] ]
    "season[1]=winter&season[0]=summer"
    |> RequestValue.parseString
    |> should equal expected

[<Fact>]
let ``RequestValue should parse jagged indexed list`` () =
    let expected = RObject [
        "season", RList [ RString "summer"; RNull; RString "winter" ] ]
    "season[0]=summer&season[2]=winter"
    |> RequestValue.parseString
    |> should equal expected

[<Fact>]
let ``RequestValue should parse out-of-order, jagged indexed list`` () =
    let expected = RObject [
        "season", RList [ RString "summer"; RNull; RString "winter" ] ]
    "season[2]=winter&season[0]=summer"
    |> RequestValue.parseString
    |> should equal expected

[<Fact>]
let ``RequestValue should parse object with indexed list`` () =
    let expected = RObject [
        "user", RObject [
            "name", RString "john doe"
            "hobbies", RList [ RString "cycling"; RString "hiking" ] ] ]
    "user.name=john%20doe&user.hobbies[0]=cycling&user.hobbies[1]=hiking"
    |> RequestValue.parseString
    |> should equal expected

[<Fact>]
let ``RequestValue should parse complex`` () =
    let expected = RObject [
        "season", RString "summer"
        "orders", RNumber 2
        "tags", RList [ RString "clothing"; RString "shoes"]
        "user", RObject [
            "name", RString "john"
            "age", RNumber 97
            "hobbies", RList [ RString "cycling"; RString "hiking" ]
            "cards", RList [
                RObject [
                    "num", RNumber 123
                    "kind", RString "visa" ]
                RObject [
                    "num", RNumber 456
                    "kind", RString "visa" ] ] ] ]

    let requestValue =
        seq {
            "season", seq { "summer" }
            "orders", seq { "2" }
            "tags[]", seq { "clothing"; "shoes" }
            "user.name", seq { "john" }
            "user.age", seq { "97" }
            "user.hobbies[]", seq { "cycling"; "hiking" }
            "user.cards[].num", seq { "123"; "456" }
            "user.cards[].kind", seq { "visa"; "amex" }
        }
        |> dict
        |> RequestValue.parse

    requestValue |> should equal expected