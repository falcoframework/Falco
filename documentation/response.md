# Response Writing

The `HttpHandler` type is used to represent the processing of a request. It can be thought of as the eventual (i.e., asynchronous) completion and processing of an HTTP request, defined in F# as: `HttpContext -> Task`. Handlers will typically involve some combination of: [route inspection](request.md#route-binding), [form](request.md#form-binding)/[query](request.md#query-binding) binding, business logic and finally response writing. With access to the `HttpContext` you are able to inspect all components of the request, and manipulate the response in any way you choose.

## Plain Text responses

```fsharp
let textHandler : HttpHandler =
    Response.ofPlainText "hello world"
```

## HTML responses

Write your views in plain F#, directly in your assembly, using the [Markup](markup.md) module. A performant F# DSL capable of generating any angle-bracket markup. Also available directly as a standalone [NuGet](https://www.nuget.org/packages/Falco.Markup) package.

```fsharp
let htmlHandler : HttpHandler =
    let html =
        _html [ _lang_ "en" ] [
            _head [] []
            _body [] [
                _h1' "Sample App" // shorthand for: `_h1 [] [ Text.raw "Sample App" ]`
            ]
        ]

    Response.ofHtml html

// Automatically protect against XSS attacks
let secureHtmlHandler : HttpHandler =
    let html token =
        _html [] [
            _body [] [
                _form [ _method_ "post" ] [
                    _input [ _name_ "first_name" ]
                    _input [ _name_ "last_name" ]
                    // using the CSRF HTML helper
                    Xsrf.antiforgeryInput token
                    _input [ _type_ "submit"; _value_ "Submit" ]
                ]
            ]
        ]

    Response.ofHtmlCsrf html
```

Alternatively, if you're using an external view engine and want to return an HTML response from a string literal, then you can use `Response.ofHtmlString`.

```fsharp
let htmlHandler : HttpHandler =
    Response.ofHtmlString "<html>...</html>"
```

## JSON responses

These handlers use the .NET built-in `System.Text.Json.JsonSerializer`.

```fsharp
type Person =
    { First : string
      Last  : string }

let jsonHandler : HttpHandler =
    let name = { First = "John"; Last = "Doe" }
    Response.ofJson name

let jsonOptionsHandler : HttpHandler =
    let options = JsonSerializerOptions()
    options.DefaultIgnoreCondition <- JsonIgnoreCondition.WhenWritingNull
    let name = { First = "John"; Last = "Doe" }
    Response.ofJsonOptions options name
```

## Redirect (301/302) Response

```fsharp
let oldUrlHandler : HttpHandler =
    Response.redirectPermanently "/new-url" // HTTP 301

let redirectUrlHandler : HttpHandler =
    Response.redirectTemporarily "/new-url" // HTTP 302
```

## Content Disposition

```fsharp
let inlineBinaryHandler : HttpHandler =
    let contentType = "image/jpeg"
    let headers = [ HeaderNames.CacheControl,  "no-store, max-age=0" ]
    let bytes = // ... binary data
    Response.ofBinary contentType headers bytes

let attachmentHandler : HttpHandler =
    let filename = "profile.jpg"
    let contentType = "image/jpeg"
    let headers = [ HeaderNames.CacheControl,  "no-store, max-age=0" ]
    let bytes = // ... binary data
    Response.ofAttachment filename contentType headers bytes
```

## Response Modifiers

Response modifiers can be thought of as the in-and-out modification of the `HttpResponse`. A preamble to writing and returning. Since these functions receive the `Httpcontext` as input and return it as the only output, they can take advantage of function compoistion.

### Set the status code of the response

```fsharp
let notFoundHandler : HttpHandler =
    Response.withStatusCode 404
    >> Response.ofPlainText "Not found"
```

### Add a header(s) to the response

```fsharp
let handlerWithHeaders : HttpHandler =
    Response.withHeaders [ "Content-Language", "en-us" ]
    >> Response.ofPlainText "Hello world"
```

### Add a cookie to the response

> IMPORTANT: *Do not* use this for authentication. Instead use the `Response.signInAndRedirect` and `Response.signOutAndRedirect` functions found in the [Authentication](authenication.md) module.

```fsharp
let handlerWithCookie : HttpHandler =
    Response.withCookie "greeted" "1"
    >> Response.ofPlainText "Hello world"

let handlerWithCookieOptions : HttpHandler =
    let options = CookieOptions()
    options.Expires <- DateTime.Now.Minutes(15)
    Response.withCookie options "greeted" "1"
    >> Response.ofPlainText "Hello world"
```

[Next: Request Handling](request.md)
