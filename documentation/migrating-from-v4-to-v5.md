# Migrating from v4.x to v5.x

With Falco v5.x the main objective was to simplify the API and improve the overall devlopment experience long term. The idea being provide only what is necessary, or provides the most value in the most frequently developed areas.

This document will attempt to cover the anticipated transformations necessary to upgrade from v4.x to v5.x. Pull requests are welcome for missing scenarios, thank you in advance for your help.

## `webHost` expression

Perhaps the most significant change is the removal of the `webHost` expression, which attempted to make web application server construction more pleasant. Microsoft has made really nice strides in this area (i.e., `WebApplication`) and it's been difficult at times to stay sync with the breaking changes to the underlying interfaces. As such, we elected to remove it altogether.

Below demonstrates how to migrate a "hello world" app from v4 to v5 by replacing the `webHost` expression with the Microsoft provided `WebApplicationBuilder`.

<table>
<tr>
<td>

```fsharp
// Falco v4.x
open Falco

webHost args {

    use_static_files

    endpoints [
        get "/"
            (Response.ofPlainText "hello world")
    ]
}
```

</td>
<td>

```fsharp
// Falco v5.x
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
    .Run()
```

</td>
</tr>
</table>

## `configuration` expression

The configuration expression has also been removed. Again, the idea being to try and get in the way of potentially evolving APIs as much as possible. Even more so in the areas where the code was mostly decorative.

> Note: This example is entirely trivial since the `WebApplication.CreateBuilder()` configures a host with common, sensible defaults.

<table>
<tr>
<td>

```fsharp
open Falco
open Falco.HostBuilder

let config = configuration [||] {
    required_json "appsettings.json"
    optional_json "appsettings.Development.json"
}

webHost [||] {
    endpoints []
}
```

</td>
<td>

```fsharp
open Falco
open Falco.Routing
open Microsoft.AspNetCore.Builder
open Microsoft.Extensions.Configuration
// ^-- this import adds access to Configuration

let bldr = WebApplication.CreateBuilder()
let conf =
    bldr.Configuration
        .AddJsonFile("appsettings.json", optional = false)
        .AddJsonFile("appsettings.Development.json")

let wapp = WebApplication.Create()

let endpoints = []

wapp.UseRouting()
    .UseFalco(endpoints)
    .Run()
```

</td>
</tr>
</table>

## `StringCollectionReader` replaced by `RequestData`

For the most part, this upgrade won't require any changes for the end user. Especially if the continuation-style functions in the `Request` module were used.

Explicit references to: `CookieCollectionReader`, `HeaderCollectionReader`, `RouteCollectionReader`, `QueryCollectionReader` will need to be updated to `RequestData`. `FormCollectionReader` has been replaced by `FormData`.

## Form Streaming

Falco now automatically detects whether the form is transmiting `multipart/form-data`, which means deprecating the `Request` module streaming functions.

- `Request.streamForm` becomes -> `Request.mapForm`
- `Request.streamFormSecure` becomes -> `Request.mapFormSecure`
- `Request.mapFormStream`  becomes -> `Request.mapForm`
- `Request.mapFormStreamSecure` becomes -> `Request.mapFormSecure`

## Removed `Services.inject<'T1 .. 'T5>`

This type was removed because it continued to pose problems for certain code analysis tools. To continue using the service locator pattern, you can now use the more versatile `HttpContext` extension method `ctx.Plug<T>()`. For example:

```fsharp
let myHandler : HttpHandler =
    Services.inject<MyService> (fun myService ctx ->
        let message = myService.CreateMessage()
        Response.ofPlainText $"{message}" ctx)

// becomes
let myHandler : HttpHandler = fun ctx ->
    let myService = ctx.Plug<MyService>()
    let message = myService.CreateMessage()
    Response.ofPlainText $"{message}" ctx

```

## `Xss` module renamed to `Xsrf`

The `Xss` module has been renamed to `Xsrf` to better describe it's intent.

```fsharp
    //before: Xss.antiforgeryInput
    Xsrf.antiforgeryInput // ..

    //before: Xss.getToken
    Xsrf.getToken // ..

    //before: Xss.validateToken
    Xsrf.validateToken // ..
```

## `Crypto` module removed

The Crypto module provided functionality for: random numbers, salt generation and key derivation. The code in this module was really a veneer on top of the cryptographic providers in the base library. Extracting this code into your project would be dead simple. The [source](https://github.com/FalcoFramework/Falco/blob/25d828d832c0fde2dfff04775bea1eced9050458/src/Falco/Security.fs#L3) is permalinked here for such purposes.

## `Auth` module removed

The `Auth` module functionality was ported one-to-one to the `Response` module.
