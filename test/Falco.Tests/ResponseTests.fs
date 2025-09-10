module Falco.Tests.Response

open System.Text
open System.Text.Json
open System.Text.Json.Serialization
open Falco
open Falco.Markup
open FsUnit.Xunit
open Microsoft.Net.Http.Headers
open NSubstitute
open Xunit

[<Fact>]
let ``Response.withStatusCode should modify HttpResponse StatusCode`` () =
    let ctx = getHttpContextWriteable false

    let expected = 204

    task {
        do! ctx
            |> (Response.withStatusCode expected >> Response.ofEmpty)

        ctx.Response.StatusCode
        |> should equal expected
    }

[<Fact>]
let ``Response.withHeader should set header`` () =
    let serverName = "Kestrel"
    let ctx = getHttpContextWriteable false

    task {
        do! ctx
            |> (Response.withHeaders [ HeaderNames.Server, serverName ] >> Response.ofEmpty)

        ctx.Response.Headers.[HeaderNames.Server][0]
        |> should equal serverName
    }

[<Fact>]
let ``Response.withContentType should set header`` () =
    let contentType = "text/plain; charset=utf-8"
    let ctx = getHttpContextWriteable false

    task {
        do! ctx
            |> (Response.withContentType contentType>> Response.ofEmpty)

        ctx.Response.Headers.[HeaderNames.ContentType][0]
        |> should equal contentType
    }

[<Fact>]
let ``Response.redirectPermanentlyTo invokes HttpRedirect with permanently moved resource`` () =
    let ctx = getHttpContextWriteable false
    let permanentRedirect = true
    task {
        do! ctx
            |> Response.redirectPermanently "/"
        ctx.Response.Received().Redirect("/", permanentRedirect)
    }

[<Fact>]
let ``Response.redirectTemporarilyTo invokes HttpRedirect with temporarily moved resource`` () =
    let ctx = getHttpContextWriteable false
    let permanentRedirect = false
    task {
        do! ctx
            |> Response.redirectTemporarily "/"
        ctx.Response.Received().Redirect("/", permanentRedirect)
    }


[<Fact>]
let ``Response.ofBinary produces valid inline result from Byte[]`` () =
    let ctx = getHttpContextWriteable false
    let expected = "falco"
    let contentType = "text/plain; charset=utf-8"

    task {
        do! ctx
            |> Response.ofBinary contentType [] (expected |> Encoding.UTF8.GetBytes)

        let! body = getResponseBody ctx
        let contentLength = ctx.Response.ContentLength
        let contentType = ctx.Response.Headers.[HeaderNames.ContentType][0]
        let contentDisposition = ctx.Response.Headers.[HeaderNames.ContentDisposition][0]

        body               |> should equal expected
        contentType        |> should equal contentType
        contentDisposition |> should equal "inline"
    }

[<Fact>]
let ``Response.ofAttachment produces valid attachment result from Byte[]`` () =
    let ctx = getHttpContextWriteable false
    let expected = "falco"
    let contentType = "text/plain; charset=utf-8"

    task {
        do! ctx
            |> Response.ofAttachment "falco.txt" contentType [] (expected |> Encoding.UTF8.GetBytes)

        let! body = getResponseBody ctx
        let contentLength = ctx.Response.ContentLength
        let contentType = ctx.Response.Headers.[HeaderNames.ContentType][0]
        let contentDisposition = ctx.Response.Headers.[HeaderNames.ContentDisposition][0]

        body               |> should equal expected
        contentType        |> should equal contentType
        contentDisposition |> should equal "attachment; filename=\"falco.txt\""
    }

[<Fact>]
let ``Response.ofPlainText produces text/plain result`` () =
    let ctx = getHttpContextWriteable false

    let expected = "hello"

    task {
        do! ctx
            |> Response.ofPlainText expected

        let! body = getResponseBody ctx
        let contentLength = ctx.Response.ContentLength
        let contentType = ctx.Response.Headers.[HeaderNames.ContentType][0]

        body          |> should equal expected
        contentLength |> should equal (int64 (Encoding.UTF8.GetByteCount(expected)))
        contentType   |> should equal "text/plain; charset=utf-8"
    }

