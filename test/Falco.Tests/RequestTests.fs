module Falco.Tests.Request

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

[<Fact>]
let ``Request.getVerb should return HttpVerb from HttpContext`` () =
    let ctx = getHttpContextWriteable false
    ctx.Request.Method <- "GET"

    Request.getVerb ctx
    |> should equal GET

[<Fact>]
let ``Request.getCookies`` () =
    let ctx = getHttpContextWriteable false
    ctx.Request.Cookies <- Map.ofList ["name", "falco"] |> cookieCollection

    let cookies= Request.getCookies ctx
    cookies?name.AsString() |> should equal "falco"

[<Fact>]
let ``Request.getHeaders should work for present and missing header names`` () =
    let serverName = "Kestrel"
    let ctx = getHttpContextWriteable false
    ctx.Request.Headers.Add(HeaderNames.Server, StringValues(serverName))

    let headers =  Request.getHeaders ctx

    headers.GetString HeaderNames.Server |> should equal serverName
    headers.TryGetString "missing" |> should equal None

[<Fact>]
let ``Request.getRouteValues should return Map<string, string> from HttpContext`` () =
    let ctx = getHttpContextWriteable false
    ctx.Request.RouteValues <- RouteValueDictionary({|name="falco"|})

    let route = Request.getRoute ctx

    route.GetString "name"
    |> should equal "falco"

[<Fact>]
let ``Request.getRoute should preserve large int64 values as strings`` () =
    // Regression test for https://github.com/falcoframework/Falco/issues/149
    let ctx = getHttpContextWriteable false
    ctx.Request.RouteValues <- RouteValueDictionary()
    ctx.Request.RouteValues.Add("id", "9223372036854775807") // Int64.MaxValue as string

    let route = Request.getRoute ctx

    // Should return the original string, not scientific notation
    route.GetString "id"
    |> should equal "9223372036854775807"

    // Should also be parseable as int64
    route.GetInt64 "id"
    |> should equal 9223372036854775807L

[<Fact>]
let ``Request.mapJson`` () =
    let ctx = getHttpContextWriteable false
    use ms = new MemoryStream(Encoding.UTF8.GetBytes("{\"name\":\"falco\"}"))
    ctx.Request.ContentLength.Returns(13L) |> ignore
    ctx.Request.Body.Returns(ms) |> ignore

    let handle json : HttpHandler =
        json.Name |> should equal "falco"
        Response.ofEmpty

    Request.mapJson handle ctx

[<Fact>]
let ``Request.mapJsonOption`` () =
    let ctx = getHttpContextWriteable false
    use ms = new MemoryStream(Encoding.UTF8.GetBytes("{\"name\":\"falco\",\"age\":null}"))
    ctx.Request.ContentLength.Returns(22L) |> ignore
    ctx.Request.Body.Returns(ms) |> ignore

    let handle json : HttpHandler =
        json.Name |> should equal "falco"
        Response.ofEmpty

    let options = JsonSerializerOptions()
    options.AllowTrailingCommas <- true
    options.PropertyNameCaseInsensitive <- true
    options.DefaultIgnoreCondition <- JsonIgnoreCondition.WhenWritingNull

    Request.mapJsonOptions options handle ctx

[<Fact>]
let ``Request.mapCookies`` () =
    let ctx = getHttpContextWriteable false
    ctx.Request.Cookies <- Map.ofList ["name", "falco"] |> cookieCollection

    let handle name : HttpHandler =
        name |> should equal "falco"
        Response.ofEmpty

    Request.mapCookies (fun r -> r.GetString "name") handle ctx

[<Fact>]
let ``Request.mapHeaders`` () =
    let serverName = "Kestrel"
    let ctx = getHttpContextWriteable false
    ctx.Request.Headers.Add(HeaderNames.Server, StringValues(serverName))

    let handle server : HttpHandler =
        server |> should equal serverName
        Response.ofEmpty

    Request.mapHeaders (fun r -> r.GetString HeaderNames.Server) handle ctx

