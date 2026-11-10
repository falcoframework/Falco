namespace Falco

open System
open System.Threading.Tasks
open Microsoft.AspNetCore.Http

/// The eventual return of asynchronous HttpContext processing.
type HttpHandler = HttpContext -> Task

module HttpHandler =
    /// Convert HttpHandler to a RequestDelegate.
    let toRequestDelegate (handler : HttpHandler) =
        new RequestDelegate(handler)

/// Modifies an HttpContext and returns it for further processing.
type HttpResponseModifier = HttpContext -> HttpContext

/// An HTTP method supported by endpoint routing.
type HttpVerb =
    /// The HTTP GET method.
    | GET
    /// The HTTP HEAD method.
    | HEAD
    /// The HTTP POST method.
    | POST
    /// The HTTP PUT method.
    | PUT
    /// The HTTP PATCH method.
    | PATCH
    /// The HTTP DELETE method.
    | DELETE
    /// The HTTP OPTIONS method.
    | OPTIONS
    /// The HTTP TRACE method.
    | TRACE
    /// Any HTTP method.
    | ANY

    override x.ToString() =
        match x with
        | GET     -> HttpMethods.Get
        | HEAD    -> HttpMethods.Head
        | POST    -> HttpMethods.Post
        | PUT     -> HttpMethods.Put
        | PATCH   -> HttpMethods.Patch
        | DELETE  -> HttpMethods.Delete
        | OPTIONS -> HttpMethods.Options
        | TRACE   -> HttpMethods.Trace
        | ANY     -> String.Empty
