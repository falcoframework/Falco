namespace Falco

open System
open System.Collections.Generic
open Microsoft.AspNetCore.Builder
open Microsoft.AspNetCore.Http
open Microsoft.AspNetCore.Routing
open Microsoft.Extensions.FileProviders
open Falco.StringUtils

/// Specifies an association of a route pattern to a collection of
/// HttpEndpointHandler.
type HttpEndpoint =
    { Pattern  : string
      Handlers : (HttpVerb * HttpHandler) seq
      Configure : EndpointBuilder -> EndpointBuilder }

module Routing =
    /// Constructor for multi-method HttpEndpoint.
    ///
    /// - `pattern` - The route pattern to which the HttpEndpoint will be associated.
    /// - `handlers` - A sequence of tuples associating an HttpVerb to an HttpHandler. The HttpVerb ANY can be used to match any HTTP method.
    let all
        (pattern : string)
        (handlers : (HttpVerb * HttpHandler) seq) : HttpEndpoint =
        { Pattern  = pattern
          Handlers = handlers
          Configure = id }

    /// Constructor for a singular HttpEndpoint.
    ///
    /// - `verb` - The HttpVerb to which the HttpHandler will be associated. The HttpVerb ANY can be used to match any HTTP method.
    /// - `pattern` - The route pattern to which the HttpEndpoint will be associated.
    /// - `handler` - The HttpHandler to be associated with the provided HttpVerb and route pattern.
    let route verb pattern handler =
        all pattern [ verb, handler ]

    /// HttpEndpoint constructor that matches any HttpVerb.
    ///
    /// Note: Use with caution as this will match any HTTP method, which may not be desirable in all cases.
    ///
    /// - `pattern` - The route pattern to which the HttpHandler will be associated. The HttpVerb ANY can be used to match any HTTP method.
    /// - `handler` - The HttpHandler to be associated with the provided route pattern for any HTTP method.
    let any pattern handler =
        route ANY pattern handler

    /// GET HttpEndpoint constructor.
    ///
    /// - `pattern` - The route pattern to which the HttpHandler will be associated.
    /// - `handler` - The HttpHandler to be associated with the GET HttpVerb and provided route pattern.
    let get pattern handler =
        route GET pattern handler

    /// HEAD HttpEndpoint constructor.
    ///
    /// - `pattern` - The route pattern to which the HttpHandler will be associated.
    /// - `handler` - The HttpHandler to be associated with the HEAD HttpVerb and provided route pattern.
    let head pattern handler =
        route HEAD pattern handler

    /// POST HttpEndpoint constructor.
    ///
    /// - `pattern` - The route pattern to which the HttpHandler will be associated.
    /// - `handler` - The HttpHandler to be associated with the POST HttpVerb and provided route pattern.
    let post pattern handler =
        route POST pattern handler

    /// PUT HttpEndpoint constructor.
    ///
    /// - `pattern` - The route pattern to which the HttpHandler will be associated.
    /// - `handler` - The HttpHandler to be associated with the PUT HttpVerb and
    let put pattern handler =
        route PUT pattern handler

    /// PATCH HttpEndpoint constructor.
    ///
    /// - `pattern` - The route pattern to which the HttpHandler will be associated.
    /// - `handler` - The HttpHandler to be associated with the PATCH HttpVerb and
    let patch pattern handler =
        route PATCH pattern handler

    /// DELETE HttpEndpoint constructor.
    ///
    /// - `pattern` - The route pattern to which the HttpHandler will be associated.
    /// - `handler` - The HttpHandler to be associated with the DELETE HttpVerb and provided route pattern.
    let delete pattern handler =
        route DELETE pattern handler

    /// OPTIONS HttpEndpoint constructor.
    ///
    /// - `pattern` - The route pattern to which the HttpHandler will be associated.
    /// - `handler` - The HttpHandler to be associated with the OPTIONS HttpVerb and provided route pattern.
    let options pattern handler =
        route OPTIONS pattern handler

    /// TRACE HttpEndpoint construct.
    ///
    /// - `pattern` - The route pattern to which the HttpHandler will be associated.
    /// - `handler` - The HttpHandler to be associated with the TRACE HttpVerb and provided route pattern.
    let trace pattern handler =
        route TRACE pattern handler

    /// HttpEndpoint constructor that matches any HttpVerb which maps the route
    /// using the provided `map` function.
    ///
    /// Note: Use with caution as this will match any HTTP method, which may not be desirable in all cases.
    ///
    /// - `pattern` - The route pattern to which the HttpHandler will be associated. The HttpVerb ANY can be used to match any HTTP method.
    /// - `map` - A function that takes the route pattern and returns a mapped value which will be included in the HttpContext for the HttpHandler to use.
    /// - `handler` - The HttpHandler to be associated with the provided route pattern for any HTTP method, which will receive the mapped value in the HttpContext.
    let mapAny pattern map handler =
        any pattern (Request.mapRoute map handler)

    /// GET HttpEndpoint constructor which maps the route using the provided
    /// `map` function.
    ///
    /// - `pattern` - The route pattern to which the HttpHandler will be associated.
    /// - `map` - A function that takes the route pattern and returns a mapped value which will be included in the HttpContext for the HttpHandler to use.
    /// - `handler` - The HttpHandler to be associated with the GET HttpVerb and provided route pattern, which will receive the mapped value in the HttpContext.
    let mapGet pattern map handler =
        get pattern (Request.mapRoute map handler)

    /// HEAD HttpEndpoint constructor which maps the route using the provided
    /// `map` function.
    ///
    /// - `pattern` - The route pattern to which the HttpHandler will be associated.
    /// - `map` - A function that takes the route pattern and returns a mapped value which will be included in the HttpContext for the HttpHandler to use.
    /// - `handler` - The HttpHandler to be associated with the HEAD HttpVerb and provided route pattern, which will receive the mapped value in the HttpContext.
    let mapHead pattern map handler =
        head pattern (Request.mapRoute map handler)

    /// POST HttpEndpoint constructor which maps the route using the provided
    /// `map` function.
    ///
    /// - `pattern` - The route pattern to which the HttpHandler will be associated.
    /// - `map` - A function that takes the route pattern and returns a mapped value which will be included in the HttpContext for the HttpHandler to use.
    /// - `handler` - The HttpHandler to be associated with the POST HttpVerb and provided route pattern, which will receive the mapped value in the HttpContext.
    let mapPost pattern map handler =
        post pattern (Request.mapRoute map handler)

    /// PUT HttpEndpoint constructor which maps the route using the provided
    /// `map` function.
    ///
    /// - `pattern` - The route pattern to which the HttpHandler will be associated.
    /// - `map` - A function that takes the route pattern and returns a mapped value which will be included in the HttpContext for the HttpHandler to use.
    /// - `handler` - The HttpHandler to be associated with the PUT HttpVerb and provided route pattern, which will receive the mapped value in the HttpContext.
    let mapPut pattern map handler =
        put pattern (Request.mapRoute map handler)

    /// PATCH HttpEndpoint constructor which maps the route using the provided
    /// `map` function.
    ///
    /// - `pattern` - The route pattern to which the HttpHandler will be associated.
    /// - `map` - A function that takes the route pattern and returns a mapped value which will be included in the HttpContext for the HttpHandler to use.
    /// - `handler` - The HttpHandler to be associated with the PATCH HttpVerb and provided route pattern, which will receive the mapped value in the HttpContext.
    let mapPatch pattern map handler =
        patch pattern (Request.mapRoute map handler)

    /// DELETE HttpEndpoint constructor which maps the route using the provided
    /// `map` function.
    ///
    /// - `pattern` - The route pattern to which the HttpHandler will be associated.
    /// - `map` - A function that takes the route pattern and returns a mapped value which will be included in the HttpContext for the HttpHandler to use.
    /// - `handler` - The HttpHandler to be associated with the DELETE HttpVerb and provided route pattern, which will receive the mapped value in the HttpContext.
    let mapDelete pattern map handler =
        delete pattern (Request.mapRoute map handler)

    /// OPTIONS HttpEndpoint constructor which maps the route using the provided
    /// `map` function.
    ///
    /// - `pattern` - The route pattern to which the HttpHandler will be associated.
    /// - `map` - A function that takes the route pattern and returns a mapped value which will be included in the HttpContext for the HttpHandler to use.
    /// - `handler` - The HttpHandler to be associated with the OPTIONS HttpVerb and provided route pattern, which will receive the mapped value in the HttpContext.
    let mapOptions pattern map handler =
        options pattern (Request.mapRoute map handler)

    /// TRACE HttpEndpoint construct which maps the route using the provided
    /// `map` function.
    ///
    /// - `pattern` - The route pattern to which the HttpHandler will be associated.
    /// - `map` - A function that takes the route pattern and returns a mapped value which will be included in the HttpContext for the HttpHandler to use.
    /// - `handler` - The HttpHandler to be associated with the TRACE HttpVerb and provided route pattern, which will receive the mapped value in the HttpContext.
    let mapTrace pattern map handler =
        trace pattern (Request.mapRoute map handler)

    /// Configure the display name attribute of the endpoint.
    ///
    /// Note: The display name is used for endpoint selection and will be included
    /// in the HttpContext for the HttpHandler to use.
    ///
    /// - `displayName` - The display name to be associated with the endpoint, which will be included in the HttpContext for the HttpHandler to use.
    /// - `endpoint` - The HttpEndpoint for which the display name will be set.
    let setDisplayName (displayName : string) (endpoint : HttpEndpoint) =
        let configure (builder : EndpointBuilder) =
            (builder :?> RouteEndpointBuilder).DisplayName <- displayName
            builder

        { endpoint with Configure = endpoint.Configure >> configure }

    /// Set an explicit order for the endpoint.
    ///
    /// Note: The order is used for endpoint selection and will be included in the
    /// HttpContext for the HttpHandler to use. Endpoints with lower order values
    /// will be selected before those with higher values.
    ///
    /// - `n` - The order to be associated with the endpoint, which will be included in the HttpContext for the HttpHandler to use.
    /// - `endpoint` - The HttpEndpoint for which the order will be set.
    let setOrder (n : int) (endpoint : HttpEndpoint) =
        let configure (builder : EndpointBuilder) =
            (builder :?> RouteEndpointBuilder).Order <- n
            builder

        { endpoint with Configure = endpoint.Configure >> configure }

