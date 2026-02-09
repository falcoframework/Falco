[<RequireQualifiedAccess>]
module Falco.Request

open System
open System.IO
open System.Security.Claims
open System.Text
open System.Text.Json
open System.Threading
open System.Threading.Tasks
open Microsoft.AspNetCore.Authentication
open Microsoft.AspNetCore.Http
open Falco.Multipart
open Falco.Security
open Falco.StringUtils

let private defaultTaskTimeout =
    TimeSpan.FromSeconds 30.0

let internal defaultJsonOptions =
    let options = JsonSerializerOptions()
    options.AllowTrailingCommas <- true
    options.PropertyNameCaseInsensitive <- true
    options.TypeInfoResolver <- JsonSerializerOptions.Default.TypeInfoResolver
    options.MakeReadOnly() // optimize for reuse
    options

/// Obtains the `HttpVerb` of the request.
let getVerb (ctx : HttpContext) : HttpVerb =
    match ctx.Request.Method with
    | m when strEquals m HttpMethods.Get     -> GET
    | m when strEquals m HttpMethods.Head    -> HEAD
    | m when strEquals m HttpMethods.Post    -> POST
    | m when strEquals m HttpMethods.Put     -> PUT
    | m when strEquals m HttpMethods.Patch   -> PATCH
    | m when strEquals m HttpMethods.Delete  -> DELETE
    | m when strEquals m HttpMethods.Options -> OPTIONS
    | m when strEquals m HttpMethods.Trace   -> TRACE
    | _ -> ANY

/// Streams the request body into a string, up to the maximum size defined by `maxSize`.
/// Cannot be called after the body has already been read, and will throw an
/// exception if the body is not seekable and empty.
///
/// - `maxSize`: The maximum size, in total bytes, allowed for the body being read.
let getBodyStringOptions (maxSize : int64) (ctx : HttpContext) : Task<string> =
    task {
        use tokenSource = new CancellationTokenSource(defaultTaskTimeout)
        let str = new MemoryStream()

        let mutable bytesRead = 0L
        let buffer = Array.zeroCreate 65536 // 64KB chunks
        let mutable shouldRead = true

        while shouldRead do
            let! count = ctx.Request.Body.ReadAsync(buffer, 0, buffer.Length, tokenSource.Token)
            match count with
            | 0 -> shouldRead <- false
            | n ->
                bytesRead <- bytesRead + int64 n
                if bytesRead > maxSize then
                    raise (InvalidOperationException $"Body exceeds maximum size of {maxSize} bytes")
                do! str.WriteAsync(buffer, 0, n, tokenSource.Token)

        str.Seek(0L, SeekOrigin.Begin) |> ignore
        return Encoding.UTF8.GetString(str.ToArray())
    }

/// Streams the request body into a string, up to a maximum body size of `Multipart.DefaultMaxSize`.
/// Cannot be called after the body has already been read, and will throw an
/// exception if the body is not seekable and empty.
///
/// Note: `Multipart.DefaultMaxSize` is used as the default maximum body size for
/// consistency with multipart form data processing, but can be overridden by using
/// `getBodyStringOptions` directly.
let getBodyString (ctx : HttpContext) : Task<string> =
    getBodyStringOptions Multipart.DefaultMaxSize ctx

/// Retrieves the cookie from the request. Returns a `RequestData` containing the cookie values.
let getCookies (ctx : HttpContext) : RequestData =
    RequestValue.parseCookies ctx.Request.Cookies
    |> RequestData

/// Retrieves the headers from the request. Returns a `RequestData` containing the header values.
let getHeaders (ctx : HttpContext) : RequestData  =
    RequestValue.parseHeaders ctx.Request.Headers
    |> RequestData

/// Retrieves all route values from the request, including query string. Returns a `RequestData` containing the route and query values.
let getRoute (ctx : HttpContext) : RequestData =
    RequestValue.parseRoute (ctx.Request.RouteValues, ctx.Request.Query)
    |> RequestData

/// Retrieves the query string and route values from the request. Returns a `RequestData` containing the query values.
let getQuery (ctx : HttpContext) : RequestData =
    RequestValue.parseQuery ctx.Request.Query
    |> RequestData

/// Retrieves the form collection and route values from the request.
///
/// Performs CSRF validation for POST, PUT, PATCH, DELETE requests, if antiforgery
/// services are registered and a token is provided in the request.
///
/// Automatically detects if request is multipart/form-data, and will enable streaming.
///
/// Note: Consumes the request body, so should not be called after body has already been read.
///
/// - `maxSize`: The maximum size, in total bytes, allowed for the body being read.
let getFormOptions (maxSize : int64) (ctx : HttpContext) : Task<FormData> =
    task {
        if ctx.Request.ContentLength.HasValue && ctx.Request.ContentLength.Value > maxSize then
            return FormData.Invalid
        else
            let! isAuth = Xsrf.validateToken ctx

            if isAuth then
                use tokenSource = new CancellationTokenSource(defaultTaskTimeout)

                let! form =
                    if ctx.Request.IsMultipart() then
                        ctx.Request.StreamFormAsync (tokenSource.Token, maxSize)
                    else
                        ctx.Request.ReadFormAsync tokenSource.Token

                let files = if isNull form.Files then None else Some form.Files

                let requestValue = RequestValue.parseForm (form, Some ctx.Request.RouteValues)

                return FormData(requestValue, files)

            else
                return FormData.Invalid
    }

