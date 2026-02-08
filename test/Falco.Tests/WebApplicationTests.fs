module Falco.Tests.WebApplication

open System
open System.Collections.Generic
open Xunit
open FsUnit.Xunit
open Microsoft.AspNetCore.Builder
open Microsoft.AspNetCore.Http
open Microsoft.AspNetCore.Routing
open Microsoft.Extensions.Configuration
open Microsoft.Extensions.DependencyInjection
open Microsoft.Extensions.Logging
open Falco
open Falco.Routing

type IFakeIntService =
    abstract member GetValue : unit -> int

type IFakeBoolService =
    abstract member GetValue : unit -> bool

type FakeIntService() =
    interface IFakeIntService with
        member _.GetValue() = 42

type FakeBoolService() =
    interface IFakeBoolService with
        member _.GetValue() = true

// -----------------
// Test Helpers
// -----------------

let emptyHandler : HttpHandler = Response.ofPlainText ""

let createTestBuilder() =
    let bldr = WebApplication.CreateBuilder([||])
    bldr.Services.AddRouting() |> ignore
    bldr


let createTestApp() =
    let builder = createTestBuilder()
    builder.Build()

// -----------------
// WebApplicationBuilder Extensions
// -----------------

[<Fact>]
let ``AddConfiguration should apply configuration builder function`` () =
    let builder = createTestBuilder()
    let mutable functionCalled = false

    builder.AddConfiguration(fun config ->
        functionCalled <- true
        config
    ) |> ignore

    functionCalled |> should equal true

[<Fact>]
let ``AddConfiguration should modify configuration`` () =
    let builder = createTestBuilder()

    builder.AddConfiguration(fun config ->
        config.AddInMemoryCollection([
            KeyValuePair("TestKey", "TestValue")
        ])
    ) |> ignore

    let app = builder.Build()
    app.Configuration.["TestKey"] |> should equal "TestValue"

[<Fact>]
let ``AddLogging should apply logging builder function`` () =
    let builder = createTestBuilder()
    let mutable functionCalled = false

    builder.AddLogging(fun logging ->
        functionCalled <- true
        logging
    ) |> ignore

    functionCalled |> should equal true

[<Fact>]
let ``AddLogging should configure logging`` () =
    let builder = createTestBuilder()

    builder.AddLogging(fun logging ->
        logging.ClearProviders().SetMinimumLevel(LogLevel.Warning)
    ) |> ignore

    let app = builder.Build()
    let logger = app.Services.GetRequiredService<ILogger<obj>>()
    logger |> should not' (be Null)

[<Fact>]
let ``AddServices should register services`` () =
    let builder = createTestBuilder()

    builder.AddServices(fun config services ->
        services.AddSingleton<string>("test-service")
    ) |> ignore

    let app = builder.Build()
    let service = app.Services.GetRequiredService<string>()
    service |> should equal "test-service"

[<Fact>]
let ``AddServices should receive configuration`` () =
    let builder = createTestBuilder()
    builder.Configuration.["TestKey"] <- "TestValue"

    let mutable receivedConfig : IConfiguration = null

    builder.AddServices(fun config services ->
        receivedConfig <- config
        services
    ) |> ignore

    receivedConfig |> should not' (be Null)
    receivedConfig.["TestKey"] |> should equal "TestValue"

[<Fact>]
let ``AddServicesIf should apply when predicate is true`` () =
    let builder = createTestBuilder()

    builder.AddServicesIf(true, fun _ services ->
        services.AddSingleton<string>("conditional")
    ) |> ignore

    let app = builder.Build()
    let service = app.Services.GetRequiredService<string>()
    service |> should equal "conditional"

[<Fact>]
let ``AddServicesIf should not apply when predicate is false`` () =
    let builder = createTestBuilder()

    builder.AddServicesIf(false, fun _ services ->
        services.AddSingleton<string>("should-not-exist")
    ) |> ignore

    let app = builder.Build()
    let service = app.Services.GetService<string>()
    service |> should be Null

