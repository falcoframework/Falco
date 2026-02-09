[<RequireQualifiedAccess>]
module Falco.Response

open System
open System.IO
open System.Security.Claims
open System.Text
open System.Text.Json
open Falco.Markup
open Falco.Security
open Microsoft.AspNetCore.Antiforgery
open Microsoft.AspNetCore.Authentication
open Microsoft.AspNetCore.Http
open Microsoft.Extensions.Primitives
open Microsoft.Net.Http.Headers

// ------------
// Modifiers
// ------------

/// Sets multiple headers for response.
///
/// Headers provided will replace any existing headers with the same name.
///
/// - `headers` - A list of header name and value pairs to add to the response.
let withHeaders
    (headers : (string * string) list) : HttpResponseModifier = fun ctx ->
    headers
    |> List.iter (fun (name, content : string) ->
        ctx.Response.Headers[name] <- StringValues(content))
    ctx

/// Sets ContentType header for response.
///
/// - `contentType` - The value to set for the Content-Type header.
let withContentType
    (contentType : string) : HttpResponseModifier = fun ctx ->
    ctx.Response.ContentType <- contentType
    withHeaders [ HeaderNames.ContentType, contentType ] ctx

/// Set StatusCode for response.
///
/// - `statusCode` - The HTTP status code to set for the response.
let withStatusCode
    (statusCode : int) : HttpResponseModifier = fun ctx ->
    ctx.Response.StatusCode <- statusCode
    ctx

/// Adds cookie to response.
///
/// - `key` - The name of the cookie to add to the response.
/// - `value` - The value of the cookie to add to the response.
let withCookie
    (key : string)
    (value : string) : HttpResponseModifier = fun ctx ->
    ctx.Response.Cookies.Append(key, value)
    ctx

/// Adds a configured cookie to response, via CookieOptions.
///
/// - `options` - The CookieOptions to apply when adding the cookie to the response.
/// - `key` - The name of the cookie to add to the response.
/// - `value` - The value of the cookie to add to the response.
let withCookieOptions
    (options : CookieOptions)
    (key : string)
    (value : string) : HttpResponseModifier = fun ctx ->
    ctx.Response.Cookies.Append(key, value, options)
    ctx

// ------------
// Handlers
// ------------

/// Flushes any remaining response headers or data and returns empty response.
let ofEmpty : HttpHandler = fun ctx ->
    ctx.Response.ContentLength <- 0
    ctx.Response.CompleteAsync()

type private RedirectType =
    | PermanentlyTo of url: string
    | TemporarilyTo of url: string

let private redirect
    (redirectType: RedirectType): HttpHandler = fun ctx ->
    let (permanent, url) =
        match redirectType with
        | PermanentlyTo url -> (true, url)
        | TemporarilyTo url -> (false, url)

    task {
        ctx.Response.Redirect(url, permanent)
        do! ctx.Response.CompleteAsync()
    }

/// Returns a redirect (301) to client.
///
/// - `url` - The URL to which the client will be redirected.
let redirectPermanently (url: string) =
    withStatusCode 301
    >> redirect (PermanentlyTo url)

/// Returns a redirect (302) to client.
///
/// - `url` - The URL to which the client will be redirected.
let redirectTemporarily (url: string) =
    withStatusCode 302
    >> redirect (TemporarilyTo url)

let private writeBytes
    (bytes : byte[]) : HttpHandler = fun ctx ->
        task {
            ctx.Response.ContentLength <- bytes.LongLength
            do! ctx.Response.Body.WriteAsync(bytes, 0, bytes.Length)
        }

/// Returns an inline binary (i.e., Byte[]) response with the specified
/// Content-Type.
///
/// Note: Automatically sets "content-disposition: inline".
///
/// - `contentType` - The value to set for the Content-Type header.
/// - `headers` - A list of additional header name and value pairs to add to the response.
/// - `bytes` - The binary content to write to the response body.///
let ofBinary
    (contentType : string)
    (headers : (string * string) list)
    (bytes : Byte[]) : HttpHandler =
    let headers = (HeaderNames.ContentDisposition, "inline") :: headers
    withContentType contentType
    >> withHeaders headers
    >> writeBytes bytes

/// Returns a binary (i.e., Byte[]) attachment response with the specified
/// Content-Type and optional filename.
///
/// Note: Automatically sets "content-disposition: attachment" and includes
/// filename if provided.
///
/// - `filename` - The name of the file to be used in the Content-Disposition header. If empty or null, no filename will be included.
/// - `contentType` - The value to set for the Content-Type header.
/// - `headers` - A list of additional header name and value pairs to add to the response.
/// - `bytes` - The binary content to write to the response body.
let ofAttachment
    (filename : string)
    (contentType : string)
    (headers :
    (string * string) list)
    (bytes : Byte[]) : HttpHandler =
    let contentDisposition =
        if StringUtils.strNotEmpty filename then
            let escapedFilename = HeaderUtilities.EscapeAsQuotedString filename
            StringUtils.strConcat [ "attachment; filename="; string escapedFilename ]
        else "attachment"

    let headers = (HeaderNames.ContentDisposition, contentDisposition) :: headers

    withContentType contentType
    >> withHeaders headers
    >> writeBytes bytes

