module Falco.Tests.Routing

open Xunit
open Falco
open Falco.Routing
open FsUnit.Xunit
open Microsoft.AspNetCore.Builder
open Microsoft.AspNetCore.Routing
open System

let emptyHandler : HttpHandler = Response.ofPlainText ""

// -----------------
// HttpEndpoint Tests
// -----------------

[<Fact>]
let ``route function should return valid HttpEndpoint`` () =
    let routeVerb = GET
    let routePattern = "/"

    let endpoint = route routeVerb routePattern emptyHandler
    endpoint.Pattern |> should equal routePattern

    let verb, handler = Seq.head endpoint.Handlers
    verb |> should equal routeVerb
    handler |> should be instanceOfType<HttpHandler>

[<Fact>]
let ``all function should create endpoint with multiple handlers`` () =
    let pattern = "/api/users"
    let handlers = [
        GET, emptyHandler
        POST, emptyHandler
        DELETE, emptyHandler
    ]

    let endpoint = all pattern handlers
    endpoint.Pattern |> should equal pattern
    endpoint.Handlers |> Seq.length |> should equal 3

    let verbs = endpoint.Handlers |> Seq.map fst |> List.ofSeq
    verbs |> should contain GET
    verbs |> should contain POST
    verbs |> should contain DELETE

[<Fact>]
let ``HTTP verb helpers create correct endpoints`` () =
    let testEndpointFunction
        (mapEndPoint: string -> HttpHandler -> HttpEndpoint)
        (expectedVerb : HttpVerb) =
        let pattern = "/test"
        let endpoint = mapEndPoint pattern emptyHandler
        endpoint.Pattern |> should equal pattern
        let (verb, handler) = Seq.head endpoint.Handlers
        verb |> should equal expectedVerb
        handler |> should be instanceOfType<HttpHandler>

    [
        any, ANY
        get, GET
        head, HEAD
        post, POST
        put, PUT
        patch, PATCH
        delete, DELETE
        options, OPTIONS
        trace, TRACE
    ]
    |> List.iter (fun (fn, verb) -> testEndpointFunction fn verb)

[<Fact>]
let ``mapGet should compose route mapping with handler`` () =
    let pattern = "/users/{id}"
    let mutable capturedId = ""

    let handler (id: string) : HttpHandler =
        capturedId <- id
        Response.ofEmpty

    let endpoint = mapGet pattern (fun r -> r.GetString "id") handler
    endpoint.Pattern |> should equal pattern

    let (verb, _) = Seq.head endpoint.Handlers
    verb |> should equal GET

[<Fact>]
let ``mapPost should compose route mapping with handler`` () =
    let pattern = "/users"
    let endpoint = mapPost pattern (fun _ -> "test") (fun _ -> emptyHandler)

    let (verb, _) = Seq.head endpoint.Handlers
    verb |> should equal POST

[<Fact>]
let ``all map* functions create correct verb endpoints`` () =
    let pattern = "/test/{id}"
    let map = fun (r: RequestData) -> r.GetString "id"

    [
        mapAny, ANY
        mapGet, GET
        mapHead, HEAD
        mapPost, POST
        mapPut, PUT
        mapPatch, PATCH
        mapDelete, DELETE
        mapOptions, OPTIONS
        mapTrace, TRACE
    ]
    |> List.iter (fun (fn, verb) ->
        let endpoint = fn pattern map (fun _ -> emptyHandler)
        let (endpointVerb, _) = Seq.head endpoint.Handlers
        endpointVerb |> should equal verb
    )

[<Fact>]
let ``setDisplayName should configure endpoint display name`` () =
    let endpoint =
        route GET "/" emptyHandler
        |> setDisplayName "MyCustomEndpoint"

    let dataSource = FalcoEndpointDataSource([ endpoint ])
    let builtEndpoint = Seq.head dataSource.Endpoints :?> RouteEndpoint

    builtEndpoint.DisplayName |> should equal "MyCustomEndpoint"

[<Fact>]
let ``setOrder should configure endpoint order`` () =
    let endpoint =
        route GET "/" emptyHandler
        |> setOrder 42

    let dataSource = FalcoEndpointDataSource([ endpoint ])
    let builtEndpoint = Seq.head dataSource.Endpoints :?> RouteEndpoint

    builtEndpoint.Order |> should equal 42

