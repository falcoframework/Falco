# Example - Hello World MVC

Let's take our basic [Hello World](example-hello-world.md) to the next level. This means we're going to dial up the complexity a little bit. But we'll do this using the well-recognized MVC pattern. We'll contain the app to a single file to make "landscaping" the pattern more straightforward.

The code for this example can be found [here](https://github.com/FalcoFramework/Falco/tree/master/examples/HelloWorldMvc).

## Creating the Application Manually

```shell
> dotnet new falco -o HelloWorldMvcApp
```

## Model

The purpose of our application is to greet people in multiple formats. Common to all formats is the input name used to produce the greeting. So let's create a type to represent the _input_ that constitutes a greeting.

```fsharp
type Greeting =
    { Name : string }
```

It's a simple model in this case because our application is purposefully simple. We're not in model school.

## View

Falco comes packaged with a [lovely little HTML DSL](https://github.com/FalcoFramework/Falco.Markup/). It can produce any form of angle-markup, and does so very [efficiently](https://github.com/FalcoFramework/Falco.Markup/?tab=readme-ov-file#performance). The main benefit is that our views are _pure_ F#, compile-time checked and live alongside the rest of our code.

First we define a shared HTML5 `layout` function that references our project `style.css`. Then a small menu component which takes our greeting and a list of "actions", the combination of URL function and label.

Next, we define a module to contain the view for our greeting which uses our layout and component.

> You'll notice the `style.css` file resides in a folder called `wwwroot`. This is an [ASP.NET convention](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/static-files) which we'll enable later when we [build the web server](#web-server).

```fsharp
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
            View.menu greeting.Name actions
        ]
```

The markup code is fairly self-explanatory. But in a nutshell:

- `_{a-Z0-9}` produces HTML elements.
- `_{a-Z0-9}_` produces HTML element attributes.
- `_text`, `_textf`, `_textEnc` produces HTML text nodes.
- `_{a-Z0-9}'` is a shorthand for creating HTML elements with text content.

You can also use the long form of the `Elem`, `Attr` and `Text` modules if you prefer (ex: `Elem.div [ Attr.class' "my-class" ] [ Text.raw "Hello" ]`).

## Errors

We'll define a couple static error responses to help prettify our error output.

```fsharp
module ErrorResponse =
    let notFound =
        Response.withStatusCode 404 >>
        Response.ofHtml (View.layout [ _h1' "Not Found" ])

    let serverException =
        Response.withStatusCode 500 >>
        Response.ofHtml (View.layout [ _h1' "Server Error" ])
```

Here we see the [`HttpResponseModifier`](response.md#response-modifiers) at play, which sets the status code before buffering out the HTML response. We'll reference these responses later when we [build the web server](#web-server).

## Controller

Our controller will be responsible for three actions, as defined in our [route](#routing) module. We define three handlers which output the user-provided "name" in different ways: plain text, JSON and HTML.

```fsharp
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
```

You'll notice that this controller defines its own `endpoints` too. This associates a route to a handler when passed into Falco (we'll do this later). Defining this within the controller is personal preference. But considering controller actions usually operate against a common URL pattern, it allows a private, reusable route mapping to exist (see `mapName`).

## Web Server

This is a great opportunity to demonstrate further how to configure a more complex web server than we saw in the basic hello world example.

To do that, we'll define an explicit entry point function which gives us access to the command line argument. By then forwarding these into the web application, we gain further [configurability](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/configuration#command-line). You'll notice the application contains a file called `appsettings.json`, this is another [ASP.NET convention](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/configuration#default-application-configuration-sources) that provides fully-featured and extensible configuration functionality and is automatically available when using `WebApplication.Create()`.

In this example, we examine the environment name to create an "is development" toggle. We use this to determine the extensiveness of our error output. You'll notice we use our exception page from above when an exception occurs when not in development mode. Otherwise, we show a developer-friendly error page. Next we activate static file support, via the default web root of `wwwroot`.

We end off by registering a terminal handler, which functions as our "not found" (404) response.

```fsharp
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
```

## Wrapping Up

This example was a leap ahead from our basic hello world. But having followed this, you now understand many of the patterns you'll need to know to build end-to-end server applications with Falco. Unsurprisingly, the entire program fits inside 86 LOC. One of the magnificent benefits of writing code in F#.

[Next: Example - Dependency Injection](example-dependency-injection.md)