/// Writes string to response body with provided encoding.
///
/// - `encoding` - The encoding to use when converting the string to bytes for the response body.
/// - `str` - The string content to write to the response body. If null, empty, or whitespace, an empty response will be returned.
let ofString
    (encoding : Encoding)
    (str : string) : HttpHandler =
    if String.IsNullOrWhiteSpace str then ofEmpty
    else writeBytes (encoding.GetBytes(str))

/// Returns a "text/plain; charset=utf-8" response with provided string to client.
///
/// - `str` - The string content to write to the response body. If null, empty, or whitespace, an empty response will be returned.
let ofPlainText
    (str : string) : HttpHandler =
    withContentType "text/plain; charset=utf-8"
    >> ofString Encoding.UTF8 str

/// Returns a "text/html; charset=utf-8" response with provided HTML string to client.
///
/// - `html` - The HTML string content to write to the response body. If null, empty, or whitespace, an empty response will be returned.
let ofHtmlString
    (html : string) : HttpHandler =
    withContentType "text/html; charset=utf-8"
    >> ofString Encoding.UTF8 html

/// Returns a "text/html; charset=utf-8" response with provided HTML to client.
///
/// - `html` - The HTML content to write to the response body. If null, empty, or whitespace, an empty response will be returned.
let ofHtml
    (html : XmlNode) : HttpHandler =
    ofHtmlString (renderHtml html)

let private withCsrfToken handleToken : HttpHandler = fun ctx ->
    let csrfToken = Xsrf.getToken ctx
    handleToken csrfToken ctx

/// Returns a CSRF token-dependant "text/html; charset=utf-8" response with
/// provided HTML to client.
///
/// - `view` - A function that takes an AntiforgeryTokenSet and returns the HTML content to write to the response body. If the returned HTML is null, empty, or whitespace, an empty response will be returned.
let ofHtmlCsrf
    (view : AntiforgeryTokenSet -> XmlNode) : HttpHandler =
    withCsrfToken (fun token -> token |> view |> ofHtml)

/// Returns a "text/html; charset=utf-8" response with provided HTML fragment,
/// if found, to client. If no element with the provided id is found, an empty
/// string is returned.
///
/// - `id` - The id of the HTML element to render and write to the response body.
/// - `html` - The HTML content to search for the element with the specified id.
let ofFragment
    (id : string)
    (html : XmlNode) : HttpHandler =
    ofHtmlString (renderFragment html id)

/// Returns a CSRF token-dependant "text/html; charset=utf-8" response with
/// provided HTML fragment, if found, to client. If no element with the
/// provided id is found, an empty string is returned.
///
/// - `id` - The id of the HTML element to render and write to the response body.
/// - `view` - A function that takes an AntiforgeryTokenSet and returns the HTML content to search for the element with the specified id.
let ofFragmentCsrf
    (id : string)
    (view : AntiforgeryTokenSet -> XmlNode) : HttpHandler =
    withCsrfToken (fun token -> token |> view |> ofFragment id)