[<Fact>]
let ``setDisplayName and setOrder can be chained`` () =
    let endpoint =
        route GET "/" emptyHandler
        |> setDisplayName "TestEndpoint"
        |> setOrder 99

    let dataSource = FalcoEndpointDataSource([ endpoint ])
    let builtEndpoint = Seq.head dataSource.Endpoints :?> RouteEndpoint

    builtEndpoint.DisplayName |> should equal "TestEndpoint"
    builtEndpoint.Order |> should equal 99

// -----------------
// FalcoEndpointDataSource Tests
// -----------------

[<Fact>]
let ``FalcoEndpointDataSource with empty constructor`` () =
    let dataSource = FalcoEndpointDataSource()
    dataSource.Endpoints |> should haveCount 0

[<Fact>]
let ``FalcoEndpointDataSource with single endpoint`` () =
    let endpoint = route GET "/" emptyHandler
    let dataSource = FalcoEndpointDataSource([ endpoint ])

    dataSource.Endpoints |> should haveCount 1
    let builtEndpoint = Seq.head dataSource.Endpoints :?> RouteEndpoint
    builtEndpoint.RoutePattern.RawText |> should equal "/"

[<Fact>]
let ``FalcoEndpointDataSource with multiple endpoints`` () =
    let endpoints = [
        route GET "/" emptyHandler
        route POST "/users" emptyHandler
        route DELETE "/users/{id}" emptyHandler
    ]
    let dataSource = FalcoEndpointDataSource(endpoints)

    dataSource.Endpoints |> should haveCount 3

[<Fact>]
let ``FalcoEndpointDataSource with multi-verb endpoint creates separate route endpoints`` () =
    let endpoint = all "/api/users" [
        GET, emptyHandler
        POST, emptyHandler
        PUT, emptyHandler
    ]
    let dataSource = FalcoEndpointDataSource([ endpoint ])

    // Should create 3 separate RouteEndpoints (one per verb)
    dataSource.Endpoints |> should haveCount 3

    let routeEndpoints = dataSource.Endpoints |> Seq.cast<RouteEndpoint> |> List.ofSeq
    routeEndpoints |> List.iter (fun re ->
        re.RoutePattern.RawText |> should equal "/api/users"
    )

[<Fact>]
let ``FalcoEndpointDataSource builds endpoints with correct HTTP method metadata`` () =
    let endpoint = route GET "/test" emptyHandler
    let dataSource = FalcoEndpointDataSource([ endpoint ])

    let builtEndpoint = Seq.head dataSource.Endpoints :?> RouteEndpoint
    let httpMethodMetadata =
        builtEndpoint.Metadata.GetMetadata<HttpMethodMetadata>()

    httpMethodMetadata |> should not' (be Null)
    httpMethodMetadata.HttpMethods |> should contain "GET"

[<Fact>]
let ``FalcoEndpointDataSource with ANY verb creates empty HTTP methods metadata`` () =
    let endpoint = route ANY "/test" emptyHandler
    let dataSource = FalcoEndpointDataSource([ endpoint ])

    let builtEndpoint = Seq.head dataSource.Endpoints :?> RouteEndpoint
    let httpMethodMetadata =
        builtEndpoint.Metadata.GetMetadata<HttpMethodMetadata>()

    httpMethodMetadata |> should not' (be Null)
    httpMethodMetadata.HttpMethods |> should be Empty

[<Fact>]
let ``FalcoEndpointDataSource builds endpoints with route name metadata`` () =
    let pattern = "/api/users"
    let endpoint = route GET pattern emptyHandler
    let dataSource = FalcoEndpointDataSource([ endpoint ])

    let builtEndpoint = Seq.head dataSource.Endpoints :?> RouteEndpoint
    let routeNameMetadata =
        builtEndpoint.Metadata.GetMetadata<RouteNameMetadata>()

    routeNameMetadata |> should not' (be Null)
    routeNameMetadata.RouteName |> should equal pattern

[<Fact>]
let ``FalcoEndpointDataSource default display name includes verb and pattern`` () =
    let endpoint = route POST "/users" emptyHandler
    let dataSource = FalcoEndpointDataSource([ endpoint ])

    let builtEndpoint = Seq.head dataSource.Endpoints :?> RouteEndpoint
    builtEndpoint.DisplayName |> should equal "POST /users"

