namespace HelloWorldMvc

open Falco
open Falco.Markup
open Falco.Routing
open Microsoft.AspNetCore.Builder

module Model =
    type NameGreeting =
        { Name : string }

    type Greeting =
        { Message : string }

module Route =
    let index = "/"
    let greetPlainText = "/greet/text/{name?}"
    let greetJson = "/greet/json/{name?}"
    let greetHtml = "/greet/html/{name?}"

module Url =
    let greetPlainText name = Route.greetPlainText.Replace("{name?}", name)
    let greetJson name = Route.greetJson.Replace("{name?}", name)
    let greetHtml name = Route.greetHtml.Replace("{name?}", name)

module View =
    open Model

    let layout content =
        Templates.html5 "en"
            [ _link [ _href_ "/style.css"; _rel_ "stylesheet" ] ]
            content

    module GreetingView =
        let detail greeting =
            layout [
                _h1' $"Hello {greeting.Name} using HTML"
                _hr []
                _p' "Greet other ways:"
                _nav [] [
                    _a
                        [ _href_ (Url.greetHtml greeting.Name) ]
                        [ _text "Greet in HTML"]
                    _text " | "
                    _a
                        [ _href_ (Url.greetPlainText greeting.Name) ]
                        [ _text "Greet in plain text"]
                    _text " | "
                    _a
                        [ _href_ (Url.greetJson greeting.Name) ]
                        [ _text "Greet in JSON " ]
                ]
            ]

module Controller =
    open Model
    open View

    /// Error page(s)
    module ErrorController =
        let notFound : HttpHandler =
            Response.withStatusCode 404 >>
            Response.ofHtml (layout [ _h1' "Not Found" ])

        let serverException : HttpHandler =
            Response.withStatusCode 500 >>
            Response.ofHtml (layout [ _h1' "Server Error" ])

        let endpoints =
            [ get "/error/not-found" notFound
              get "/error/server-exception" serverException ]

    module GreetingController =
        let index name =
            { Name = name }
            |> GreetingView.detail
            |> Response.ofHtml

        let plainTextDetail name =
            Response.ofPlainText $"Hello {name} using plain text"

        let jsonDetail name =
            let message = { Message = $"Hello {name} using JSON" }
            Response.ofJson message

        let endpoints =
            let mapRoute (r : RequestData) =
                r?name.AsStringNonEmpty("you")

            [ mapGet Route.index mapRoute index
              mapGet Route.greetPlainText mapRoute plainTextDetail
              mapGet Route.greetJson mapRoute jsonDetail
              mapGet Route.greetHtml mapRoute index ]

module App =
    open Controller

    let endpoints =
        ErrorController.endpoints
        @ GreetingController.endpoints

module Program =
    open Controller

    [<EntryPoint>]
    let main args =
        let wapp = WebApplication.Create(args)

        let isDevelopment = wapp.Environment.EnvironmentName = "Development"

        wapp.UseIf(isDevelopment, DeveloperExceptionPageExtensions.UseDeveloperExceptionPage)
            .UseIf(not(isDevelopment), FalcoExtensions.UseFalcoExceptionHandler ErrorController.serverException)
            .Use(StaticFileExtensions.UseStaticFiles)
            .UseRouting()
            .UseFalco(App.endpoints)
            .Run(ErrorController.notFound)

        0
