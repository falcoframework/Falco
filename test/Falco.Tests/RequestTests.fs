module Falco.Tests.Request

open System
open System.Collections.Generic
open System.IO
open System.Text
open System.Text.Json
open System.Text.Json.Serialization
open System.Threading
open Falco
open FsUnit.Xunit
open NSubstitute
open Xunit
open Microsoft.AspNetCore.Authentication
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
let ``Request.getVerb should handle all HTTP methods`` () =
    let ctx = getHttpContextWriteable false

    ctx.Request.Method <- "POST"
    Request.getVerb ctx |> should equal POST

    ctx.Request.Method <- "PUT"
    Request.getVerb ctx |> should equal PUT

    ctx.Request.Method <- "PATCH"
    Request.getVerb ctx |> should equal PATCH

    ctx.Request.Method <- "DELETE"
    Request.getVerb ctx |> should equal DELETE

    ctx.Request.Method <- "OPTIONS"
    Request.getVerb ctx |> should equal OPTIONS

[<Fact>]
let ``Request.getVerb should return ANY for unknown methods`` () =
    let ctx = getHttpContextWriteable false
    ctx.Request.Method <- "CUSTOM"

    Request.getVerb ctx |> should equal ANY

[<Fact>]
let ``Request.bodyString handler should provide body string`` () =
    let ctx = getHttpContextWriteable false
    let bodyContent = "test content"
    use ms = new MemoryStream(Encoding.UTF8.GetBytes(bodyContent))
    ctx.Request.Body <- ms

    let handle body : HttpHandler =
        body |> should equal bodyContent
        Response.ofEmpty

    Request.bodyString handle ctx

[<Fact>]
let ``Request.getBodyString should read request body as string`` () =
    let ctx = getHttpContextWriteable false
    let bodyContent = "Hello, World!"
    use ms = new MemoryStream(Encoding.UTF8.GetBytes(bodyContent))
    ctx.Request.Body <- ms

    task {
        let! body = Request.getBodyString ctx
        body |> should equal bodyContent
    }

[<Fact>]
let ``Request.getBodyStringOptions should enforce max size limit`` () =
    let ctx = getHttpContextWriteable false
    let largeContent = String.replicate (11 * 1024 * 1024) "x"
    use ms = new MemoryStream(Encoding.UTF8.GetBytes(largeContent))
    ctx.Request.Body <- ms

    task {
        let maxSize = 10L * 1024L * 1024L
        let! ex = Assert.ThrowsAsync<InvalidOperationException>(
            fun () -> Request.getBodyStringOptions maxSize ctx)

        ex.Message.Contains "exceeds maximum size" |> should equal true
    }

[<Fact>]
let ``Request.getBodyString should handle empty body`` () =
    let ctx = getHttpContextWriteable false
    use ms = new MemoryStream()
    ctx.Request.Body <- ms

    task {
        let! body = Request.getBodyString ctx
        body |> should equal ""
    }

[<Fact>]
let ``Request.getCookies`` () =
    let ctx = getHttpContextWriteable false
    ctx.Request.Cookies <- Map.ofList ["name", "falco"] |> cookieCollection

    let cookies= Request.getCookies ctx
    cookies?name.AsString() |> should equal "falco"

[<Fact>]
let ``Request.getCookies should handle multiple values`` () =
    let ctx = getHttpContextWriteable false
    ctx.Request.Cookies <-
        Map.ofList ["session", "abc123"; "theme", "dark"]
        |> cookieCollection

    let cookies = Request.getCookies ctx
    cookies.GetString "session" |> should equal "abc123"
    cookies.GetString "theme" |> should equal "dark"

[<Fact>]
let ``Request.getHeaders should work for present and missing header names`` () =
    let serverName = "Kestrel"
    let ctx = getHttpContextWriteable false
    ctx.Request.Headers.Add(HeaderNames.Server, StringValues(serverName))

    let headers =  Request.getHeaders ctx

    headers.GetString HeaderNames.Server |> should equal serverName
    headers.TryGetString "missing" |> should equal None

[<Fact>]
let ``Request.getHeaders should be case-insensitive`` () =
    let ctx = getHttpContextWriteable false
    ctx.Request.Headers.Add("X-Custom-Header", StringValues("value123"))

    let headers = Request.getHeaders ctx
    headers.GetString "x-custom-header" |> should equal "value123"
    headers.GetString "X-CUSTOM-HEADER" |> should equal "value123"

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
let ``Request.getQuery should exclude route values`` () =
    let ctx = getHttpContextWriteable false
    ctx.Request.RouteValues <- RouteValueDictionary({|id="123"|})

    let query = Dictionary<string, StringValues>()
    query.Add("filter", StringValues("active"))
    ctx.Request.Query <- QueryCollection(query)

    let queryData = Request.getQuery ctx
    queryData.GetString "filter" |> should equal "active"
    queryData.TryGetString "id" |> should equal None

