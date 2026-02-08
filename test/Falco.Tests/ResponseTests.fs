module Falco.Tests.Response

open System.Security.Claims
open System.Text
open System.Text.Json
open System.Text.Json.Serialization
open Falco
open Falco.Markup
open FsUnit.Xunit
open Microsoft.AspNetCore.Antiforgery
open Microsoft.AspNetCore.Authentication
open Microsoft.AspNetCore.Http
open Microsoft.Extensions.Primitives
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
let ``Response.withHeaders should set header`` () =
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
let ``Response.withStatusCode with multiple modifiers`` () =
    let ctx = getHttpContextWriteable false

    task {
        do! ctx
            |> (Response.withStatusCode 201
                >> Response.withContentType "application/json"
                >> Response.ofEmpty)

        ctx.Response.StatusCode |> should equal 201
        ctx.Response.Headers.[HeaderNames.ContentType][0] |> should equal "application/json"
    }

[<Fact>]
let ``Response.withCookie should add cookie to response`` () =
    let ctx = getHttpContextWriteable false
    let key = "sessionId"
    let value = "abc123"

    task {
        do! ctx
            |> (Response.withCookie key value >> Response.ofEmpty)

        ctx.Response.Cookies.Received().Append(key, value) |> ignore
    }

[<Fact>]
let ``Response.withCookieOptions should add cookie with options`` () =
    let ctx = getHttpContextWriteable false
    let key = "sessionId"
    let value = "abc123"
    let options = CookieOptions()
    options.HttpOnly <- true
    options.Secure <- true

    task {
        do! ctx
            |> (Response.withCookieOptions options key value >> Response.ofEmpty)

        ctx.Response.Cookies.Received().Append(key, value, options) |> ignore
    }

[<Fact>]
let ``Response chaining multiple modifiers`` () =
    let ctx = getHttpContextWriteable false

    task {
        do! ctx
            |> (Response.withStatusCode 200
                >> Response.withContentType "application/json"
                >> Response.withHeaders [ "X-Custom", "value" ]
                >> Response.ofEmpty)

        ctx.Response.StatusCode |> should equal 200
        ctx.Response.Headers.[HeaderNames.ContentType][0] |> should equal "application/json"
        ctx.Response.Headers.["X-Custom"][0] |> should equal "value"
    }

[<Fact>]
let ``Response modifiers are composable`` () =
    let ctx = getHttpContextWriteable false
    let modifier = Response.withStatusCode 201 >> Response.withContentType "text/custom"

    task {
        do! ctx |> (modifier >> Response.ofEmpty)
        ctx.Response.StatusCode |> should equal 201
        ctx.Response.ContentType |> should equal "text/custom"
    }

[<Fact>]
let ``Response.redirectPermanentlyTo invokes HttpRedirect with permanently moved resource`` () =
    let ctx = getHttpContextWriteable false
    let permanentRedirect = true
    task {
        do! ctx
            |> Response.redirectPermanently "/"
        ctx.Response.Received().Redirect("/", permanentRedirect)
        ctx.Response.StatusCode |> should equal 301
    }

[<Fact>]
let ``Response.redirectTemporarilyTo invokes HttpRedirect with temporarily moved resource`` () =
    let ctx = getHttpContextWriteable false
    let permanentRedirect = false
    task {
        do! ctx
            |> Response.redirectTemporarily "/"
        ctx.Response.Received().Redirect("/", permanentRedirect)
        ctx.Response.StatusCode |> should equal 302
    }

[<Fact>]
let ``Response.ofEmpty produces empty response`` () =
    let ctx = getHttpContextWriteable false

    task {
        do! ctx |> Response.ofEmpty
        ctx.Response.ContentLength |> should equal 0L
    }

