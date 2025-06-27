# Example - HTMX

[Falco.Htmx](https://github.com/FalcoFramework/Falco.Htmx) brings type-safe [htmx](https://htmx.org/) support to [Falco](https://github.com/FalcoFramework/Falco). It provides a complete mapping of all attributes, typed request data and ready-made response modifiers.

In this example, we'll demonstrate some of the more common htmx attributes and how to use them with Falco.

At this point, we'll assume you have reviewed the docs, other examples and understand the basics of Falco. We don't be covering any of the basics in the code review.

The code for this example can be found [here](https://github.com/FalcoFramework/Falco/tree/master/examples/Htmx).

## Creating the Application Manually

```shell
> dotnet new falco -o HtmxApp
> cd HtmxApp
> dotnet add package Falco.Htmx
```

## Layout

First we'll define a simple layout and enable htmx by including the script. Notice the strongly typed reference, `HtmxScript.cdnSrc`, which is provided by Falco.Htmx and resolves to the official CDN URL.

```fsharp
module View =
    let template content =
        _html [ _lang "en" ] [
            _head [] [
                _script [ _src HtmxScript.cdnSrc ] [] ]
            _body []
                content ]
```

## Components

A nice convention when working with Falco.Markup is to create a `Components` module within your `View` module. We'll define a few components here.

All of the htmx attributes and properties are mapped within the `Hx` module. Wherever a limited scope of options exist, strongly typed references are provided. For example, `Hx.swapOuterHtml` is a strongly typed reference to the `hx-swap` attribute with the value `outerHTML`. This is a great way to avoid typos and ensure that your code is type-safe.

```fsharp
module View =
    // Layout ...

    module Components =
        let clicker =
            _button
                [ Hx.get "/click"
                  Hx.swapOuterHtml ]
                [ _text "Click Me" ]

        let resetter =
            _div [ _id "wrapper" ] [
                _h2' "Way to go! You clicked it!"
                _br []
                _button
                    [ Hx.get "/reset"
                      Hx.swapOuterHtml
                      Hx.targetCss "#wrapper" ]
                    [ _text "Reset" ] ]
```

The `clicker` component is a simple button that will send a GET request to the server when clicked. The response will replace the button with the `resetter` component which will be rendered in the same location and can be used to restore the original state.

## Handlers

Next we define a couple basic handlers to handle the requests for the original document and ajax requests.

```fsharp
module App =
    let handleIndex : HttpHandler =
        let html =
            View.template [
                _h1' "Example: Click & Swap"
                View.Components.clicker ]

        Response.ofHtml html

    let handleClick : HttpHandler =
        Response.ofHtml View.Components.resetter

    let handleReset : HttpHandler =
        Response.ofHtml View.Components.clicker
```

You can see that the `handleClick` and `handleReset` handlers are simply returning the `resetter` and `clicker` components respectively. The `handleIndex` handler is returning the full HTML document with the `clicker` component.

## Web Server

To finish things off, we'll map our handlers to the expected routes and initialize the web server.

```fsharp
[<EntryPoint>]
let main args =
    let wapp = WebApplication.Create()

    let endpoints =
        [
            get "/" App.handleIndex
            get "/click" App.handleClick
            get "/reset" App.handleReset
        ]

    wapp.UseRouting()
        .UseFalco(endpoints)
        .Run()
    0 // Exit code
```

## Wrapping Up

That's it! You now have a simple web application that uses htmx to swap out components on the page without a full page reload. This is just the beginning of what you can do with htmx and Falco. You can use the same principles to create more complex interactions and components.

For more information about the htmx integration, check out the [Falco.Htmx](https://github.com/FalcoFramework/Falco.Htmx) repository. It contains a full list of all the attributes and properties that are available, as well as examples of how to use them.

[Go back to docs home](/docs)
