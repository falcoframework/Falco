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

        let response = client.PostAsync("/hello/John+Smith", form).Result
        let content = response.Content.ReadAsStringAsync().Result
        Assert.Equal("""{"Message":"Hello John Smith!"}""", content)

        use form = new FormUrlEncodedContent(dict [ ("age", "42") ])
        let response = client.PostAsync("/hello/John", form).Result
        let content = response.Content.ReadAsStringAsync().Result
        Assert.Equal("""{"Message":"Hello John, you are 42 years old!"}""", content)

    [<Fact>]
    let ``Receive mapped form response from: POST /hello-form`` () =
        use client = factory.CreateClient ()
        use form = new FormUrlEncodedContent(dict [("name", "Mr ++ ")])
        let response = client.PostAsync("/hello-form", form).Result
        let content = response.Content.ReadAsStringAsync().Result
        Assert.Equal("Hello Mr ++ !", content)

    [<Fact>]
    let ``Receive mapped query response from: GET /hello-query`` () =
        use client = factory.CreateClient ()
        let response = client.GetAsync("/hello-query?name=Mr%20%2B%2B").Result
        let content = response.Content.ReadAsStringAsync().Result
        Assert.Equal("Hello Mr ++!", content)

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

module Program = let [<EntryPoint>] main _ = 0
