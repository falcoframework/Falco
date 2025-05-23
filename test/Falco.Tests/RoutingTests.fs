﻿module Falco.Tests.Routing

open Xunit
open Falco
open Falco.Routing
open FsUnit.Xunit
open Microsoft.AspNetCore.Routing

let emptyHandler : HttpHandler = Response.ofPlainText ""

[<Fact>]
let ``route function should return valid HttpEndpoint`` () =
    let routeVerb = GET
    let routePattern = "/"

    let endpoint = route routeVerb routePattern emptyHandler
    endpoint.Pattern |> should equal routePattern

    let (verb, handler) = Seq.head endpoint.Handlers
    verb |> should equal routeVerb
    handler |> should be instanceOfType<HttpHandler>

[<Fact>]
let ``any function returns HttpEndpoint matching ANY HttpVerb`` () =
    let testEndpointFunction
        mapEndPoint
        (verb : HttpVerb) =
        let pattern = "/"
        let endpoint = mapEndPoint pattern emptyHandler
        endpoint.Pattern |> should equal pattern
        let (verb, handler) = Seq.head endpoint.Handlers
        verb |> should equal verb
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
let ``a test`` () =
    let endpoint = route GET "/" emptyHandler |> setDisplayName "emptyHandler" |> setOrder 99
    let dataSource = FalcoEndpointDataSource([ endpoint ])
    let builtEndpoints = dataSource.Endpoints
    builtEndpoints |> should haveCount 1
    let builtEndpoint = Seq.head builtEndpoints :?> RouteEndpoint
    builtEndpoint.DisplayName |> should equal "emptyHandler"
    builtEndpoint.Order |> should equal 99
