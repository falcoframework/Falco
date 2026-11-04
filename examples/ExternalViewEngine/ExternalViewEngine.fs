module Falco.Scriban.Program

open Falco
open Falco.Routing
open Microsoft.AspNetCore.Builder
open Microsoft.Extensions.DependencyInjection
open Scriban

type Greeting =
    { Name : string }

type IViewEngine =
    abstract member Render : title: string * model: obj * template: string -> string

type PageViewEngine() =
    let htmlTemplate = Template.Parse """
        <!DOCTYPE html>
        <html>
        <head>
            <meta charset="utf-8">
            <meta name="viewport" content="width=device-width, initial-scale=1">
            <title>{{ title }}</title>
        </head>
        <body>
            {{ content }}
        </body>
        </html>
        """
        // ^-- parse the HTML template once during construction

    interface IViewEngine with
        member _.Render(title, model, template) =
            let viewTemplate = Template.Parse template
            let content = viewTemplate.Render model
            htmlTemplate.Render {| Title = title; Content = content |}

type PageModule(viewEngine : IViewEngine) =
    let homepage model =
        viewEngine.Render($"Hello {model.Name}", model, """
            <h1>Hello {{ name }}!</h1>
            <form>
                <input name="name" placeholder="enter your name">
                <input type="submit">
            </form>
        """)
        // ^-- these triple quoted strings auto-escape characters like double quotes
        //     for us very practical for things like HTML
        |> Response.ofHtmlString

    let mapName (r : RequestData) =
        { Name = r?name.AsStringNonEmpty "World" }

    interface IFalcoModule with
        member _.Endpoints = seq {
            mapGet "/" mapName homepage
        }

[<EntryPoint>]
let main args =
    let bldr = WebApplication.CreateBuilder(args)

    bldr.Services
        .AddSingleton<IViewEngine, PageViewEngine>()
        // ^-- register Scriban implementation as a dependency
        .AddSingleton<IFalcoModule, PageModule>()
        // ^-- register the Falco module to expose the endpoint
        |> ignore

    let wapp = bldr.Build()

    wapp.UseRouting()
        .UseFalco()
        .Run()

    0 // Exit code
