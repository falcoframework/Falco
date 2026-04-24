namespace Falco.IntegrationTests

open System.Net.Http
open System.Text
open System.Text.Json
open Microsoft.AspNetCore.Mvc.Testing
open Xunit
open Falco.IntegrationTests.App

module FalcoOpenApiTestServer =
    let createFactory() =
        new WebApplicationFactory<Program>()

module Tests =
    let private factory = FalcoOpenApiTestServer.createFactory ()

    [<Fact>]
    let ``Receive plain-text response from: GET /hello``() =
        use client = factory.CreateClient ()
        let content = client.GetStringAsync("/").Result
        Assert.Equal("Hello World!", content)

    [<Fact>]
    let ``Receive text/html response from GET /html`` () =
        use client = factory.CreateClient ()
        let content = client.GetStringAsync("/html").Result
        Assert.Equal("""<!DOCTYPE html><html><head></head><body><h1>hello world</h1></body></html>""", content)


    [<Fact>]
    let ``Receive application/json response from GET /json`` () =
        use client = factory.CreateClient ()
        let content = client.GetStringAsync("/json").Result
        Assert.Equal("""{"Message":"hello world"}""", content)

    [<Fact>]
    let ``Receive mapped application/json response from: GET /hello/name?`` () =
        use client = factory.CreateClient ()
        let content = client.GetStringAsync("/hello").Result
        Assert.Equal("""{"Message":"Hello world!"}""", content)

        let content = client.GetStringAsync("/hello/John").Result
        Assert.Equal("""{"Message":"Hello John!"}""", content)

        let content = client.GetStringAsync("/hello/John?age=42").Result
        Assert.Equal("""{"Message":"Hello John, you are 42 years old!"}""", content)

    [<Fact>]
    let ``Receive mapped application/json response from: POST /hello/name?`` () =
        use client = factory.CreateClient ()
        use form = new FormUrlEncodedContent([])
        let response = client.PostAsync("/hello", form).Result
        let content = response.Content.ReadAsStringAsync().Result
        Assert.Equal("""{"Message":"Hello world!"}""", content)

        let response = client.PostAsync("/hello/John", form).Result
        let content = response.Content.ReadAsStringAsync().Result
        Assert.Equal("""{"Message":"Hello John!"}""", content)

        use form = new FormUrlEncodedContent(dict [ ("age", "42") ])
        let response = client.PostAsync("/hello/John", form).Result
        let content = response.Content.ReadAsStringAsync().Result
        Assert.Equal("""{"Message":"Hello John, you are 42 years old!"}""", content)

    [<Fact>]
    let ``Receive utf8 text/plain response from: GET /plug/name?`` () =
        use client = factory.CreateClient ()
        let content = client.GetStringAsync("/plug").Result
        Assert.Equal("Hello world 😀", content)

        let content = client.GetStringAsync("/plug/John").Result
        Assert.Equal("Hello John 😀", content)

    [<Fact>]
    let ``Receive application/json request body and return from: GET /api/message`` () =
        use client = factory.CreateClient ()

        use body = new StringContent(JsonSerializer.Serialize { Message = "Hello /api/message" }, Encoding.UTF8, "application/json")
        let response = client.PostAsync("/api/message", body).Result
        let content = response.Content.ReadAsStringAsync().Result
        Assert.Equal("""{"Message":"Hello /api/message"}""", content)

        use body = new StringContent("", Encoding.UTF8, "application/json")
        let response = client.PostAsync("/api/message", body).Result
        let content = response.Content.ReadAsStringAsync().Result
        Assert.Equal("""{}""", content)

    [<Fact>]
    let ``GET /hello/ (trailing slash) returns default greeting`` () =
        use client = factory.CreateClient()
        let content = client.GetStringAsync("/hello/").Result
        Assert.Equal("""{"Message":"Hello world!"}""", content)

    [<Fact>]
    let ``GET /hello/{name} decodes url-encoded name`` () =
        use client = factory.CreateClient()
        let content = client.GetStringAsync("/hello/John%20Doe").Result
        Assert.Equal("""{"Message":"Hello John Doe!"}""", content)

    [<Fact>]
    let ``GET /hello/{name}?age=invalid ignores invalid age`` () =
        use client = factory.CreateClient()
        let content = client.GetStringAsync("/hello/John?age=not-a-number").Result
        Assert.Equal("""{"Message":"Hello John!"}""", content)

    [<Fact>]
    let ``POST /hello/{name} with invalid age ignores age`` () =
        use client = factory.CreateClient()
        use form = new FormUrlEncodedContent(dict [ ("age", "not-a-number") ])
        let response = client.PostAsync("/hello/Jane", form).Result
        let content = response.Content.ReadAsStringAsync().Result
        Assert.Equal("""{"Message":"Hello Jane!"}""", content)

    [<Fact>]
    let ``POST /hello with age but no name uses default world`` () =
        use client = factory.CreateClient()
        use form = new FormUrlEncodedContent(dict [ ("age", "7") ])
        let response = client.PostAsync("/hello", form).Result
        let content = response.Content.ReadAsStringAsync().Result
        Assert.Equal("""{"Message":"Hello world, you are 7 years old!"}""", content)

    [<Fact>]
    let ``POST /api/message with non-json content type should return empty JSON literal`` () =
        use client = factory.CreateClient()
        use body = new StringContent("Message=Hello", Encoding.UTF8, "text/plain")
        let response = client.PostAsync("/api/message", body).Result
        let content = response.Content.ReadAsStringAsync().Result
        Assert.Equal("""{}""", content)

    [<Fact>]
    let ``POST /api/message echoes input`` () =
        use client = factory.CreateClient()
        let payload = """{"Message":"Hello /api/message","Extra":"ignored"}"""
        use body = new StringContent(payload, Encoding.UTF8, "application/json")
        let response = client.PostAsync("/api/message", body).Result
        let content = response.Content.ReadAsStringAsync().Result
        Assert.Equal("""{"Message":"Hello /api/message","Extra":"ignored"}""", content)

    [<Fact>]
    let ``GET /json returns application/json content type`` () =
        use client = factory.CreateClient()
        let response = client.GetAsync("/json").Result
        let ct = response.Content.Headers.ContentType.ToString()
        Assert.Contains("application/json", ct)

    [<Fact>]
    let ``GET /hello returns application/json content type`` () =
        use client = factory.CreateClient()
        let response = client.GetAsync("/hello").Result
        let ct = response.Content.Headers.ContentType.ToString()
        Assert.Contains("application/json", ct)

    [<Fact>]
    let ``GET / returns text/plain content type`` () =
        use client = factory.CreateClient()
        let response = client.GetAsync("/").Result
        let ct = response.Content.Headers.ContentType.ToString()
        Assert.Contains("text/plain", ct)