[<Fact>]
let ``Request.getForm should handle urlencoded form data`` () =
    let ctx = getHttpContextWriteable false
    ctx.Request.ContentType <- "application/x-www-form-urlencoded"

    let form = Dictionary<string, StringValues>()
    form.Add("username", StringValues("john"))
    form.Add("password", StringValues("secret"))
    let f = FormCollection(form)

    ctx.Request.ReadFormAsync().Returns(f) |> ignore
    ctx.Request.ReadFormAsync(Arg.Any<CancellationToken>()).Returns(f) |> ignore

    task {
        let! formData = Request.getForm ctx
        formData.GetString "password" |> should equal "secret"
        formData.GetString "username" |> should equal "john"
    }

[<Fact>]
let ``Request.getForm should detect multipart form data and stream`` () =
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

    task {
        let! formData = Request.getForm ctx
        formData.GetString "name" |> should equal "falco"
        formData.Files |> Option.map Seq.length |> Option.defaultValue 0 |> should equal 2

        // read file 1
        use file1Stream = formData.Files.Value.[0].OpenReadStream()
        use reader1 = new StreamReader(file1Stream)
        let! file1Content = reader1.ReadToEndAsync()
        file1Content |> should equal "Content of a.txt.\r\n"

        // read file 2
        use file2Stream = formData.Files.Value.[1].OpenReadStream()
        use reader2 = new StreamReader(file2Stream)
        let! file2Content = reader2.ReadToEndAsync()
        file2Content |> should equal "<!DOCTYPE html><title>Content of a.html.</title>\r\n"
    }

[<Fact>]
let ``Request.getJson should deserialize with case insensitive property names`` () =
    let ctx = getHttpContextWriteable false
    use ms = new MemoryStream(Encoding.UTF8.GetBytes "{\"NAME\":\"falco\"}")
    ctx.Request.ContentType <- "application/json"
    ctx.Request.Body <- ms

    task {
        let! json = Request.getJson ctx
        json.Name |> should equal "falco"
    }

[<Fact>]
let ``Request.getJson should allow trailing commas`` () =
    let ctx = getHttpContextWriteable false
    use ms = new MemoryStream(Encoding.UTF8.GetBytes "{\"name\":\"falco\",}")
    ctx.Request.ContentType <- "application/json"
    ctx.Request.Body <- ms

    task {
        let! json = Request.getJson ctx
        json.Name |> should equal "falco"
    }

[<Fact>]
let ``Request.mapJson`` () =
    let ctx = getHttpContextWriteable false
    use ms = new MemoryStream(Encoding.UTF8.GetBytes "{\"name\":\"falco\"}")
    ctx.Request.ContentType <- "application/json"
    ctx.Request.ContentLength.Returns(13L) |> ignore
    ctx.Request.Body.Returns(ms) |> ignore

    let handle json : HttpHandler =
        json.Name |> should equal "falco"
        Response.ofEmpty

    task {
        do! Request.mapJson handle ctx
    }

[<Fact>]
let ``Request.mapJson should handle empty body`` () =
    let ctx = getHttpContextWriteable false
    use ms = new MemoryStream()
    ctx.Request.ContentType <- "application/json"
    ctx.Request.Body <- ms

    let handle json : HttpHandler =
        json.Name |> should equal null
        Response.ofEmpty

    task {
        do! Request.mapJson handle ctx
    }

[<Fact>]
let ``Request.mapJsonOption`` () =
    let ctx = getHttpContextWriteable false
    use ms = new MemoryStream(Encoding.UTF8.GetBytes "{\"name\":\"falco\",\"age\":null}")
    ctx.Request.ContentType <- "application/json"
    ctx.Request.ContentLength.Returns 22L |> ignore
    ctx.Request.Body.Returns(ms) |> ignore

    let handle json : HttpHandler =
        json.Name |> should equal "falco"
        Response.ofEmpty

    let options = JsonSerializerOptions()
    options.AllowTrailingCommas <- true
    options.PropertyNameCaseInsensitive <- true
    options.DefaultIgnoreCondition <- JsonIgnoreCondition.WhenWritingNull

    task {
        do! Request.mapJsonOptions options handle ctx
    }

[<Fact>]
let ``Request.mapJsonOptions with null value should deserialize`` () =
    let ctx = getHttpContextWriteable false
    use ms = new MemoryStream(Encoding.UTF8.GetBytes "{\"name\":null}")
    ctx.Request.Body <- ms

    let handle json : HttpHandler =
        json.Name |> should equal null
        Response.ofEmpty

    let options = JsonSerializerOptions(PropertyNameCaseInsensitive = true)

    task {
        do! Request.mapJsonOptions options handle ctx
    }

