[<RequireQualifiedAccess>]
module Falco.Request

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

/// Obtains the HttpVerb of the request
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

/// Streams the request body into a string.
let getBodyString (ctx : HttpContext) : Task<string> =
    task {
        use reader = new StreamReader(ctx.Request.Body, Encoding.UTF8)
        return! reader.ReadToEndAsync()
    }

/// Retrieves the cookie from the request.
let getCookies (ctx : HttpContext) : RequestData =
    RequestValue.parseCookies ctx.Request.Cookies
    |> RequestData

/// Retrieves the headers from the request.
let getHeaders (ctx : HttpContext) : RequestData  =
    RequestValue.parseHeaders ctx.Request.Headers
    |> RequestData

/// Retrieves all route values from the request, including query string.
let getRoute (ctx : HttpContext) : RequestData =
    RequestValue.parseRoute (ctx.Request.RouteValues, ctx.Request.Query)
    |> RequestData

/// Retrieves the query string and route values from the request.
let getQuery (ctx : HttpContext) : RequestData =
    RequestValue.parseQuery ctx.Request.Query
    |> RequestData

/// Retrieves the form collection and route values from the request.
///
/// Automatically detects if request is multipart/form-data, and will enable
/// streaming.
let getForm (ctx : HttpContext) : Task<FormData> =
    task {
        use tokenSource = new CancellationTokenSource()

        let! form =
            if ctx.Request.IsMultipart() then
                ctx.Request.StreamFormAsync(tokenSource.Token)
            else
                ctx.Request.ReadFormAsync(tokenSource.Token)

        let files = if isNull(form.Files) then None else Some form.Files

        let requestValue = RequestValue.parseForm (form, Some ctx.Request.RouteValues)

        return FormData(requestValue, files)
    }

/// Retrieves the form collection from the request, if the CSRF token is valid,
/// otherwise returns None.
///
/// Automatically detects if request is multipart/form-data, and will enable
/// streaming.
let getFormSecure (ctx : HttpContext) : Task<FormData option> =
    task {
        let! isAuth = Xsrf.validateToken ctx
        if isAuth then
            let! form = getForm ctx
            return Some form
        else
            return None
    }

/// Attempts to bind request body using System.Text.Json and provided
/// JsonSerializerOptions.
let getJsonOptions<'T>
    (options : JsonSerializerOptions)
    (ctx : HttpContext) : Task<'T> = task {

        if ctx.Request.Body.CanSeek && ctx.Request.Body.Length = 0L then
            return JsonSerializer.Deserialize<'T>("{}", options)
        else
            use tokenSource = new CancellationTokenSource()
            let! json = JsonSerializer.DeserializeAsync<'T>(ctx.Request.Body, options, tokenSource.Token).AsTask()
            return json
    }


let internal defaultJsonOptions =
    let options = JsonSerializerOptions()
    options.AllowTrailingCommas <- true
    options.PropertyNameCaseInsensitive <- true
    options

/// Attempts to bind request body using System.Text.Json and default
/// JsonSerializerOptions.
let getJson<'T> (ctx : HttpContext) =
    getJsonOptions<'T> defaultJsonOptions ctx

// ------------
// Handlers
// ------------

/// Buffers the current HttpRequest body into a
/// string and provides to next HttpHandler.
let bodyString
    (next : string -> HttpHandler) : HttpHandler = fun ctx ->
    task {
        let! body = getBodyString ctx
        return! next body ctx
    }

