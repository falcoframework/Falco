# Example - External View Engine

Falco comes packaged with a [built-in view engine](markup.md). But if you'd prefer to write your own templates, or use an external view engine, that is entirely possible as well.

In this example we'll do some basic page rendering by integrating with [scriban](https://github.com/scriban/scriban). An amazing template engine by [xoofx](https://github.com/xoofx).

The code for this example can be found [here](https://github.com/FalcoFramework/Falco/tree/master/examples/ExternalViewEngine).

## Creating the Application Manually

```shell
> dotnet new falco -o ExternalViewEngineApp
> cd ExternalViewEngineApp
> dotnet add package Scriban
```

## Implementing a View Engine

There are a number of ways we could abstract this functionality. But in sticking with our previous examples, we'll create an interface. To keep things simple we'll use inline string literals for templates and perform rendering synchronously.

```fsharp
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
            let content = viewTemplate.Render(model)
            htmlTemplate.Render({| Title = title; Content = content |})
```

We define an interface `IViewEngine` which describes template rendering as a function that receives a title, a template string literal and a model. The template and model are used to produce the page content at runtime. We define a field to contain our template to serve as our layout. Assigning two simple inputs, `{{title}}` and `{{content}}`. Then we render the layout template using an anonymous object literal containing the page content we rendered earlier, `{| Title = pageTitle; Content = pageContent |}`

## Rendering Pages

To use our [scriban](https://github.com/scriban/scriban) view engine we'll need to request it from the dependency container, then pass it our template literal and model. We'll do this in our PageModule which defines our `homepage` endpoint and associates it to the URL "/".

> See [dependency injection](example-dependency-injection.md) for further explanation.

```fsharp
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
```

In this function we obtain the instance of our view engine, and immediately render the user-provided template and model to string, responding with the result of this as `text/html`.

## Registering the View Engine

Since our [scriban](https://github.com/scriban/scriban) view engine is stateless and dependency-free, we can use the generic extension method to register it as a singleton.

> Note: `Transient` and `Scoped` lifetimes would also work here.

```
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
```

## Wrapping Up

This example demonstrates how to effectively integrate an external view engine into your Falco application. By defining a simple interface, implementing it with [scriban](https://github.com/scriban/scriban) and adding it to the dependency container, we can render HTML pages dynamically based on user input.

[Next: Example - Basic REST API](example-basic-rest-api.md)