/// Retrieves the form collection and route values from the request.
///
/// Performs CSRF validation for POST, PUT, PATCH, DELETE requests, if antiforgery
/// services are registered and a token is provided in the request.
///
/// Automatically detects if request is multipart/form-data, and will enable streaming.
///
/// Uses a default maximum body size of `Multipart.DefaultMaxSize` for consistency
/// with multipart form data processing, but can be overridden by using
/// `getFormOptions` directly.
///
/// Note: Consumes the request body, so should not be called after body has already been read.
let getForm (ctx : HttpContext) : Task<FormData> =
    getFormOptions Multipart.DefaultMaxSize ctx

/// Attempts to bind request body using System.Text.Json and provided
/// JsonSerializerOptions. If the body is empty or not JSON, returns the default
/// value of 'T.
///
/// - `options`: The `JsonSerializerOptions` to use during deserialization.
let getJsonOptions<'T>
    (options : JsonSerializerOptions)
    (ctx : HttpContext) : Task<'T> = task {
        try
            if not (ctx.Request.HasJsonContentType()) then
                ctx.Response.StatusCode <- StatusCodes.Status415UnsupportedMediaType
                return JsonSerializer.Deserialize<'T>("{}", options)

            elif ctx.Request.Body.CanSeek && ctx.Request.Body.Length = 0L then
                return JsonSerializer.Deserialize<'T>("{}", options)

            else
                use tokenSource = new CancellationTokenSource(defaultTaskTimeout)
                let! json = JsonSerializer.DeserializeAsync<'T>(ctx.Request.Body, options, tokenSource.Token).AsTask()
                return json

        with
        | :? NotSupportedException as _ ->
            return JsonSerializer.Deserialize<'T>("{}", options)
    }

/// Attempts to bind request body using System.Text.Json and default
/// `JsonSerializerOptions`. If the body is empty or not JSON, returns the default
/// value of 'T.
let getJson<'T> (ctx : HttpContext) =
    getJsonOptions<'T> defaultJsonOptions ctx

// ------------
// Handlers
// ------------

/// Buffers the current HttpRequest body into a string and provides to next `HttpHandler`.
/// Note: Uses `getBodyString`, which has a default maximum body size of `Multipart.DefaultMaxSize`
/// for consistency with multipart form data processing, but can be overridden
/// by using `getBodyStringOptions` directly.
///
/// - `next`: The next `HttpHandler` to invoke, which takes the buffered body string as input.
let bodyString
    (next : string -> HttpHandler) : HttpHandler = fun ctx ->
    task {
        let! body = getBodyString ctx
        return! next body ctx
    }