/// Projects cookie values onto 'T and provides
/// to next HttpHandler.
let mapCookies
    (map : RequestData -> 'T)
    (next : 'T -> HttpHandler) : HttpHandler = fun ctx ->
    getCookies ctx
    |> map
    |> fun route -> next route ctx

/// Projects header values onto 'T and provides
/// to next HttpHandler.
let mapHeaders
    (map : RequestData -> 'T)
    (next : 'T -> HttpHandler) : HttpHandler = fun ctx ->
    getHeaders ctx
    |> map
    |> fun route -> next route ctx

/// Projects route values onto 'T and provides
/// to next HttpHandler.
let mapRoute
    (map : RequestData -> 'T)
    (next : 'T -> HttpHandler) : HttpHandler = fun ctx ->
    getRoute ctx
    |> map
    |> fun route -> next route ctx

/// Projects query string onto 'T and provides
/// to next HttpHandler.
let mapQuery
    (map : RequestData -> 'T)
    (next : 'T -> HttpHandler) : HttpHandler = fun ctx ->
    getQuery ctx
    |> map
    |> fun query -> next query ctx


/// Projects form dta onto 'T and provides to next HttpHandler.
///
/// Automatically detects if request is content-type: multipart/form-data, and
/// if so, will enable streaming.
let mapForm
    (map : FormData -> 'T)
    (next : 'T -> HttpHandler) : HttpHandler = fun ctx ->
    task {
        let! form = getForm ctx
        return! next (map form) ctx
    }

/// Validates the CSRF of the current request.
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

/// Projects form data onto 'T and provides
/// to next HttpHandler.
///
/// Automatically detects if request is multipart/form-data, and will enable
/// streaming.
let mapFormSecure
    (map : FormData -> 'T)
    (next : 'T -> HttpHandler)
    (handleInvalidToken : HttpHandler) : HttpHandler = fun ctx ->
    task {
        let! form = getFormSecure ctx

        let respondWith =
            match form with
            | Some form ->
                next (map form)
            | None ->
                handleInvalidToken

        return! respondWith ctx
    }

/// Projects JSON using custom JsonSerializerOptions
/// onto 'T and provides to next HttpHandler, throws
/// JsonException if errors occur during deserialization.
let mapJsonOptions<'T>
    (options : JsonSerializerOptions)
    (next : 'T -> HttpHandler) : HttpHandler = fun ctx ->
    task {
        let! json = getJsonOptions options ctx
        return! next json ctx
    }

/// Projects JSON onto 'T and provides to next
/// HttpHandler, throws JsonException if errors
/// occur during deserialization.
let mapJson<'T>
    (next : 'T -> HttpHandler) : HttpHandler =
    mapJsonOptions<'T> defaultJsonOptions next


// ------------
// Authentication
// ------------

/// Attempts to authenticate the current request using the provided
/// scheme and passes AuthenticateResult into next HttpHandler.
let authenticate
    (authScheme : string)
    (next : AuthenticateResult -> HttpHandler) : HttpHandler = fun ctx ->
    task {
        let! authenticateResult = ctx.AuthenticateAsync(authScheme)
        return! next authenticateResult ctx
    }

/// Authenticate the current request using the default authentication scheme.
///
/// Proceeds if the authentication status of current `IPrincipal` is true.
///
/// The default authentication scheme can be configured using
/// `Microsoft.AspNetCore.Authentication.AuthenticationOptions.DefaultAuthenticateScheme.`
let ifAuthenticated
    (authScheme : string)
    (handleOk : HttpHandler) : HttpHandler =
    authenticate authScheme (fun authenticateResult ctx ->
        if authenticateResult.Succeeded then
            handleOk ctx
        else
            ctx.ForbidAsync())

/// Proceeds if the authentication status of current IPrincipal is true
/// and they exist in a list of roles.
let ifAuthenticatedInRole
    (authScheme : string)
    (roles : string list)
    (handleOk : HttpHandler) : HttpHandler =
    authenticate authScheme (fun authenticateResult ctx ->
        let isInRole = List.exists authenticateResult.Principal.IsInRole roles
        match authenticateResult.Succeeded, isInRole with
        | true, true ->
            handleOk ctx
        | _ ->
            ctx.ForbidAsync())

/// Proceeds if the authentication status of current IPrincipal is true
/// and has a specific scope.
let ifAuthenticatedWithScope
    (authScheme : string)
    (issuer : string)
    (scope : string)
    (handleOk : HttpHandler) : HttpHandler =
    authenticate authScheme (fun authenticateResult ctx ->
        if authenticateResult.Succeeded then
            let hasScope =
                let predicate (claim : Claim) = (strEquals claim.Issuer issuer) && (strEquals claim.Type "scope")
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
let ifNotAuthenticated
    (authScheme : string)
    (handleOk : HttpHandler) : HttpHandler =
    authenticate authScheme (fun authenticateResult ctx ->
        if authenticateResult.Succeeded then
            ctx.ForbidAsync()
        else
            handleOk ctx)