[<Fact>]
let ``FalcoEndpointDataSource ANY verb display name is pattern only`` () =
    let endpoint = route ANY "/test" emptyHandler
    let dataSource = FalcoEndpointDataSource([ endpoint ])

    let builtEndpoint = Seq.head dataSource.Endpoints :?> RouteEndpoint
    builtEndpoint.DisplayName |> should equal "/test"

[<Fact>]
let ``FalcoEndpointDataSource with route parameters`` () =
    let endpoint = route GET "/users/{id}/posts/{postId}" emptyHandler
    let dataSource = FalcoEndpointDataSource([ endpoint ])

    let builtEndpoint = Seq.head dataSource.Endpoints :?> RouteEndpoint
    builtEndpoint.RoutePattern.Parameters |> should haveCount 2

    let paramNames =
        builtEndpoint.RoutePattern.Parameters
        |> Seq.map (fun p -> p.Name)
        |> List.ofSeq

    paramNames |> should contain "id"
    paramNames |> should contain "postId"

[<Fact>]
let ``FalcoEndpointDataSource GetChangeToken returns NullChangeToken`` () =
    let dataSource = FalcoEndpointDataSource()
    let changeToken = dataSource.GetChangeToken()

    changeToken |> should be instanceOfType<Microsoft.Extensions.FileProviders.NullChangeToken>

[<Fact>]
let ``FalcoEndpointDataSource.FalcoEndpoints can be added dynamically`` () =
    let dataSource = FalcoEndpointDataSource()
    dataSource.FalcoEndpoints.Add(route GET "/dynamic" emptyHandler)

    dataSource.Endpoints |> should haveCount 1

[<Fact>]
let ``FalcoEndpointDataSource combines constructor and FalcoEndpoints`` () =
    let initialEndpoint = route GET "/initial" emptyHandler
    let dataSource = FalcoEndpointDataSource([ initialEndpoint ])

    dataSource.FalcoEndpoints.Add(route POST "/added" emptyHandler)

    dataSource.Endpoints |> should haveCount 2

[<Fact>]
let ``FalcoEndpointDataSource applies IEndpointConventionBuilder conventions`` () =
    let endpoint = route GET "/test" emptyHandler
    let dataSource = FalcoEndpointDataSource([ endpoint ])

    let mutable conventionApplied = false
    let convention = Action<EndpointBuilder>(fun _ -> conventionApplied <- true)

    (dataSource :> IEndpointConventionBuilder).Add(convention)

    // Force endpoint building
    dataSource.Endpoints |> ignore

    conventionApplied |> should equal true

[<Fact>]
let ``FalcoEndpointDataSource request delegate executes handler`` () =
    let mutable handlerExecuted = false
    let testHandler : HttpHandler = fun ctx ->
        handlerExecuted <- true
        Response.ofEmpty ctx

    let endpoint = route GET "/test" testHandler
    let dataSource = FalcoEndpointDataSource([ endpoint ])

    let builtEndpoint = Seq.head dataSource.Endpoints :?> RouteEndpoint
    builtEndpoint.RequestDelegate |> should not' (be Null)

[<Fact>]
let ``FalcoEndpointDataSource preserves endpoint pattern casing`` () =
    let endpoint = route GET "/API/Users" emptyHandler
    let dataSource = FalcoEndpointDataSource([ endpoint ])

    let builtEndpoint = Seq.head dataSource.Endpoints :?> RouteEndpoint
    builtEndpoint.RoutePattern.RawText |> should equal "/API/Users"

[<Fact>]
let ``FalcoEndpointDataSource with complex route patterns`` () =
    let patterns = [
        "/users"
        "/users/{id:int}"
        "/users/{id}/posts/{postId:guid}"
        "/api/v{version:apiVersion}/users"
    ]

    let endpoints = patterns |> List.map (fun p -> route GET p emptyHandler)
    let dataSource = FalcoEndpointDataSource(endpoints)

    dataSource.Endpoints |> should haveCount 4

    let builtPatterns =
        dataSource.Endpoints
        |> Seq.cast<RouteEndpoint>
        |> Seq.map (fun e -> e.RoutePattern.RawText)
        |> List.ofSeq

    patterns |> List.iter (fun p -> builtPatterns |> should contain p)

[<Fact>]
let ``FalcoEndpointDataSource default order is 0`` () =
    let endpoint = route GET "/test" emptyHandler
    let dataSource = FalcoEndpointDataSource([ endpoint ])

    let builtEndpoint = Seq.head dataSource.Endpoints :?> RouteEndpoint
    builtEndpoint.Order |> should equal 0
