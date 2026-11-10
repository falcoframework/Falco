namespace Falco

[<AutoOpen>]
module Extensions =
    open Microsoft.AspNetCore.Builder
    open Microsoft.AspNetCore.Http
    open Microsoft.AspNetCore.Routing
    open Microsoft.Extensions.Configuration
    open Microsoft.Extensions.DependencyInjection
    open Microsoft.Extensions.Logging

    type IEndpointRouteBuilder with
        /// Registers a sequence of `Falco.HttpEndpoint` instances with the endpoint route builder.
        member this.UseFalcoEndpoints(endpoints : HttpEndpoint seq) : IEndpointRouteBuilder =
            let falcoDataSource =
                let registeredDataSource = this.ServiceProvider.GetService<FalcoEndpointDataSource>()
                if obj.ReferenceEquals(registeredDataSource, null) then
                    FalcoEndpointDataSource([])
                else
                    registeredDataSource

            for endpoint in endpoints do
                falcoDataSource.FalcoEndpoints.Add(endpoint)

            this.DataSources.Add(falcoDataSource)

            this

    type WebApplicationBuilder with
        /// Applies `fn` to the application's configuration builder.
        member this.AddConfiguration(fn : IConfigurationBuilder -> IConfigurationBuilder) : WebApplicationBuilder =
            fn this.Configuration |> ignore
            this

        /// Applies `fn` to the application's logging builder.
        member this.AddLogging(fn : ILoggingBuilder -> ILoggingBuilder) : WebApplicationBuilder =
            fn this.Logging |> ignore
            this

        /// Apply `fn` to `WebApplicationBuilder.Services :> IServiceCollection`  if `predicate` is true.
        member this.AddServicesIf(predicate : bool, fn : IConfiguration -> IServiceCollection -> IServiceCollection) : WebApplicationBuilder =
            if predicate then fn this.Configuration this.Services |> ignore
            this

        /// Applies `fn` to the application's configuration and service collection.
        member this.AddServices(fn : IConfiguration -> IServiceCollection -> IServiceCollection) : WebApplicationBuilder =
            this.AddServicesIf(true, fn)

    type IApplicationBuilder with
        /// Apply `fn` to `WebApplication :> IApplicationBuilder` if `predicate` is true.
        member this.UseIf(predicate : bool, fn : IApplicationBuilder -> IApplicationBuilder) : IApplicationBuilder =
            if predicate then fn this |> ignore
            this

        /// Analogous to `IApplicationBuilder.Use` but returns `WebApplication`.
        member this.Use(fn : IApplicationBuilder -> IApplicationBuilder) : IApplicationBuilder =
            this.UseIf(true, fn)

        /// Activates Falco integration with IEndpointRouteBuilder. Scans for
        /// `IFalcoModule` implementations and appends collected endpoints to
        /// the `endpoints` argument.
        ///
        /// This is the default way to enable the library.
        member this.UseFalco(endpoints : HttpEndpoint seq) : IApplicationBuilder =
            this.UseEndpoints(fun endpointBuilder ->
                endpointBuilder.ServiceProvider.GetServices<IFalcoModule>()
                |> Seq.collect _.Endpoints
                |> fun moduleEndpoints -> Seq.concat [ endpoints; moduleEndpoints ]
                |> endpointBuilder.UseFalcoEndpoints |> ignore)

        /// Activates Falco integration with IEndpointRouteBuilder without additional endpoints.
        member this.UseFalco() =
            this.UseFalco([])

        /// Registers a `Falco.HttpHandler` as terminal middleware (i.e., not found).
        /// This should be registered at the end of the middleware pipeline to catch any
        /// requests that were not handled by any other middleware. The provided
        /// handler will be executed for any requests that reach this point in
        /// the pipeline, allowing you to return a custom 404 response or perform
        /// other actions as needed.
        member this.UseFalcoNotFound(notFoundHandler : HttpHandler) : unit =
            this.Run(handler = HttpHandler.toRequestDelegate notFoundHandler)

        /// Registers a `Falco.HttpHandler` as exception handler lambda.
        /// See: https://docs.microsoft.com/en-us/aspnet/core/fundamentals/error-handling?#exception-handler-lambda
        member this.UseFalcoExceptionHandler(exceptionHandler : HttpHandler) : IApplicationBuilder =
            let configure (appBuilder : IApplicationBuilder) =
                appBuilder.Run(HttpHandler.toRequestDelegate exceptionHandler)

            this.UseExceptionHandler(configure) |> ignore
            this

    type WebApplication with
        /// Registers a `Falco.HttpHandler` as terminal middleware (i.e., not found)
        /// then runs application, blocking the calling thread until host shutdown.
        member this.Run(terminalHandler : HttpHandler) : unit =
            this.UseFalcoNotFound(terminalHandler) |> ignore
            this.Run()

        /// Apply `fn` to `WebApplication :> IApplicationBuilder` if `predicate` is true.
        member this.UseIf(predicate : bool, fn : IApplicationBuilder -> IApplicationBuilder) : WebApplication =
            (this :> IApplicationBuilder).UseIf(predicate, fn) |> ignore
            this

        /// Analogous to `IApplicationBuilder.Use` but returns `WebApplication`.
        member this.Use(fn : IApplicationBuilder -> IApplicationBuilder) : WebApplication =
            this.UseIf(true, fn)

        /// Adds endpoint routing to the application pipeline.
        member this.UseRouting() : WebApplication =
            (this :> IApplicationBuilder).UseRouting() |> ignore
            this

        /// Activates Falco integration with IEndpointRouteBuilder.
        ///
        /// This is the default way to enable the package.
        member this.UseFalco(endpoints : HttpEndpoint seq) : WebApplication =
            (this :> IApplicationBuilder).UseFalco(endpoints) |> ignore
            this

        /// Activates Falco integration with IEndpointRouteBuilder without additional endpoints.
        member this.UseFalco() =
            this.UseFalco([])

        /// Registers a `Falco.HttpHandler` as exception handler lambda.
        /// See: https://docs.microsoft.com/en-us/aspnet/core/fundamentals/error-handling?#exception-handler-lambda
        member this.UseFalcoExceptionHandler(exceptionHandler : HttpHandler) : WebApplication =
            (this :> IApplicationBuilder).UseFalcoExceptionHandler(exceptionHandler) |> ignore
            this

        /// Registers a `Falco.HttpHandler` as terminal middleware (i.e., not found).
        member this.UseFalcoNotFound(notFoundHandler : HttpHandler) : WebApplication =
            (this :> IApplicationBuilder).UseFalcoNotFound(notFoundHandler) |> ignore
            this

    /// Provides extension methods for Falco application configuration.
    type FalcoExtensions =
        /// Registers a `Falco.HttpHandler` as global exception handler.
        static member UseFalcoExceptionHandler
            (exceptionHandler : HttpHandler)
            (app : IApplicationBuilder) =
            app.UseFalcoExceptionHandler exceptionHandler

    type HttpContext with
        /// Attempts to obtain a dependency from IServiceCollection.
        /// Throws InvalidDependencyException if the dependency is missing.
        member this.Plug<'T>() =
            this.RequestServices.GetRequiredService<'T>()