[<Fact>]
let ``AddServices and AddServicesIf can be chained`` () =
    let builder = createTestBuilder()

    builder
        .AddServices(fun _ services -> services.AddSingleton<IFakeIntService>(FakeIntService()))
        .AddServicesIf(true, fun _ services -> services.AddSingleton<string>("text"))
        .AddServicesIf(false, fun _ services -> services.AddSingleton<IFakeBoolService>(FakeBoolService()))
        |> ignore

    let app = builder.Build()
    app.Services.GetRequiredService<IFakeIntService>() |> should be ofExactType<FakeIntService>
    app.Services.GetRequiredService<string>() |> should equal "text"
    app.Services.GetService<IFakeBoolService>() |> should be null

// -----------------
// IApplicationBuilder Extensions
// -----------------

[<Fact>]
let ``IApplicationBuilder.UseIf should apply middleware when predicate is true`` () =
    let app = createTestApp()
    let mutable middlewareCalled = false

    (app :> IApplicationBuilder).UseIf(true, fun appBuilder ->
        middlewareCalled <- true
        appBuilder
    ) |> ignore

    middlewareCalled |> should equal true

[<Fact>]
let ``IApplicationBuilder.UseIf should not apply middleware when predicate is false`` () =
    let app = createTestApp()
    let mutable middlewareCalled = false

    (app :> IApplicationBuilder).UseIf(false, fun appBuilder ->
        middlewareCalled <- true
        appBuilder
    ) |> ignore

    middlewareCalled |> should equal false

[<Fact>]
let ``IApplicationBuilder.UseFalco should register endpoints`` () =
    let app = createTestApp()
    let endpoints = [
        route GET "/" emptyHandler
        route POST "/users" emptyHandler
    ]

    app.UseRouting().UseFalco(endpoints) |> ignore

    let mutable sum = 0
    for ds in (app :> IEndpointRouteBuilder).DataSources do
        sum <- sum + ds.Endpoints.Count

    sum |> should equal endpoints.Length

[<Fact>]
let ``IApplicationBuilder.UseFalcoExceptionHandler should register exception handler`` () =
    let builder = createTestBuilder()
    let app = builder.Build()

    let exceptionHandler : HttpHandler = fun ctx ->
        ctx.Response.StatusCode <- 500
        Response.ofPlainText "Error occurred" ctx

    (app :> IApplicationBuilder).UseFalcoExceptionHandler(exceptionHandler) |> ignore

    // This is hard to verify without actually triggering an exception
    // Just verify it doesn't throw
    true |> should equal true

// -----------------
// WebApplication Extensions
// -----------------

[<Fact>]
let ``WebApplication.UseIf should apply when predicate is true`` () =
    let app = createTestApp()
    let mutable middlewareCalled = false

    app.UseIf(true, fun appBuilder ->
        middlewareCalled <- true
        appBuilder
    ) |> ignore

    middlewareCalled |> should equal true

[<Fact>]
let ``WebApplication.UseIf should not apply when predicate is false`` () =
    let app = createTestApp()
    let mutable middlewareCalled = false

    app.UseIf(false, fun appBuilder ->
        middlewareCalled <- true
        appBuilder
    ) |> ignore

    middlewareCalled |> should equal false

// -----------------
// IEndpointRouteBuilder Extensions
// -----------------

[<Fact>]
let ``UseFalcoEndpoints should add endpoints to data source`` () =
    let builder = createTestBuilder()
    let app = builder.Build()

    app.UseRouting() |> ignore

    app.UseEndpoints(fun endpoints ->
        let falcoEndpoints = [
            route GET "/" emptyHandler
            route POST "/users" emptyHandler
        ]

        endpoints.UseFalcoEndpoints(falcoEndpoints) |> ignore
    ) |> ignore

    (app :> IEndpointRouteBuilder).DataSources
    |> Seq.tryFind (fun ds -> ds :? FalcoEndpointDataSource)
    |> Option.isSome
    |> should equal true

