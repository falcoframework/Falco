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
let withHeaders
    (headers : (string * string) list) : HttpResponseModifier = fun ctx ->
    headers
    |> List.iter (fun (name, content : string) ->
        ctx.Response.Headers[name] <- StringValues(content))
    ctx

/// Sets ContentType header for response.
let withContentType
    (contentType : string) : HttpResponseModifier =
    withHeaders [ HeaderNames.ContentType, contentType ]

/// Set StatusCode for response.
let withStatusCode
    (statusCode : int) : HttpResponseModifier = fun ctx ->
    ctx.Response.StatusCode <- statusCode
    ctx

/// Adds cookie to response.
let withCookie
    (key : string)
    (value : string) : HttpResponseModifier = fun ctx ->
    ctx.Response.Cookies.Append(key, value)
    ctx

/// Adds a configured cookie to response, via CookieOptions.
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
    ctx.Response.Redirect(url, permanent)
    ctx.Response.CompleteAsync()

/// Returns a redirect (301) to client.
let redirectPermanently (url: string) =
    redirect (PermanentlyTo url)

/// Returns a redirect (302) to client.
let redirectTemporarily (url: string) =
    redirect (TemporarilyTo url)

let private setContentLength
    (contentLength : int64)
    (ctx : HttpContext) =
    ctx.Response.ContentLength <- contentLength

let private writeBytes
    (bytes : byte[]) : HttpHandler = fun ctx ->
        task {
            setContentLength bytes.LongLength ctx
            do! ctx.Response.Body.WriteAsync(bytes, 0, bytes.Length)
        }

let private writeStream
    (str : Stream) : HttpHandler = fun ctx ->
        task {
            setContentLength str.Length ctx
            str.Position <- 0
            do! str.CopyToAsync(ctx.Response.Body)
        }

/// Returns an inline binary (i.e., Byte[]) response with the specified
/// Content-Type.
///
/// Note: Automatically sets "content-disposition: inline".
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
let ofAttachment
    (filename : string)
    (contentType : string)
    (headers :
    (string * string) list)
    (bytes : Byte[]) : HttpHandler =
    let contentDisposition =
        if StringUtils.strNotEmpty filename then
            StringUtils.strConcat [ "attachment; filename=\""; filename; "\"" ]
        else "attachment"

    let headers = (HeaderNames.ContentDisposition, contentDisposition) :: headers

    withContentType contentType
    >> withHeaders headers
    >> writeBytes bytes

/// Writes string to response body with provided encoding.
let ofString
    (encoding : Encoding)
    (str : string) : HttpHandler =
    if isNull str then ofEmpty
    else writeBytes (encoding.GetBytes(str))

/// Returns a "text/plain; charset=utf-8" response with provided string to
/// client.
let ofPlainText
    (str : string) : HttpHandler =
    withContentType "text/plain; charset=utf-8"
    >> ofString Encoding.UTF8 str

/// Returns a "text/html; charset=utf-8" response with provided HTML string to
/// client.
let ofHtmlString
    (html : string) : HttpHandler =
    withContentType "text/html; charset=utf-8"
    >> ofString Encoding.UTF8 html

/// Returns a "text/html; charset=utf-8" response with provided HTML to client.
let ofHtml
    (html : XmlNode) : HttpHandler =
    let render =
        match html with
        | NodeList _ -> renderNode
        | SelfClosingNode _
        | ParentNode _
        | TextNode _ -> renderHtml

    ofHtmlString (render html)

/// Returns a CSRF token-dependant "text/html; charset=utf-8" response with
/// provided HTML to client.
let ofHtmlCsrf
    (view : AntiforgeryTokenSet -> XmlNode) : HttpHandler =
    let withCsrfToken handleToken : HttpHandler = fun ctx ->
        let csrfToken = Xsrf.getToken ctx
        handleToken csrfToken ctx

    withCsrfToken (fun token -> token |> view |> ofHtml)

/// Returns an optioned "application/json; charset=utf-8" response with the
/// serialized object provided to the client.
let ofJsonOptions
    (options : JsonSerializerOptions)
    (obj : 'T) : HttpHandler = fun ctx ->
    task {
        use str = new MemoryStream()
        do! JsonSerializer.SerializeAsync(str, obj, options)
        return!
            withContentType "application/json; charset=utf-8" ctx
            |> writeStream str
    }

/// Returns a "application/json; charset=utf-8" response with the serialized
/// object provided to the client.
let ofJson
    (obj : 'T) : HttpHandler =
    withContentType "application/json; charset=utf-8"
    >> ofJsonOptions Request.defaultJsonOptions obj

/// Signs in claim principal for provided scheme then responds with a
/// 301 redirect to provided URL.
let signIn
    (authScheme : string)
    (claimsPrincipal : ClaimsPrincipal) : HttpHandler = fun ctx ->
    task {
        do! ctx.SignInAsync(authScheme, claimsPrincipal)
    }

/// Signs in claim principal for provided scheme and options then responds with a
/// 301 redirect to provided URL.
let signInOptions
    (authScheme : string)
    (claimsPrincipal : ClaimsPrincipal)
    (options : AuthenticationProperties) : HttpHandler = fun ctx ->
    task {
        do! ctx.SignInAsync(authScheme, claimsPrincipal, options)
    }

/// Signs in claim principal for provided scheme then responds with a 301 redirect
/// to provided URL.
let signInAndRedirect
    (authScheme : string)
    (claimsPrincipal : ClaimsPrincipal)
    (url : string) : HttpHandler =
    let options = AuthenticationProperties(RedirectUri = url)
    signInOptions authScheme claimsPrincipal options

/// Terminates authenticated context for provided scheme then responds with a 301
/// redirect to provided URL.
let signOut
    (authScheme : string) : HttpHandler = fun ctx ->
    task {
        do! ctx.SignOutAsync(authScheme)
    }

/// Terminates authenticated context for provided scheme then responds with a 301
/// redirect to provided URL.
let signOutOptions
    (authScheme : string)
    (options : AuthenticationProperties) : HttpHandler = fun ctx ->
    task {
        do! ctx.SignOutAsync(authScheme, options)
    }

/// Terminates authenticated context for provided scheme then responds with a 301
/// redirect to provided URL.
let signOutAndRedirect
    (authScheme : string)
    (url : string) : HttpHandler =
    let options = AuthenticationProperties(RedirectUri = url)
    signOutOptions authScheme options

/// Challenges the specified authentication scheme.
/// An authentication challenge can be issued when an unauthenticated user
/// requests an endpoint that requires authentication. Then given redirectUri is
/// forwarded to the authentication handler for use after authentication succeeds.
let challengeOptions
    (authScheme : string)
    (options : AuthenticationProperties) : HttpHandler = fun ctx ->
    task {
        do! ctx.ChallengeAsync(authScheme, options)
    }

/// Challenges the specified authentication scheme.
/// An authentication challenge can be issued when an unauthenticated user
/// requests an endpoint that requires authentication. Then given redirectUri is
/// forwarded to the authentication handler for use after authentication succeeds.
let challengeAndRedirect
    (authScheme : string)
    (redirectUri : string) : HttpHandler =
    let options = AuthenticationProperties(RedirectUri = redirectUri)
    challengeOptions authScheme options
