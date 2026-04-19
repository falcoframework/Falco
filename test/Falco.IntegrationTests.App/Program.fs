module Falco.IntegrationTests.App

open Falco
open Falco.Markup
open Falco.Routing
open Falco.Security
open Microsoft.AspNetCore.Antiforgery
open Microsoft.AspNetCore.Builder
open Microsoft.AspNetCore.Http
open Microsoft.Extensions.DependencyInjection
open Microsoft.Extensions.Hosting

type IGreeter =
    abstract member Greet : name : string -> string

type FriendlyGreeter() =
    interface IGreeter with
        member _.Greet(name : string) =
            $"Hello {name} 😀"

type Person =
    { Name : string
      Age : int option }

type Greeting =
    { Message : string }

let endpoints =
    let mapRouteData (data : RequestData) =
        { Name = data?name.AsStringNonEmpty("world")
          Age = None }

    let mapRequestData (person : Person) (data : RequestData) =
        let person = { person with Age = data?age.AsIntOption() }
        let message =
            match person.Age with
            | Some a -> $"Hello {person.Name}, you are {a} years old!"
            | _ -> $"Hello {person.Name}!"
        { Message = message }

    [
        get "/"
            (Response.ofPlainText "Hello World!")

        get "/html"
            (Response.ofHtml
                (Elem.html [] [
                    Elem.head [] []
                    Elem.body [] [ Text.h1 "hello world" ] ]))

        get "/json"
            (Response.ofJson { Message = "hello world" })

        mapGet "/hello/{name?}" mapRouteData
            (fun person -> Request.mapQuery (mapRequestData person) Response.ofJson)

        mapPost "/hello/{name?}" mapRouteData
            (fun person -> Request.mapForm (mapRequestData person) Response.ofJson Response.ofEmpty)

        mapGet "/plug/{name?}"
            (fun r -> r?name.AsStringNonEmpty("world"))
            (fun name ctx ->
                let greeter = ctx.Plug<IGreeter>() // <-- access our dependency from the container
                let greeting = greeter.Greet(name) // <-- invoke our greeter.Greet(name) method
                Response.ofPlainText greeting ctx)

        post "/api/message"
            (Request.mapJson Response.ofJson)

        // Endpoint that emits an antiforgery token (for tests)
        get "/csrf-token" (fun ctx ->
            let token = Xsrf.getToken ctx
            Response.ofJson
                {| FormFieldName = token.FormFieldName
                   RequestToken = token.RequestToken |}
                ctx)

        // Endpoint that consumes a form while antiforgery is enabled. This
        // exercises Request.getForm, which validates the CSRF token and then
        // reads the form. For multipart requests with a valid token, the
        // antiforgery validation pre-reads the body; previously, consumeForm
        // would then call StreamFormAsync again and fail with
        // "Unexpected end of Stream".
        post "/form-with-csrf" (fun ctx -> task {
            let! form = Request.getForm ctx
            match form with
            | Some f ->
                let name = f.Get("name").AsStringNonEmpty("")
                return! Response.ofJson {| Message = $"Hello {name}!" |} ctx
            | None ->
                ctx.Response.StatusCode <- 400
                return! Response.ofPlainText "invalid" ctx })
    ]

let bldr = WebApplication.CreateBuilder()

bldr.Services
    .AddSingleton<IGreeter, FriendlyGreeter>()
|> ignore

let wapp = bldr.Build()

wapp.UseHttpsRedirection()
|> ignore

wapp.UseRouting()
    .UseFalco(endpoints)
|> ignore

wapp.Run()

type Program() = class end