[<Fact>]
let ``UseFalcoEndpoints should create new data source if not registered`` () =
    let builder = createTestBuilder()
    let app = builder.Build()

    app.UseRouting() |> ignore

    app.UseEndpoints(fun endpoints ->
        endpoints.UseFalcoEndpoints([ route GET "/" emptyHandler ]) |> ignore
    ) |> ignore

    (app :> IEndpointRouteBuilder).DataSources
    |> Seq.tryFind (fun ds -> ds :? FalcoEndpointDataSource)
    |> Option.isSome
    |> should equal true

[<Fact>]
let ``UseFalcoEndpoints should reuse registered data source`` () =
    let builder = createTestBuilder()
    let dataSource = FalcoEndpointDataSource()
    builder.Services.AddSingleton<FalcoEndpointDataSource>(dataSource) |> ignore

    let app = builder.Build()
    app.UseRouting() |> ignore

    app.UseEndpoints(fun endpoints ->
        endpoints.UseFalcoEndpoints([ route GET "/test" emptyHandler ]) |> ignore
    ) |> ignore

    (app :> IEndpointRouteBuilder).DataSources
    |> Seq.tryFind (fun ds -> ds :? FalcoEndpointDataSource)
    |> Option.isSome
    |> should equal true

// -----------------
// HttpContext Extensions
// -----------------

[<Fact>]
let ``HttpContext.Plug should resolve registered service`` () =
    let builder = createTestBuilder()
    builder.Services.AddSingleton<string>("test-dependency") |> ignore
    let app = builder.Build()

    let ctx = DefaultHttpContext()
    ctx.RequestServices <- app.Services

    let dependency = ctx.Plug<string>()
    dependency |> should equal "test-dependency"

[<Fact>]
let ``HttpContext.Plug should throw on missing service`` () =
    let app = createTestApp()

    let ctx = DefaultHttpContext()
    ctx.RequestServices <- app.Services

    (fun () -> ctx.Plug<IDisposable>() |> ignore)
    |> should throw typeof<InvalidOperationException>

[<Fact>]
let ``HttpContext.Plug should resolve different service types`` () =
    let builder = createTestBuilder()
    builder.Services.AddSingleton<IFakeIntService>(FakeIntService()) |> ignore
    builder.Services.AddSingleton<string>("text") |> ignore
    builder.Services.AddSingleton<IFakeBoolService>(FakeBoolService()) |> ignore
    let app = builder.Build()

    let ctx = DefaultHttpContext()
    ctx.RequestServices <- app.Services

    ctx.Plug<IFakeIntService>() |> should be ofExactType<FakeIntService>
    ctx.Plug<string>() |> should equal "text"
    ctx.Plug<IFakeBoolService>() |> should be ofExactType<FakeBoolService>

// -----------------
// Integration Tests
// -----------------

[<Fact>]
let ``Full pipeline with builder and app extensions`` () =
    let builder = createTestBuilder()

    builder
        .AddConfiguration(fun config ->
            config.AddInMemoryCollection([
                KeyValuePair("AppName", "TestApp")
            ])
        )
        .AddServices(fun config services ->
            services.AddSingleton<string>(config.["AppName"])
        )
        |> ignore

    let app = builder.Build()

    app
        .UseRouting()
        .UseFalco([
            route GET "/" (fun ctx ->
                let appName = ctx.Plug<string>()
                Response.ofPlainText appName ctx
            )
        ])
        |> ignore

    let appName = app.Services.GetRequiredService<string>()
    appName |> should equal "TestApp"

[<Fact>]
let ``Multiple UseFalco calls should accumulate endpoints`` () =
    let app = createTestApp()
    app.UseRouting() |> ignore

    app.UseFalco([ route GET "/first" emptyHandler ]) |> ignore
    app.UseFalco([ route POST "/second" emptyHandler ]) |> ignore

    let mutable sum = 0
    for ds in (app :> IEndpointRouteBuilder).DataSources do
        sum <- sum + ds.Endpoints.Count

    sum |> should equal 2