[<Fact>]
let ``Response.ofJson produces applicaiton/json result`` () =
    let ctx = getHttpContextWriteable false

    let expected = "{\"Name\":\"John Doe\"}"

    task {
        do! ctx
            |> Response.ofJson { Name = "John Doe"}

        let! body = getResponseBody ctx
        let contentLength = ctx.Response.ContentLength
        let contentType = ctx.Response.Headers.[HeaderNames.ContentType][0]

        body          |> should equal expected
        contentLength |> should equal (int64 (Encoding.UTF8.GetByteCount(expected)))
        contentType   |> should equal "application/json; charset=utf-8"
    }

[<Fact>]
let ``Response.ofJsonOptions produces applicaiton/json result ignoring nulls`` () =
    let ctx = getHttpContextWriteable false

    let expected = "{}"

    task {
        let jsonOptions = JsonSerializerOptions()
        jsonOptions.DefaultIgnoreCondition <- JsonIgnoreCondition.WhenWritingNull

        do! ctx
            |> Response.ofJsonOptions jsonOptions { Name = null }

        let! body = getResponseBody ctx
        let contentLength = ctx.Response.ContentLength
        let contentType = ctx.Response.Headers.[HeaderNames.ContentType][0]

        body          |> should equal expected
        contentLength |> should equal (int64 (Encoding.UTF8.GetByteCount(expected)))
        contentType   |> should equal "application/json; charset=utf-8"
    }

[<Fact>]
let ``Response.ofHtml produces text/html result`` () =
    let ctx = getHttpContextWriteable false

    let expected = "<!DOCTYPE html><html><div class=\"my-class\"><h1>hello</h1></div></html>"

    let doc =
        _html [] [
                _div [ _class_ "my-class" ] [
                        _h1 [] [ _text "hello" ]
                    ]
            ]

    task {
        do! ctx
            |> Response.ofHtml doc

        let! body = getResponseBody ctx
        let contentLength = ctx.Response.ContentLength
        let contentType = ctx.Response.Headers.[HeaderNames.ContentType][0]

        body          |> should equal expected
        contentLength |> should equal (int64 (Encoding.UTF8.GetByteCount(expected)))
        contentType   |> should equal "text/html; charset=utf-8"
    }

[<Fact>]
let ``Response.ofHtmlString produces text/html result`` () =
    let ctx = getHttpContextWriteable false

    let expected = "<!DOCTYPE html><html><div class=\"my-class\"><h1>hello</h1></div></html>"

    task {
        do! ctx
            |> Response.ofHtmlString expected

        let! body = getResponseBody ctx
        let contentLength = ctx.Response.ContentLength
        let contentType = ctx.Response.Headers.[HeaderNames.ContentType][0]

        body          |> should equal expected
        contentLength |> should equal (int64 (Encoding.UTF8.GetByteCount(expected)))
        contentType   |> should equal "text/html; charset=utf-8"
    }


[<Fact>]
let ``Response.challengeAndRedirect`` () =
    let ctx = getHttpContextWriteable false

    task {
        do! ctx
            |> Response.challengeAndRedirect AuthScheme "/"
        ctx.Response.StatusCode |> should equal 401
        ctx.Response.Headers.WWWAuthenticate.ToArray() |> should contain AuthScheme
        ctx.Response.Headers.Location.ToArray() |> should contain "/"
    }

[<Fact>]
let ``Response.ofFragment returns the specified fragment`` () =
    let ctx = getHttpContextWriteable false
    let expected = """<div id="fragment1">1</div>"""

    let html =
        Elem.div [] [
            _div [ _id_ "fragment1" ] [ _text "1" ]
            _div [ _id_ "fragment2" ] [ _text "2" ]
        ]

    task {
        do! ctx |> Response.ofFragment "fragment1" html

        let! body = getResponseBody ctx
        let contentLength = ctx.Response.ContentLength
        let contentType = ctx.Response.Headers.[HeaderNames.ContentType][0]

        body          |> should equal expected
        contentLength |> should equal (int64 (Encoding.UTF8.GetByteCount(expected)))
        contentType   |> should equal "text/html; charset=utf-8"
    }
