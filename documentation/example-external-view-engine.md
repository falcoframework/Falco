# Example - External View Engine

Falco comes packaged with a [built-in view engine](markup.md). But if you'd prefer to write your own templates, or use an external template engine, that is entirely possible as well.

In this example we'll do some basic page rendering by integrating with [scriban](https://github.com/scriban/scriban). An amazing template engine by [xoofx](https://github.com/xoofx).

The code for this example can be found [here](https://github.com/FalcoFramework/Falco/tree/master/examples/ExternalViewEngine).

## Creating the Application Manually

```shell
> dotnet new falco -o ExternalViewEngineApp
> cd ExternalViewEngineApp
> dotnet add package Scriban
```

## Implementing a Template Engine

There are a number of ways we could achieve this functionality. But in sticking with our previous examples, we'll create an interface. To keep things simple we'll use inline string literals for templates and perform rendering synchronously.

```fsharp
open Scriban

type ITemplate =
    abstract member Render : template: string * model: obj -> string

type ScribanTemplate() =
    interface ITemplate with
        member _.Render(template, model) =
            let tmpl = Template.Parse template
            tmpl.Render(model)
```

We define an interface `ITemplate` which describes template rendering as a function that receives a template string literal and a model, producing a string literal. Then we implement this interface definition using Scriban.

## Rendering Pages

To use our Scriban template engine we'll need to request it from the dependency container, then pass it our template literal and model.

> See [dependency injection](example-dependency-injection.md) for further explanation.

Since rendering more than one page is the goal, we'll create a shared `renderPage` function to do the dirty work for us.

```fsharp
open Falco

module Pages =
    let private renderPage pageTitle template viewModel : HttpHandler = fun ctx ->
        let templateService = ctx.Plug<ITemplate>() // <-- obtain our template service from the dependency container
        let pageContent = templateService.Render(template, viewModel) // <-- render our template with the provided view model as string literal
        let htmlTemplate = """
            <!DOCTYPE html>
            <html>
            <head>
                <meta charset="utf-8">
                <meta name="viewport" content="width=device-width, initial-scale=1">
                <title>{{title}}</title>
            </head>
            <body>
                {{content}}
            </body>
            </html>
        """
        // ^ these triple quoted strings auto-escape characters like double quotes for us
        //   very practical for things like HTML

        let html = templateService.Render(htmlTemplate, {| Title = pageTitle; Content = pageContent |})

        Response.ofHtmlString html ctx // <-- return template literal as "text/html; charset=utf-8" response
```

In this function we obtain the instance of our template engine, and immediately render the user-provided template and model. Next, we define a local template literal to serve as our layout. Assigning two simple inputs, `{{title}}` and `{{content}}`. Then we render the layout template using our template engine and an anonymous object literal `{| Title = pageTitle; Content = pageContent |}`, responding with the result of this as `text/html`.

To render pages, we simply need to create a localized template literal, and feed it into our `renderPage` function. Below we define a home and 404 page.

```fsharp
    let homepage : HttpHandler = fun ctx ->
        let query = Request.getQuery ctx // <-- obtain access to strongly-typed representation of the query string
        let viewModel = {| Name = query?name.AsStringNonEmpty("World") |} // <-- access 'name' from query, or default to 'World'
        let template = """
            <h1>Hello {{ name }}!</h1>
        """
        renderPage $"Hello {viewModel.Name}" template viewModel ctx

    let notFound : HttpHandler =
        let template = """
            <h1>Page not found</h1>
        """
        renderPage "Page Not Found" template {||}
```

## Registering the Template Engine

Since our Scriban template engine is stateless and dependency-free, we can use the generic extension method to register it as a singleton.

> Note: `Transient` and `Scoped` lifetimes would also work here.

```
open Falco
open Falco.Routing
open Microsoft.AspNetCore.Builder
open Microsoft.Extensions.DependencyInjection

[<EntryPoint>]
let main args =
    let bldr = WebApplication.CreateBuilder(args)

    bldr.Services
        .AddSingleton<ITemplate, ScribanTemplate>() // <-- register ITemplates implementation as a dependency
        |> ignore

    let endpoints =
        [ get "/" Pages.homepage ]

    let wapp = bldr.Build()

    wapp.UseRouting()
        .UseFalco(endpoints)
        .UseFalcoNotFound(Pages.notFound)
        .Run()

    0 // Exit code
```

## Wrapping Up

This example demonstrates how to effectively integrate an external view engine into your Falco application. By defining a simple interface, implementing it with Scriban and adding it to the dependency container, we can render HTML pages dynamically based on user input.

[Next: Example - Basic REST API](example-basic-rest-api.md)