module AntiforgeryMultipartTests =
    open System.Net
    open Microsoft.AspNetCore.Hosting
    open Microsoft.AspNetCore.TestHost
    open Microsoft.Extensions.DependencyInjection

    // Build a factory that registers antiforgery services on top of the
    // integration test app. The antiforgery service is not registered by the
    // base app so as not to affect other tests.
    let private factory =
        let baseFactory = FalcoOpenApiTestServer.createFactory ()
        baseFactory.WithWebHostBuilder(fun b ->
            b.ConfigureTestServices(fun services ->
                services.AddAntiforgery() |> ignore)
            |> ignore)

    type private CsrfToken = { FormFieldName : string; RequestToken : string }

    // The TestServer HttpClient has a default CookieContainer that replays
    // Set-Cookie across requests — no manual cookie forwarding needed.
    let private getCsrfToken (client : HttpClient) : CsrfToken =
        let response = client.GetAsync("/csrf-token").Result
        response.EnsureSuccessStatusCode() |> ignore
        let body = response.Content.ReadAsStringAsync().Result
        use doc = JsonDocument.Parse(body)
        let root = doc.RootElement
        { FormFieldName = root.GetProperty("FormFieldName").GetString()
          RequestToken = root.GetProperty("RequestToken").GetString() }

    [<Fact>]
    let ``POST multipart/form-data with valid CSRF token succeeds via getForm`` () =
        use client = factory.CreateClient ()
        let token = getCsrfToken client

        use content = new MultipartFormDataContent()
        content.Add(new StringContent("Alice"), "name")
        content.Add(new StringContent(token.RequestToken), token.FormFieldName)

        let response = client.PostAsync("/form-with-csrf", content).Result
        let body = response.Content.ReadAsStringAsync().Result

        // Before the fix: antiforgery validation pre-reads the multipart body via
        // ReadFormAsync, then consumeForm calls StreamFormAsync which fails with
        // "Unexpected end of Stream", surfacing as a 500.
        Assert.Equal(HttpStatusCode.OK, response.StatusCode)
        Assert.Equal("""{"Message":"Hello Alice!"}""", body)

    [<Fact>]
    let ``POST urlencoded form with valid CSRF token succeeds via getForm`` () =
        // Regression guard for the non-multipart path, which was already working.
        use client = factory.CreateClient ()
        let token = getCsrfToken client

        let form =
            new FormUrlEncodedContent(
                dict [
                    ("name", "Bob")
                    (token.FormFieldName, token.RequestToken)
                ])

        let response = client.PostAsync("/form-with-csrf", form).Result
        let body = response.Content.ReadAsStringAsync().Result

        Assert.Equal(HttpStatusCode.OK, response.StatusCode)
        Assert.Equal("""{"Message":"Hello Bob!"}""", body)

module Program = let [<EntryPoint>] main _ = 0
