# Cross-site Scripting (XSS) Attacks

Cross-site scripting attacks are extremely common since they are quite simple to carry out. Fortunately, protecting against them is as easy as performing them.

The [Microsoft.AspNetCore.Antiforgery](https://docs.microsoft.com/en-us/aspnet/core/security/anti-request-forgery) package provides the required utilities to easily protect yourself against such attacks.

## Activating Antiforgery Protection

To use the Falco Xsrf helpers, ensure that the `Antiforgery` service has been [registered](https://learn.microsoft.com/en-us/aspnet/core/security/anti-request-forgery).

```fsharp
open Falco
open Falco.Routing
open Microsoft.AspNetCore.Builder
open Microsoft.Extensions.DependencyInjection
// ^-- this import enables antiforgery activation

let endpoints =
    [
        // endpoints...
    ]

let bldr = WebApplication.CreateBuilder()

bldr.Services
    .AddAntiforgery()

let wapp = WebApplication.Create()

wapp.UseAntiforgery()
    // ^-- activate Antiforgery before routing
    .UseRouting()
    .UseFalco(endpoints)
    .Run()

```

## Falco XSRF Support

Falco provides a few handlers via `Falco.Security.Xsrf`:

```fsharp
open Falco.Markup
open Falco.Security

let formView token =
    _html [] [
        _body [] [
            _form [ _methodPost_ ] [
                // using the CSRF HTML helper, recommended to include as first
                // form element
                Xsrf.antiforgeryInput token
                _control "first_name" [] [ _text "First Name" ]
                _control "first_name" [] [ _text "First Name" ]
                _input [ _typeSubmit_ ]
            ]
        ]
    ]

// A handler that demonstrates obtaining a
// CSRF token and applying it to a view
let csrfViewHandler : HttpHandler =
    Response.ofHtmlCsrf formView

// A handler that demonstrates validating
// the request's CSRF token
let mapFormSecureHandler : HttpHandler =
    let mapPerson (form : FormData) =
        { FirstName = form?first_name.AsString()
          LastName = form?last_name.AsString }

    let handleInvalid : HttpHandler =
        Response.withStatusCode 400
        >> Response.ofEmpty

    Request.mapFormSecure mapPerson Response.ofJson handleInvalid
```

[Next: Authentication](authentication.md)
