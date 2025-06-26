# Host Configuration

As your app becomes more complex, you'll inevitably need to reach for some additional host configuration. This is where the `Microsoft.AspNetCore.Builder` import comes in. This assembly contains many useful extensions for configuring the server (ex: static files, authentication, authorization etc.).

Most of the extension methods have existed since the early days of ASP.NET Core and operate against `IApplicationBuilder`. But more recent version of ASP.NET Core have introduced a new `WebApplication` type that implements `IApplicationBuilder` and provides some additional functionality,  notably endpoint configuration. This dichotomy makes pipelining next to impossible. In C# you don't feel the sting of this as much because of `void` returns. But in F# this results in an excess amount of `|> ignore` calls.

Let's take the hero code from the [Getting Started](get-started.md) page and add the static file middleware to it:

```fsharp
module Program

open Falco
open Falco.Routing
open Microsoft.AspNetCore.Builder

let wapp = WebApplication.Create()

wapp.UseRouting()
    .UseDefaultFiles() // you might innocently think this is fine
    .UseStaticFiles()  // and so is this
                       // but uknowingly, the underlying type has changed
    .UseFalco([
        get "/" (Response.ofPlainText "Hello World!")
    ])
    .Run(Response.ofPlainText "Not found")
    // ^-- this is no longer starts up our application

// one way to fix this:
wapp.UseRouting() |> ignore
wapp.UseDefaultFiles().UseStaticFiles() |> ignore

wapp.UseFalco([
        get "/" (Response.ofPlainText "Hello World!")
    ])
    .Run(Response.ofPlainText "Not found")

// but we can do better
```

To salve this, Falco comes with a several shims. The most important of these are `WebApplication.Use` and `WebApplication.UseIf` which allow you to compose a pipeline entirely driven by `WebApplication` while at the same time taking advantage of the existing ASP.NET Core extensions.

```fsharp
wapp.UseRouting()
    .Use(fun (appl : IApplicationBuilder) ->
        appl.UseDefaultFiles()
            .UseStaticFiles())
    .UseFalco([
        get "/" (Response.ofPlainText "Hello World!")
    ])
    .Run(Response.ofPlainText "Not found")
```

The optional, but recommended way to take advantage of these is to utilize the static methods that server as the underpinning to the various extension methods available. The code below will attempt to highlight this more clearly:

```fsharp
// better yet
wapp.UseRouting()
    .Use(DefaultFilesExtensions.UseDefaultFiles)
    .Use(StaticFileExtensions.UseStaticFiles)
      // ^-- most IApplicationBuilder extensions are available as static methods similar to this
    .UseFalco([
        get "/" (Response.ofPlainText "Hello World!")
    ])
    .Run(Response.ofPlainText "Not found")
```

Next, we can use the `UseIf` extension method to conditionally add middleware to the pipeline. This is useful for things like development exception pages, or other middleware that you only want in certain environments.

```fsharp
let isDevelopment = wapp.Environment.EnvironmentName = "Development"
wapp.UseRouting()
    .UseIf(isDevelopment, DeveloperExceptionPageExtensions.UseDeveloperExceptionPage)
    .UseIf(not(isDevelopment), FalcoExtensions.UseFalcoExceptionHandler ErrorPage.serverException)
    .Use(DefaultFilesExtensions.UseDefaultFiles)
    .Use(StaticFileExtensions.UseStaticFiles)
    .UseFalco([
        get "/" (Response.ofPlainText "Hello World!")
    ])
    .Run(Response.ofPlainText "Not found")
```

This is a great way to keep your code clean and readable, while still taking advantage of the powerful middleware pipeline that ASP.NET Core provides.

[Next: Deployment](deployment.md)
