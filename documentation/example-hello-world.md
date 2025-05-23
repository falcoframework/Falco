# Example - Hello World

The goal of this program is to demonstrate the absolute bare bones hello world application, so that we can focus on the key elements when initiating a new web application.

The code for this example can be found [here](https://github.com/FalcoFramework/Falco/tree/master/examples/HelloWorld).

## Creating the Application Manually

```shell
> dotnet new falco -o HelloWorldApp
```

## Code Overview

```fsharp
open Falco
open Falco.Routing
open Microsoft.AspNetCore.Builder
// ^-- this import adds many useful extensions

let wapp = WebApplication.Create()

wapp.UseRouting()
    .UseFalco([
    // ^-- activate Falco endpoint source
        get "/" (Response.ofPlainText "Hello World!")
        // ^-- associate GET / to plain text HttpHandler
    ])
    .Run(Response.ofPlainText "Not found")
    // ^-- activate Falco endpoint source
```

First, we open the required namespaces. `Falco` bring into scope the ability to activate the library and some other extension methods to make the fluent API more user-friendly.

`Microsoft.AspNetCore.Builder` enables us to create web applications in a number of ways, we're using `WebApplication.Create()` above. It also adds many other useful extension methods, that you'll see later.

After creating the web application, we:

- Activate Falco using `wapp.UseFalco()`. This enables us to create endpoints.
- Register `GET /` endpoint to a handler which responds with "hello world".
- Run the app.

[Next: Example - Hello World MVC](example-hello-world-mvc.md)