/// Projects cookie values onto 'T and provides to next HttpHandler.
///
/// - `map`: A function that maps the cookie values from the request into a new type 'T.
/// - `next`: The next `HttpHandler` to invoke, which takes the mapped 'T as input.
let mapCookies
    (map : RequestData -> 'T)
    (next : 'T -> HttpHandler) : HttpHandler = fun ctx ->
    getCookies ctx
    |> map
    |> fun route -> next route ctx

/// Projects header values onto 'T and provides to next HttpHandler.
///
/// - `map`: A function that maps the header values from the request into a new type 'T.
/// - `next`: The next `HttpHandler` to invoke, which takes the mapped 'T as input.
let mapHeaders
    (map : RequestData -> 'T)
    (next : 'T -> HttpHandler) : HttpHandler = fun ctx ->
    getHeaders ctx
    |> map
    |> fun route -> next route ctx

/// Projects route values onto 'T and provides to next HttpHandler.
///
/// - `map`: A function that maps the route values from the request into a new type 'T.
/// - `next`: The next `HttpHandler` to invoke, which takes the mapped 'T as input.
let mapRoute
    (map : RequestData -> 'T)
    (next : 'T -> HttpHandler) : HttpHandler = fun ctx ->
    getRoute ctx
    |> map
    |> fun route -> next route ctx

/// Projects query string onto 'T and provides to next HttpHandler.
///
/// - `map`: A function that maps the query string values from the request into a new type 'T.
/// - `next`: The next `HttpHandler` to invoke, which takes the mapped 'T as input.
let mapQuery
    (map : RequestData -> 'T)
    (next : 'T -> HttpHandler) : HttpHandler = fun ctx ->
    getQuery ctx
    |> map
    |> fun query -> next query ctx

/// Projects form data onto 'T and provides to next HttpHandler.
///
/// Performs CSRF validation for HTTP POST, PUT, PATCH, DELETE verbs, if antiforgery
/// services are registered and a token is provided in the request.
///
/// Automatically detects if request is content-type: multipart/form-data, and
/// if so, will enable streaming.
///
/// - `maxSize`: The maximum size, in total bytes, allowed for the body being read. Should be set according to expected form data size and server limits.
/// - `map`: A function that maps the form data from the request into a new type 'T.
/// - `next`: The next `HttpHandler` to invoke, which takes the mapped 'T as input.
let mapFormOptions
    (maxSize : int64)
    (map : FormData -> 'T)
    (next : 'T -> HttpHandler) : HttpHandler = fun ctx ->
    task {
        let! form = getFormOptions maxSize ctx
        return! next (map form) ctx
    }

/// Projects form dta onto 'T and provides to next HttpHandler.
///
/// Performs CSRF validation for HTTP POST, PUT, PATCH, DELETE verbs, if antiforgery
/// services are registered and a token is provided in the request.
///
/// Automatically detects if request is content-type: multipart/form-data, and
/// if so, will enable streaming.
///
/// Uses `getForm` with a default maximum body size of `Multipart.DefaultMaxSize` for consistency
/// with multipart form data processing, but can be overridden by using `mapFormOptions` directly.
///
/// - `map`: A function that maps the form data from the request into a new type 'T.
/// - `next`: The next `HttpHandler` to invoke, which takes the mapped 'T as input.
let mapForm
    (map : FormData -> 'T)
    (next : 'T -> HttpHandler) : HttpHandler = fun ctx ->
    task {
        let! form = getForm ctx
        return! next (map form) ctx
    }

/// Validates the CSRF of the current request.
///
/// - `handleOk`: The `HttpHandler` to invoke if the CSRF token is valid or if validation is not applicable.
/// - `handleInvalidToken`: The `HttpHandler` to invoke if the CSRF token
let validateCsrfToken
    (handleOk : HttpHandler)
    (handleInvalidToken : HttpHandler) : HttpHandler = fun ctx ->
    task {
        let! isValid = Xsrf.validateToken ctx

        let respondWith =
            match isValid with
            | true  -> handleOk
            | false -> handleInvalidToken

        return! respondWith ctx
    }

/// Projects JSON using custom JsonSerializerOptions onto 'T and provides to next
/// `HttpHandler`, throws `JsonException` if errors occur during deserialization.
/// If the body is empty or not JSON, returns the default value of 'T.
///
/// - `options`: The `JsonSerializerOptions` to use during deserialization.
/// - `next`: The next `HttpHandler` to invoke, which takes the mapped 'T as input.
let mapJsonOptions<'T>
    (options : JsonSerializerOptions)
    (next : 'T -> HttpHandler) : HttpHandler = fun ctx ->
    task {
        let! json = getJsonOptions options ctx
        return! next json ctx
    }

/// Projects JSON onto 'T and provides to next `HttpHandler`, throws `JsonException`
/// if errors occur during deserialization. If the body is empty or not JSON, returns
/// the default value of 'T.
///
/// Uses `getJson` with default `JsonSerializerOptions`, but can be overridden by using `mapJsonOptions` directly.
///
/// - `next`: The next `HttpHandler` to invoke, which takes the mapped 'T as input.
let mapJson<'T>
    (next : 'T -> HttpHandler) : HttpHandler =
    mapJsonOptions<'T> defaultJsonOptions next

// ------------
// Authentication
// ------------

/// Attempts to authenticate the current request using the provided scheme and
/// passes AuthenticateResult into next HttpHandler. Does not modify the
/// `HttpContext.User` or authentication status, and does not challenge or forbid on its own.
///
/// - `authScheme`: The authentication scheme to use when authenticating the request. This should match the scheme used in your authentication configuration.
/// - `next`: The next `HttpHandler` to invoke, which takes the `AuthenticateResult` as input.
let authenticate
    (authScheme : string)
    (next : AuthenticateResult -> HttpHandler) : HttpHandler = fun ctx ->
    task {
        let! authenticateResult = ctx.AuthenticateAsync authScheme
        return! next authenticateResult ctx
    }

/// Authenticate the current request using the default authentication scheme. Proceeds
/// if the authentication status of current `IPrincipal` is true.
///
/// Note: The default authentication scheme can be configured using
/// `Microsoft.AspNetCore.Authentication.AuthenticationOptions.DefaultAuthenticateScheme.`
///
/// - `authScheme`: The authentication scheme to use when authenticating the request. This should match the scheme used in your authentication configuration.
/// - `handleOk`: The `HttpHandler` to invoke if the user is authenticated. If the user is not authenticated, a 403 Forbidden response will be returned.
let ifAuthenticated
    (authScheme : string)
    (handleOk : HttpHandler) : HttpHandler =
    authenticate authScheme (fun authenticateResult ctx ->
        if authenticateResult.Succeeded then
            handleOk ctx
        else
            ctx.ForbidAsync())

/// Proceeds if the authentication status of current `IPrincipal` is true and
/// they exist in a list of roles.
///
/// The roles are checked using `ClaimsPrincipal.IsInRole`, so the role claim type
/// is determined by the authentication handler in use. For example, with JWT Bearer
/// authentication, the role claim type is typically "roles" or "role", but with
/// cookie authentication it may be different depending on how claims are set up.
///
/// Note: This function assumes that the authentication handler populates the user's
/// claims with their roles in a way that `ClaimsPrincipal.IsInRole` can check. Make
/// sure your authentication setup is configured accordingly for role-based authorization
/// to work with this function.
///
/// - `authScheme`: The authentication scheme to use when authenticating the request. This should match the scheme used in your authentication configuration.
/// - `roles`: A sequence of roles to check against the authenticated user's claims. If the user is in any of the specified roles, they will be allowed to proceed.
/// - `handleOk`: The `HttpHandler` to invoke if the user is authenticated and in one of the specified roles. If the user is not authenticated or not in any of the roles, a 403 Forbidden response will be returned.
let ifAuthenticatedInRole
    (authScheme : string)
    (roles : string seq)
    (handleOk : HttpHandler) : HttpHandler =
    authenticate authScheme (fun authenticateResult ctx ->
        let isInRole = Seq.exists authenticateResult.Principal.IsInRole roles
        match authenticateResult.Succeeded, isInRole with
        | true, true ->
            handleOk ctx
        | _ ->
            ctx.ForbidAsync())

/// Proceeds if the authentication status of current IPrincipal is true and has
/// a specific scope.
///
/// The scope is checked by looking for a claim of type "scope" with the specified
/// value and issuer. This is commonly used in token-based authentication scenarios,
/// such as with JWTs, where scopes are included as claims in the token. The issuer
/// is also checked to ensure that the scope claim is coming from the expected authority.
///
/// Note: This function assumes that the authentication handler populates the user's
/// claims with their scopes in a claim of type "scope". Make sure your authentication
/// setup is configured accordingly for scope-based authorization to work with this function.
///
/// - `authScheme`: The authentication scheme to use when authenticating the request. This should match the scheme used in your authentication configuration.
/// - `issuer`: The expected issuer of the scope claim to check against. This should match the authority that issues the tokens containing the scope claims.
/// - `scope`: The specific scope value to check for in the user's claims. The user must have a claim of type "scope" with this value (and the correct issuer) to be allowed to proceed.
/// - `handleOk`: The `HttpHandler` to invoke if the user is authenticated and has the specified scope. If the user is not authenticated or does not have the required scope, a 403 Forbidden response will be returned.
let ifAuthenticatedWithScope
    (authScheme : string)
    (issuer : string)
    (scope : string)
    (handleOk : HttpHandler) : HttpHandler =
    authenticate authScheme (fun authenticateResult ctx ->
        if authenticateResult.Succeeded then
            let hasScope =
                let predicate (claim : Claim) = strEquals claim.Issuer issuer && strEquals claim.Type "scope"
                match Seq.tryFind predicate authenticateResult.Principal.Claims with
                | Some claim -> Array.contains scope (strSplit [|' '|] claim.Value)
                | None -> false
            if hasScope then
                handleOk ctx
            else
                ctx.ForbidAsync()
        else
            ctx.ForbidAsync())

/// Proceeds if the authentication status of current IPrincipal is false.
///
/// This can be used to allow access to certain handlers only for unauthenticated
/// users, such as a login or registration page. If the user is authenticated, a
/// 403 Forbidden response will be returned.
///
/// Note: This function checks if the authentication attempt succeeded, which means
/// the user is authenticated. If the authentication attempt did not succeed
/// (i.e., the user is not authenticated), it allows access to the specified handler.
/// Make sure your authentication configuration is set up correctly for this to
/// work as intended.
///
/// - `authScheme`: The authentication scheme to use when authenticating the request. This should match the scheme used in your authentication configuration.
/// - `handleOk`: The `HttpHandler` to invoke if the user is not authenticated. If the user is authenticated, a 403 Forbidden response will be returned.
let ifNotAuthenticated
    (authScheme : string)
    (handleOk : HttpHandler) : HttpHandler =
    authenticate authScheme (fun authenticateResult ctx ->
        if authenticateResult.Succeeded then
            ctx.ForbidAsync()
        else
            handleOk ctx)
