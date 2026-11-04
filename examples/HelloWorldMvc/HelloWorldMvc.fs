namespace HelloWorldMvc

open Falco
open Falco.Markup
open Falco.Routing
open Microsoft.AspNetCore.Builder
open Microsoft.Extensions.DependencyInjection

type Greeting =
    { Name : string }

module View =
    let layout content =
        Templates.html5 "en"
            [ _link [ _href_ "/style.css"; _rel_ "stylesheet" ] ]
            content

    let menu greeting actions =
        _nav [] [
            _text " | "
            for url, label in actions do
                _a
                    [ _href_ (url greeting.Name) ]
                    [ _text label ]
                _text " | "
        ]

module GreetingView =
    let detail actions greeting =
        View.layout [
            _h1' $"Hello {greeting.Name} using HTML"
            _form [] [
                _input [ _name_ "name"; _placeholder_ "Enter your name" ]
                _input [_typeSubmit_ ]
            ]
            _hr []
            _p' "Greet other ways:"
            View.menu greeting actions
        ]

module ErrorResponse =
    let notFound =
        Response.withStatusCode 404 >>
        Response.ofHtml (View.layout [ _h1' "Not Found" ])

    let serverException =
        Response.withStatusCode 500 >>
        Response.ofHtml (View.layout [ _h1' "Server Error" ])

module GreetingController =
    let index name =
        { Name = name }
        |> GreetingView.detail [
            sprintf "/greet/text?name=%s", "Greet in plain text"
            sprintf "/greet/json?name=%s", "Greet in JSON" ]
        |> Response.ofHtml

    let plainTextDetail name =
        Response.ofPlainText $"Hello {name} using plain text"

    let jsonDetail name =
        Response.ofJson {| Message = $"Hello {name} using JSON" |}

    let private mapName (r : RequestData) =
        r?name.AsStringNonEmpty "World"

    let endpoints =
        [ mapGet "/" mapName index
          mapGet "/greet/text" mapName plainTextDetail
          mapGet "/greet/json" mapName jsonDetail ]

module Program =
    [<EntryPoint>]
    let main args =
        let wapp = WebApplication.Create(args)

        let isDevelopment = wapp.Environment.EnvironmentName = "Development"

        wapp.UseIf(isDevelopment, DeveloperExceptionPageExtensions.UseDeveloperExceptionPage)
            .UseIf(not isDevelopment, FalcoExtensions.UseFalcoExceptionHandler(ErrorResponse.serverException))
            .Use(StaticFileExtensions.UseStaticFiles)
            .UseRouting()
            .UseFalco(GreetingController.endpoints)
            .Run(ErrorResponse.notFound)

        0