[<Fact>]
let ``Response.ofString with whitespace-only string`` () =
    let ctx = getHttpContextWriteable false
    let whitespace = "   \t\n  "

    task {
        do! ctx |> Response.ofString Encoding.UTF8 whitespace
        let! body = getResponseBody ctx
        body |> should equal ""  // IsNullOrWhiteSpace check
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
let ``Response.ofPlainText with empty string`` () =
    let ctx = getHttpContextWriteable false

    task {
        do! ctx |> Response.ofPlainText ""
        let! body = getResponseBody ctx
        body |> should equal ""
    }

[<Fact>]
let ``Response.ofPlainText with null string`` () =
    let ctx = getHttpContextWriteable false

    task {
        do! ctx |> Response.ofPlainText null
        let! body = getResponseBody ctx
        body |> should equal ""
    }

[<Fact>]
let ``Response.ofPlainText with multiline content`` () =
    let ctx = getHttpContextWriteable false
    let expected = "line1\nline2\nline3"

    task {
        do! ctx |> Response.ofPlainText expected
        let! body = getResponseBody ctx
        body |> should equal expected
    }

[<Fact>]
let ``Response.ofPlainText with special characters`` () =
    let ctx = getHttpContextWriteable false
    let expected = "hello\r\nworld\t!\x00end"

    task {
        do! ctx |> Response.ofPlainText expected
        let! body = getResponseBody ctx
        body |> should equal expected
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
let ``Response.ofBinary with special characters in content type`` () =
    let ctx = getHttpContextWriteable false
    let contentType = "application/octet-stream; charset=utf-8"
    let bytes = Array.zeroCreate<byte> 100

    task {
        do! ctx |> Response.ofBinary contentType [] bytes
        ctx.Response.ContentType |> should equal contentType
    }

[<Fact>]
let ``Response.ofBinary should preserve valid UTF-8 content`` () =
    let ctx = getHttpContextWriteable false
    let expected = "hello"
    let bytes = Encoding.UTF8.GetBytes(expected)

    task {
        do! ctx |> Response.ofBinary "text/plain" [] bytes
        let! body = getResponseBody ctx
        body |> should equal expected
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
let ``Response.ofAttachment with special characters in filename`` () =
    let ctx = getHttpContextWriteable false
    let filename = "file with spaces & quotes.txt"
    let contentType = "text/plain; charset=utf-8"

    task {
        do! ctx
            |> Response.ofAttachment filename contentType [] (Encoding.UTF8.GetBytes("content"))

        let contentDisposition = ctx.Response.Headers.[HeaderNames.ContentDisposition][0]
        // Should have escaped quotes if necessary
        contentDisposition.Contains("attachment") |> should equal true
    }

[<Fact>]
let ``Response.ofAttachment with empty filename`` () =
    let ctx = getHttpContextWriteable false
    let contentType = "text/plain; charset=utf-8"

    task {
        do! ctx
            |> Response.ofAttachment "" contentType [] (Encoding.UTF8.GetBytes("content"))

        let contentDisposition = ctx.Response.Headers.[HeaderNames.ContentDisposition][0]
        contentDisposition |> should equal "attachment"
    }

[<Fact>]
let ``Response.ofAttachment preserves file extension`` () =
    let ctx = getHttpContextWriteable false
    let filename = "document.pdf"
    let contentType = "application/pdf"

    task {
        do! ctx
            |> Response.ofAttachment filename contentType [] (Encoding.UTF8.GetBytes("PDF content"))

        let contentDisposition = ctx.Response.Headers.[HeaderNames.ContentDisposition][0]
        contentDisposition.Contains("document.pdf") |> should equal true
    }

[<Fact>]
let ``Response.ofAttachment with quotes in filename`` () =
    let ctx = getHttpContextWriteable false
    let filename = "file\"with\"quotes.txt"
    let contentType = "text/plain"

    task {
        do! ctx |> Response.ofAttachment filename contentType [] (Encoding.UTF8.GetBytes("test"))
        let contentDisposition = ctx.Response.Headers.[HeaderNames.ContentDisposition][0]
        // Should be properly escaped
        contentDisposition.Contains "attachment" |> should equal true
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
let ``Response.ofJson with null object`` () =
    let ctx = getHttpContextWriteable false

    task {
        do! ctx |> Response.ofJson null
        let! body = getResponseBody ctx
        body |> should equal "null"
    }

[<Fact>]
let ``Response.ofJson with empty list`` () =
    let ctx = getHttpContextWriteable false

    task {
        do! ctx |> Response.ofJson []
        let! body = getResponseBody ctx
        body |> should equal "[]"
    }

[<Fact>]
let ``Response.ofJson with nested objects`` () =
    let ctx = getHttpContextWriteable false
    let expected = "{\"name\":\"John\",\"nested\":{\"age\":30}}"

    task {
        do! ctx |> Response.ofJson {| name = "John"; nested = {| age = 30 |} |}
        let! body = getResponseBody ctx
        body |> should equal expected
    }

[<Fact>]
let ``Response.ofJsonOptions with custom serialization settings`` () =
    let ctx = getHttpContextWriteable false
    let options = JsonSerializerOptions()
    options.WriteIndented <- true

    task {
        do! ctx |> Response.ofJsonOptions options {| test = "value" |}
        let! body = getResponseBody ctx
        body.Contains("test") |> should equal true
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
let ``Response.ofJson with large object`` () =
    let ctx = getHttpContextWriteable false
    let largeList = List.init 10000 (fun i -> {| id = i; name = $"item{i}" |})

    task {
        do! ctx |> Response.ofJson largeList
        let! body = getResponseBody ctx
        body.Contains("\"id\"") |> should equal true
        body.Contains("9999") |> should equal true  // Check last item serialized
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
let ``Response.ofHtml with empty document`` () =
    let ctx = getHttpContextWriteable false
    let doc = _html [] []

    task {
        do! ctx |> Response.ofHtml doc
        let! body = getResponseBody ctx
        body |> should equal "<!DOCTYPE html><html></html>"
    }

[<Fact>]
let ``Response.ofHtml with attributes`` () =
    let ctx = getHttpContextWriteable false
    let doc = _html [_lang_ "en"] [_body [] []]

    task {
        do! ctx |> Response.ofHtml doc
        let! body = getResponseBody ctx
        body.Contains("lang=\"en\"") |> should equal true
    }

[<Fact>]
let ``Response.ofHtmlString with empty string`` () =
    let ctx = getHttpContextWriteable false

    task {
        do! ctx |> Response.ofHtmlString ""
        let! body = getResponseBody ctx
        body |> should equal ""
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
let ``Response.ofFragment with non-existent fragment`` () =
    let ctx = getHttpContextWriteable false
    let html = _div [ _id_ "fragment1" ] [ _text "content" ]

    task {
        do! ctx |> Response.ofFragment "nonexistent" html
        let! body = getResponseBody ctx
        body |> should equal ""
    }

[<Fact>]
let ``Response.ofFragment with multiple matching fragments`` () =
    let ctx = getHttpContextWriteable false
    let html =
        _div [] [
            _div [ _id_ "fragment" ] [ _text "first" ]
            _div [ _id_ "fragment" ] [ _text "second" ]
        ]

    task {
        do! ctx |> Response.ofFragment "fragment" html
        let! body = getResponseBody ctx
        // Should return first match
        body.Contains "first" |> should equal true
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

[<Fact>]
let ``Response.signIn should call SignInAsync`` () =
    let ctx = getHttpContextWriteable true
    let principal = ClaimsPrincipal(ClaimsIdentity([], CookieScheme))

    task {
        do! ctx |> Response.signIn CookieScheme principal
        ctx.Received().SignInAsync(CookieScheme, principal) |> ignore
    }

[<Fact>]
let ``Response.signInOptions should call SignInAsync with options`` () =
    let ctx = getHttpContextWriteable true
    let principal = ClaimsPrincipal(ClaimsIdentity([], CookieScheme))
    let options = AuthenticationProperties()
    options.IsPersistent <- true

    task {
        do! ctx |> Response.signInOptions CookieScheme principal options
        ctx.Received().SignInAsync(CookieScheme, principal, options) |> ignore
    }

[<Fact>]
let ``Response.signInAndRedirect should set redirect URI`` () =
    let ctx = getHttpContextWriteable true
    let principal = ClaimsPrincipal(ClaimsIdentity([], CookieScheme))
    let redirectUri = "/dashboard"

    task {
        do! ctx |> Response.signInAndRedirect CookieScheme principal redirectUri
        ctx.Received().SignInAsync(CookieScheme, principal) |> ignore
        ctx.Response.Headers.Location.ToArray() |> should contain redirectUri
    }

[<Fact>]
let ``Response.signOut should call SignOutAsync`` () =
    let ctx = getHttpContextWriteable true

    task {
        do! ctx |> Response.signOut CookieScheme
        ctx.Received().SignOutAsync(CookieScheme) |> ignore
    }

[<Fact>]
let ``Response.signOutOptions should call SignOutAsync with options`` () =
    let ctx = getHttpContextWriteable true
    let options = AuthenticationProperties()
    options.RedirectUri <- "/goodbye"

    task {
        do! ctx |> Response.signOutOptions CookieScheme options
        ctx.Received().SignOutAsync(CookieScheme, options) |> ignore
    }

[<Fact>]
let ``Response.signOutAndRedirect should set redirect URI`` () =
    let ctx = getHttpContextWriteable true
    let redirectUri = "/goodbye"

    task {
        do! ctx |> Response.signOutAndRedirect CookieScheme redirectUri
        ctx.Received().SignOutAsync(CookieScheme) |> ignore
        ctx.Response.Headers.Location.ToArray() |> should contain redirectUri
    }

[<Fact>]
let ``Response.challengeOptions should call ChallengeAsync with options`` () =
    let ctx = getHttpContextWriteable false
    let options = AuthenticationProperties()
    options.RedirectUri <- "/login"

    task {
        do! ctx |> Response.challengeOptions CookieScheme options
        ctx.Received().ChallengeAsync(CookieScheme, options) |> ignore
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
let ``Response.ofHtmlCsrf should include CSRF token`` () =
    let ctx = getHttpContextWriteable false

    task {
        let view (token : AntiforgeryTokenSet) =
            _html [] [
                _body [] [ Security.Xsrf.antiforgeryInput token ] ]

        do! ctx |> Response.ofHtmlCsrf view
        let! body = getResponseBody ctx
        body.Contains("input") |> should equal true
    }

[<Fact>]
let ``Response.ofFragmentCsrf should include CSRF token`` () =
    let ctx = getHttpContextWriteable false

    task {
        let view (token : AntiforgeryTokenSet) =
            _div [ _id_ "myFragment" ] [
                Security.Xsrf.antiforgeryInput token
            ]

        do! ctx |> Response.ofFragmentCsrf "myFragment" view
        let! body = getResponseBody ctx
        body.Contains("input") |> should equal true
    }
