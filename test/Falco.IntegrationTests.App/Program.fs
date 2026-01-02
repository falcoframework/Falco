module Falco.IntegrationTests.App

open Falco
open Falco.Markup
open Falco.Routing
open Microsoft.AspNetCore.Builder
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
            (fun person -> Request.mapForm (mapRequestData person) Response.ofJson)

        get "/hello-query"
            (
               Request.mapQuery
                  (fun r ->
                   let name = r?name.AsStringNonEmpty("world")
                   $"Hello {name}!") Response.ofPlainText)

        post "/hello-form"
            (
               Request.mapForm
                  (fun r ->
                   let name = r?name.AsStringNonEmpty("world")
                   $"Hello {name}!") Response.ofPlainText)

        mapGet "/plug/{name?}"
            (fun r -> r?name.AsStringNonEmpty("world"))
            (fun name ctx ->
                let greeter = ctx.Plug<IGreeter>() // <-- access our dependency from the container
                let greeting = greeter.Greet(name) // <-- invoke our greeter.Greet(name) method
                Response.ofPlainText greeting ctx)

        post "/api/message"
            (Request.mapJson Response.ofJson)
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
