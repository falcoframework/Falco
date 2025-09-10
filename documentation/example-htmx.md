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
        _html [ _lang_ "en" ] [
            _head [] [
                _script [ _src_ HtmxScript.cdnSrc ] [] ]
            _body [] content ]
```

With our layout defined, we can create a view to represent our starting state.

```fsharp
module View =
    // Layout ...

    let clickAndSwap =
        template [
            _h1' "Example: Click & Swap"
            _div [ _id_ "content" ] [
                _button [
                    _id_ "clicker"
                    Hx.get "/click"
                    Hx.swapOuterHtml ]
                    [ _text "Click Me" ] ] ]
```

This view contains a button that, when clicked, will send a GET request to the `/click` endpoint. The response from that request will replace the button with the response from the server.

## Components

A nice convention when working with Falco.Markup is to create a `Components` module within your `View` module. We'll define one component here.

All of the htmx attributes and properties are mapped within the `Hx` module. Wherever a limited scope of options exist, strongly typed references are provided. For example, `Hx.swapInnerHtml` is a strongly typed reference to the `hx-swap` attribute with the value `innerHTML`. This is a great way to avoid typos and ensure that your code is type-safe.

```fsharp
module View =
    // Layout & view ...

    module Components =
        let resetter =
            _div [ _id_ "resetter" ] [
                _h2' "Way to go! You clicked it!"
                _br []
                _button [
                    Hx.get "/reset"
                    Hx.swapOuterHtml
                    Hx.targetCss "#resetter" ]
                    [ _text "Reset" ] ]
```

The `resetter` component is a simple button that will send a GET request to the server when clicked. The response will replace the entire `div` with the ID of `resetter` with the response from the server.

## Handlers

Next we define a couple basic handlers to handle the requests for the original document and ajax requests.

```fsharp
module App =
    let handleIndex : HttpHandler =
        Response.ofHtml View.clickAndSwap

    let handleClick : HttpHandler =
        Response.ofHtml View.Components.resetter

    let handleReset : HttpHandler =
        Response.ofFragment "clicker" View.clickAndSwap
```

The `handleIndex` handler is returning our full click-and-swap view, containing the clicker button. Clicking it triggers a request to the `handleClick` handler, which returns the resetter component. Clicking the reset button triggers a request to the `handleReset` handler, which returns the original clicker button as a [template fragment], extracted from the same view as the original state.

## Web Server

To finish things off, we'll map our handlers to the expected routes and initialize the web server.

```fsharp
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
```

## Wrapping Up

That's it! You now have a simple web application that uses htmx to swap out components on the page without a full page reload. This is just the beginning of what you can do with htmx and Falco. You can use the same principles to create more complex interactions and components.

For more information about the htmx integration, check out the [Falco.Htmx](https://github.com/FalcoFramework/Falco.Htmx) repository. It contains a full list of all the attributes and properties that are available, as well as examples of how to use them.

[Go back to docs home](/docs)