[<Sealed>]
type FalcoEndpointDataSource(httpEndpoints : HttpEndpoint seq) =
    inherit EndpointDataSource()

    let conventions = List<Action<EndpointBuilder>>()

    new() = FalcoEndpointDataSource([])

    member val FalcoEndpoints = List<HttpEndpoint>()

    override x.Endpoints with get() = x.BuildEndpoints()

    override _.GetChangeToken() = NullChangeToken.Singleton

    member private this.BuildEndpoints () =
        let endpoints = List<Endpoint>()

        for endpoint in Seq.concat [ httpEndpoints; this.FalcoEndpoints ] do
            let routePattern = Patterns.RoutePatternFactory.Parse endpoint.Pattern

            for (verb, handler) in endpoint.Handlers do
                let verbStr = verb.ToString()

                let displayName =
                    if strEmpty verbStr then endpoint.Pattern
                    else strConcat [|verbStr; " "; endpoint.Pattern|]

                let endpointBuilder = RouteEndpointBuilder(
                    requestDelegate = HttpHandler.toRequestDelegate handler,
                    routePattern = routePattern,
                    order = 0,
                    DisplayName = displayName)

                endpointBuilder.DisplayName <- displayName
                endpoint.Configure endpointBuilder |> ignore

                for convention in conventions do
                    convention.Invoke(endpointBuilder)

                let routeNameMetadata = RouteNameMetadata(endpoint.Pattern)
                endpointBuilder.Metadata.Add(routeNameMetadata)

                let httpMethodMetadata =
                    match verb with
                    | ANY -> HttpMethodMetadata [||]
                    | _   -> HttpMethodMetadata [|verbStr|]

                endpointBuilder.Metadata.Add(httpMethodMetadata)

                endpoints.Add(endpointBuilder.Build())

        endpoints

    interface IEndpointConventionBuilder with
        member _.Add(convention: Action<EndpointBuilder>) : unit =
            conventions.Add(convention)

        member _.Finally (_: Action<EndpointBuilder>): unit =
            ()
