# Authentication & Authorization.

ASP.NET Core has built-in support for authentication and authorization. Falco includes some prebuilt, configurable handlers for common scenarios.

> Review the [docs](https://docs.microsoft.com/en-us/aspnet/core/security/authentication) for specific implementation details.

## Secure Resources

### Allow only authenticated access

```fsharp
open Falco

let authScheme = "some.secure.scheme"

let secureResourceHandler : HttpHandler =
    let handleAuth : HttpHandler =
        Response.ofPlainText "hello authenticated user"

    Request.ifAuthenticated authScheme handleAuth
```


### Allow only non-authenticated access

```fsharp
open Falco

let anonResourceOnlyHandler : HttpHandler =
    let handleAnon : HttpHandler =
        Response.ofPlainText "hello anonymous"

    Request.ifNotAuthenticated authScheme handleAnon
```


### Allow only authenticated access when in certain role(s)

```fsharp
open Falco

let secureResourceHandler : HttpHandler =
    let handleAuthInRole : HttpHandler =
        Response.ofPlainText "hello admin"

    let rolesAllowed = [ "Admin" ]

    Request.ifAuthenticatedInRole authScheme rolesAllowed handleAuthInRole
```


### Allow only authenticated acces with a certain scope

```fsharp
open Falco

let secureResourceHandler : HttpHandler =
    let handleAuthHasScope : HttpHandler =
        Response.ofPlainText "user1, user2, user3"

    let issuer = "https://oauth2issuer.com"
    let scope = "read:users"

    Request.ifAuthenticatedWithScope authScheme issuer scope handleAuthHasScope
```

### Terminate authenticated session

```fsharp
open Falco

let logOut : HttpHandler =
    let authScheme = "..."
    let redirectTo = "/login"

    Response.signOutAndRedirect authScheme redirectTo
```

[Next: Deployment](deployment.md)