/// Returns an optioned "application/json; charset=utf-8" response with the
/// serialized object provided to the client.
///
/// - `options` - The JsonSerializerOptions to use when serializing the object to JSON.
/// - `obj` - The object to serialize to JSON and write to the response body.
let ofJsonOptions
    (options : JsonSerializerOptions)
    (obj : 'T) : HttpHandler =
    withContentType "application/json; charset=utf-8"
    >> fun ctx -> task {
        use str = new MemoryStream()
        do! JsonSerializer.SerializeAsync(str, obj, options)
        ctx.Response.ContentLength <- str.Length
        str.Position <- 0
        do! str.CopyToAsync ctx.Response.Body
    }

/// Returns a "application/json; charset=utf-8" response with the serialized
/// object provided to the client.
///
/// - `obj` - The object to serialize to JSON and write to the response body.
let ofJson
    (obj : 'T) : HttpHandler =
    withContentType "application/json; charset=utf-8"
    >> ofJsonOptions Request.defaultJsonOptions obj

/// Signs in claim principal for provided scheme then responds with a 301 redirect
/// to provided URL.
///
/// - `authScheme` - The name of the authentication scheme to use when signing in the claim principal.
/// - `claimsPrincipal` - The ClaimsPrincipal to sign in for the specified authentication scheme.
let signIn
    (authScheme : string)
    (claimsPrincipal : ClaimsPrincipal) : HttpHandler = fun ctx ->
    task {
        do! ctx.SignInAsync(authScheme, claimsPrincipal)
    }

/// Signs in claim principal for provided scheme and options then responds with a
/// 301 redirect to provided URL (via AuthenticationProperties.RedirectUri).
///
/// - `authScheme` - The name of the authentication scheme to use when signing in the claim principal.
/// - `claimsPrincipal` - The ClaimsPrincipal to sign in for the specified authentication scheme.
/// - `options` - The AuthenticationProperties to use when signing in the claim principal, which may include a RedirectUri for the post-sign-in redirect URL.
let signInOptions
    (authScheme : string)
    (claimsPrincipal : ClaimsPrincipal)
    (options : AuthenticationProperties) : HttpHandler =
    withHeaders [
        if not (String.IsNullOrEmpty options.RedirectUri) then
            HeaderNames.Location, options.RedirectUri ]
    >> (if not (String.IsNullOrEmpty options.RedirectUri) then withStatusCode 301 else id)
    >> fun ctx ->
    task {
        do! ctx.SignInAsync(authScheme, claimsPrincipal, options)
    }

/// Signs in claim principal for provided scheme then responds with a 301 redirect
/// to provided URL (via AuthenticationProperties.RedirectUri).
///
/// - `authScheme` - The name of the authentication scheme to use when signing in the claim principal.
/// - `claimsPrincipal` - The ClaimsPrincipal to sign in for the specified authentication scheme.
/// - `url` - The URL to which the client will be redirected after signing in, which will be set in the AuthenticationProperties.RedirectUri.
let signInAndRedirect
    (authScheme : string)
    (claimsPrincipal : ClaimsPrincipal)
    (url : string) : HttpHandler =
    let options = AuthenticationProperties(RedirectUri = url)
    signInOptions authScheme claimsPrincipal options

/// Terminates authenticated context for provided scheme then responds with a 301
/// redirect to provided URL (via AuthenticationProperties.RedirectUri).
///
/// - `authScheme` - The name of the authentication scheme to use when signing out the authenticated context.
let signOut
    (authScheme : string) : HttpHandler = fun ctx ->
    task {
        do! ctx.SignOutAsync authScheme
    }

/// Terminates authenticated context for provided scheme then responds with a 301
/// redirect to provided URL.
///
/// - `authScheme` - The name of the authentication scheme to use when signing out the authenticated context.
/// - `options` - The AuthenticationProperties to use when signing out, which may include a RedirectUri for the post-sign-out redirect URL.
let signOutOptions
    (authScheme : string)
    (options : AuthenticationProperties) : HttpHandler =
    withHeaders [
        if not (String.IsNullOrEmpty options.RedirectUri) then
            HeaderNames.Location, options.RedirectUri ]
    >> (if not (String.IsNullOrEmpty options.RedirectUri) then withStatusCode 301 else id)
    >> fun ctx ->
    task {
        do! ctx.SignOutAsync(authScheme, options)
    }

/// Terminates authenticated context for provided scheme then responds with a 301
/// redirect to provided URL.
///
/// - `authScheme` - The name of the authentication scheme to use when signing out the authenticated context.
/// - `url` - The URL to which the client will be redirected after signing out, which will be set in the AuthenticationProperties.RedirectUri.
let signOutAndRedirect
    (authScheme : string)
    (url : string) : HttpHandler =
    let options = AuthenticationProperties(RedirectUri = url)
    signOutOptions authScheme options

/// Challenges the specified authentication scheme.
/// An authentication challenge can be issued when an unauthenticated user
/// requests an endpoint that requires authentication. Then given redirectUri is
/// forwarded to the authentication handler for use after authentication succeeds.
///
/// Note: If options.RedirectUri is provided, a 401 status code and Location header
/// will be included in the response, with the Location header set to the RedirectUri.
/// Otherwise, no status code or Location header will be included in the response.
///
/// - `authScheme` - The name of the authentication scheme to challenge.
/// - `options` - The AuthenticationProperties to use when challenging, which may include a RedirectUri for the post-challenge redirect URL.
let challengeOptions
    (authScheme : string)
    (options : AuthenticationProperties) : HttpHandler =
    withStatusCode 401
    >> withHeaders [
        HeaderNames.WWWAuthenticate, authScheme
        if not (String.IsNullOrEmpty options.RedirectUri) then
            HeaderNames.Location, options.RedirectUri ]
    >> fun ctx ->
    task {
        do! ctx.ChallengeAsync(authScheme, options)
    }

/// Challenges the specified authentication scheme.
/// An authentication challenge can be issued when an unauthenticated user
/// requests an endpoint that requires authentication. Then given redirectUri is
/// forwarded to the authentication handler for use after authentication succeeds.
///
/// Note: A 401 status code and Location header will be included in the response, with the Location header set to the provided redirectUri.
///
/// - `authScheme` - The name of the authentication scheme to challenge.
/// - `redirectUri` - The URL to which the client will be redirected after the challenge, which will be set in the AuthenticationProperties.RedirectUri.
let challengeAndRedirect
    (authScheme : string)
    (redirectUri : string) : HttpHandler =
    let options = AuthenticationProperties(RedirectUri = redirectUri)
    challengeOptions authScheme options