[<Fact>]
let ``Request.mapJson Transfer-Encoding: chunked`` () =
    let ctx = getHttpContextWriteable false
    use ms = new MemoryStream(Encoding.UTF8.GetBytes "{\"name\":\"falco\"}")
    ctx.Request.ContentType <- "application/json"
    // Simulate chunked transfer encoding
    ctx.Request.Headers.Add(HeaderNames.TransferEncoding, "chunked")

    ctx.Request.Body.Returns(ms) |> ignore

    let handle json : HttpHandler =
        json.Name |> should equal "falco"
        Response.ofEmpty

    task {
        do! Request.mapJson handle ctx
    }

[<Fact>]
let ``Request.mapCookies`` () =
    let ctx = getHttpContextWriteable false
    ctx.Request.Cookies <- Map.ofList ["name", "falco"] |> cookieCollection

    let handle name : HttpHandler =
        name |> should equal "falco"
        Response.ofEmpty

    task {
        do! Request.mapCookies (fun r -> r.GetString "name") handle ctx
    }

[<Fact>]
let ``Request.mapHeaders`` () =
    let serverName = "Kestrel"
    let ctx = getHttpContextWriteable false
    ctx.Request.Headers.Add(HeaderNames.Server, StringValues(serverName))

    let handle server : HttpHandler =
        server |> should equal serverName
        Response.ofEmpty

    task {
        do! Request.mapHeaders (fun r -> r.GetString HeaderNames.Server) handle ctx
    }

[<Fact>]
let ``Request.mapRoute`` () =
    let ctx = getHttpContextWriteable false
    ctx.Request.RouteValues <- RouteValueDictionary {|name="falco"|}

    let handle name : HttpHandler =
        name |> should equal "falco"
        Response.ofEmpty

    task {
        do! Request.mapRoute (fun r -> r.GetString "name") handle ctx
    }

[<Fact>]
let ``Request.mapQuery`` () =
    let ctx = getHttpContextWriteable false
    let query = Dictionary<string, StringValues>()
    query.Add("name", StringValues "falco")
    ctx.Request.Query <- QueryCollection query

    let handle name : HttpHandler =
        name |> should equal "falco"
        Response.ofEmpty

    task {
        do! Request.mapQuery (fun c -> c.GetString "name") handle ctx
    }

[<Fact>]
let ``Request.mapForm`` () =
    let ctx = getHttpContextWriteable false
    let form = Dictionary<string, StringValues>()
    form.Add("name", StringValues "falco")
    let f = FormCollection(form)

    ctx.Request.ReadFormAsync().Returns(f) |> ignore
    ctx.Request.ReadFormAsync(Arg.Any<CancellationToken>()).Returns(f) |> ignore

    let handle name : HttpHandler =
        name |> should equal "falco"
        Response.ofEmpty

    task {
        do! Request.mapForm (fun f -> f?name.AsString()) handle ctx
    }

[<Fact>]
let ``Request.authenticate should call AuthenticateAsync`` () =
    let ctx = getHttpContextWriteable true

    let handle (result: AuthenticateResult) : HttpHandler =
        result.Succeeded |> should equal true
        Response.ofEmpty

    task {
        do! Request.authenticate AuthScheme handle ctx
    }

[<Fact>]
let ``Request.ifAuthenticated should allow authenticated users`` () =
    let ctx = getHttpContextWriteable true

    let mutable visited = false

    let handle : HttpHandler = fun ctx ->
        visited <- true
        Response.ofEmpty ctx

    task {
        do! Request.ifAuthenticated AuthScheme handle ctx
        visited |> should equal true
    }


[<Fact>]
let ``Request.ifNotAuthenticated should block authenticated users`` () =
    let ctx = getHttpContextWriteable false

    let mutable visited = false

    let handle : HttpHandler = fun ctx ->
        visited <- true
        Response.ofEmpty ctx

    task {
        do! Request.ifNotAuthenticated AuthScheme handle ctx
        visited |> should equal true
    }

[<Fact>]
let ``Request.ifAuthenticatedInRole should allow users in correct role`` () =
    let ctx = getHttpContextWriteable true

    let mutable visited = false

    let handle : HttpHandler = fun ctx ->
        visited <- true
        Response.ofEmpty ctx

    task {
        do! Request.ifAuthenticatedInRole AuthScheme (List.take 1 Common.AuthRoles) handle ctx
        visited |> should equal true
    }

[<Fact>]
let ``Request.ifAuthenticatedInRole should block users not in role`` () =
    let ctx = getHttpContextWriteable true

    let mutable visited = false

    let handle : HttpHandler = fun ctx ->
        visited <- true
        Response.ofEmpty ctx

    task {
        do! Request.ifAuthenticatedInRole AuthScheme ["admin2"] handle ctx
        visited |> should equal false
    }