[<Fact>]
let ``Request.mapRoute`` () =
    let ctx = getHttpContextWriteable false
    ctx.Request.RouteValues <- RouteValueDictionary({|name="falco"|})

    let handle name : HttpHandler =
        name |> should equal "falco"
        Response.ofEmpty

    Request.mapRoute (fun r -> r.GetString "name") handle ctx

[<Fact>]
let ``Request.mapQuery`` () =
    let ctx = getHttpContextWriteable false
    let query = Dictionary<string, StringValues>()
    query.Add("name", StringValues("falco"))
    ctx.Request.Query <- QueryCollection(query)

    let handle name : HttpHandler =
        name |> should equal "falco"
        Response.ofEmpty

    Request.mapQuery (fun c -> c.GetString "name") handle ctx

[<Fact>]
let ``Request.mapForm`` () =
    let ctx = getHttpContextWriteable false
    let form = Dictionary<string, StringValues>()
    form.Add("name", StringValues("falco"))
    ctx.Request.ReadFormAsync().Returns(FormCollection(form)) |> ignore

    let handle name : HttpHandler =
        name |> should equal "falco"
        Response.ofEmpty

    Request.mapForm (fun f -> f?name.AsString()) handle ctx |> ignore
    Request.mapFormSecure (fun f -> f.GetString "name") handle Response.ofEmpty ctx |> ignore

[<Fact>]
let ``Request.getForm from Stream`` () =
    let ctx = getHttpContextWriteable false
    let body =
        "--9051914041544843365972754266\r\n" +
        "Content-Disposition: form-data; name=\"name\"\r\n" +
        "\r\n" +
        "falco\r\n" +
        "--9051914041544843365972754266\r\n" +
        "Content-Disposition: form-data; name=\"file1\"; filename=\"a.txt\"\r\n" +
        "Content-Type: text/plain\r\n" +
        "\r\n" +
        "Content of a.txt.\r\n" +
        "\r\n" +
        "--9051914041544843365972754266\r\n" +
        "Content-Disposition: form-data; name=\"file2\"; filename=\"a.html\"\r\n" +
        "Content-Type: text/html\r\n" +
        "\r\n" +
        "<!DOCTYPE html><title>Content of a.html.</title>\r\n" +
        "\r\n" +
        "--9051914041544843365972754266--\r\n";

    use ms = new MemoryStream(Encoding.UTF8.GetBytes(body))
    ctx.Request.Body.Returns(ms) |> ignore

    let contentType = "multipart/form-data;boundary=\"9051914041544843365972754266\""
    ctx.Request.ContentType <- contentType

    let handle (requestValue : string, files : IFormFileCollection option) : HttpHandler =
        requestValue |> should equal "falco"
        files |> shouldBeSome (fun x ->
            x.Count |> should equal 2

            // can we access the files?
            use ms = new MemoryStream()
            use st1 = x.[0].OpenReadStream()
            st1.CopyTo(ms)

            ms.SetLength(0)
            use st2 = x.[1].OpenReadStream()
            st1.CopyTo(ms))
        Response.ofEmpty

    Request.mapForm (fun f -> f.GetString "name", f.Files) handle ctx |> ignore
    Request.mapFormSecure (fun f -> f.GetString "name", f.Files) handle Response.ofEmpty ctx |> ignore

[<Fact>]
let ``Request.mapJson Transfer-Encoding: chunked`` () =
    let ctx = getHttpContextWriteable false
    use ms = new MemoryStream(Encoding.UTF8.GetBytes("{\"name\":\"falco\"}"))
    // Simulate chunked transfer encoding
    ctx.Request.Headers.Add(HeaderNames.TransferEncoding, "chunked")
    ctx.Request.Headers.Add(HeaderNames.ContentType, "application/json")

    ctx.Request.Body.Returns(ms) |> ignore

    let handle json : HttpHandler =
        json.Name |> should equal "falco"
        Response.ofEmpty

    Request.mapJson handle ctx
