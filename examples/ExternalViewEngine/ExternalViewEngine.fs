module Falco.Scriban.Program

open Falco
open Falco.Routing
open Microsoft.AspNetCore.Builder
open Microsoft.Extensions.DependencyInjection
open Scriban

type ITemplate =
    abstract member Render : template: string * model: obj -> string

type ScribanTemplate() =
    interface ITemplate with
        member _.Render(template, model) =
            let tmpl = Template.Parse template
            tmpl.Render(model)

module Pages =
    let private renderPage pageTitle template viewModel : HttpHandler = fun ctx ->
        let templateService = ctx.Plug<ITemplate>()
        // ^-- obtain our template service from the dependency container

        let pageContent = templateService.Render(template, viewModel)
        // ^-- render our template with the provided view model as string literal

        let htmlTemplate = """
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
        // ^-- these triple quoted strings auto-escape characters like double quotes for us
        //     very practical for things like HTML

        let html = templateService.Render(htmlTemplate, {| Title = pageTitle; Content = pageContent |})

        Response.ofHtmlString html ctx
        // ^-- return template literal as "text/html; charset=utf-8" response

    let homepage : HttpHandler = fun ctx ->
        let query = Request.getQuery ctx
        // ^-- obtain access to strongly-typed representation of the query string

        let viewModel = {| Name = query?name.AsStringNonEmpty("World") |}
        // ^-- access 'name' from query, or default to 'World'

        let template = """
            <h1>Hello {{ name }}!</h1>
        """
        renderPage $"Hello {viewModel.Name}" template viewModel ctx

    let notFound : HttpHandler =
        let template = """
            <h1>Page not found</h1>
        """
        renderPage "Page Not Found" template {||}

[<EntryPoint>]
let main args =
    let bldr = WebApplication.CreateBuilder(args)

    bldr.Services
        .AddSingleton<ITemplate, ScribanTemplate>()
        // ^-- register ITemplates implementation as a dependency
        |> ignore

    let wapp = bldr.Build()

    let endpoints =
        [ get "/" Pages.homepage ]

    wapp.UseRouting()
        .UseFalco(endpoints)
        .Run(Pages.notFound)

    0 // Exit code
