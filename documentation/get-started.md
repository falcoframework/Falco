# Getting Started

## Using `dotnet new`

The easiest way to get started with Falco is by installing the `Falco.Template` package, which adds a new template to your `dotnet new` command line tool:

```shell
> dotnet new install "Falco.Template::*"
```

Afterwards you can create a new Falco application by running:

```shell
> dotnet new falco -o HelloWorldApp
> cd HelloWorldApp
> dotnet run
```

## Manually installing

Create a new F# web project:

```shell
> dotnet new web -lang F# -o HelloWorldApp
> cd HelloWorldApp
```

Install the nuget package:

```shell
> dotnet add package Falco
```

Remove any `*.fs` files created automatically, create a new file named `Program.fs` and set the contents to the following:

```fsharp
open Falco
open Falco.Routing
open Microsoft.AspNetCore.Builder
// ^-- this import adds many useful extensions

let endpoints =
    [
        get "/" (Response.ofPlainText "Hello World!")
        // ^-- associate GET / to plain text HttpHandler
    ]

let wapp = WebApplication.Create()

wapp.UseRouting()
    .UseFalco(endpoints)
    // ^-- activate Falco endpoint source
    .Run(Response.ofPlainText "Not found")
    // ^-- run app and register terminal (i.e., not found) middleware
```

Run the application:

```shell
> dotnet run
```

And there you have it, an industrial-strength [Hello World](https://github.com/FalcoFramework/Falco/tree/master/examples/HelloWorld) web app. Pretty sweet!

[Next: Routing](routing.md)
