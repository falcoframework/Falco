# Changelog

All notable changes to this project will be documented in this file.

## [6.0.0] - _unreleased_

### Added

- `Request.getBodyStringOptions` to allow configuration of the maximum request body size, with a default of `Multipart.DefaultMaxSize` (32MB).
- `Request.getFormOptions` to allow configuration of the maximum form size, with a default of `Multipart.DefaultMaxSize` (32MB).
- Guards to `RequestValue.parse` to prevent oversized numerics or leading-zero numeric strings (non-decimal) from being parsed as floats.
- Max size constraint to multipart form streaming, with a default of `Multipart.DefaultMaxSize` (32MB).
- `FormData.IsValid` property to indicate whether CSRF validation succeeded for form requests.

### Changed

- `Request.getForm` now automatically performs CSRF validation if antiforgery services are registered and the request method is POST, PUT, PATCH, or DELETE. Returns `FormData` with `IsValid = false` when validation fails.
- Increased multipart form streaming buffer size from 1024 to 8192 bytes to improve performance of large file uploads.
- `Request.ifAuthenticatedInRole` accepts roles as `seq` instead of `list` for more flexible input options.
- `Request.getJsonOptions` explicitly checks for application/json content type and empty body, returning default form of `T`. Returns 415 Unsupported Media Type response when content type is missing.
- `Response.signInOptions` and `Response.signOutOptions` now conditionally set status code 301 and Location header only when `AuthenticationProperties.RedirectUri` is present.
- `Response.challengeOptions` and `Response.challengeAndRedirect` now set status code 401 and WWW-Authenticate header to properly indicate authentication challenge.

### Fixed

- Unnecessary URL decode in `RequestValue.parseString`.
- Default `JsonSerializerOptions` used for JSON deserialization, optimized by making read-only and static to prevent unnecessary allocations.
- `Response.ofAttachment` now properly escapes provided filename to ensure correct handling of special characters and spaces across different browsers.
- Added missing Location header to `Response.signInOptions` and `Response.signOutOptions` when redirect URI is specified.

### Removed

- `Request.getFormSecure` and `Request.mapFormSecure`, which were explicit enablements of the now default CSRF validation for form requests. `Request.getForm` and `Request.mapForm` now include automatic CSRF validation when antiforgery services are registered and request method is POST, PUT, PATCH, or DELETE.

## [5.2.0] - 2025-12-21

### Added

- `net10.0` support.

## [5.1.0] - 2025-09-09

### Added

- `Response.ofFragment` and `Response.ofFragmentCsrf` to return HTML fragments by element ID, with optional CSRF token support.
- `Falco.Markup` [version 1.4.0](https://www.nuget.org/packages/Falco.Markup/1.4.0), which reverted API to 1.2.0 and provided correct support for template fragments.

## [5.0.3] - 2025-09-09

### Added

- `Falco.Markup` [version 1.3.0](https://www.nuget.org/packages/Falco.Markup/1.3.0), which enables support for template fragment responses.

## [5.0.2] - 2025-06-26

### Fixed

- Request module error for JSON transfer encoding chunked requests.

## [5.0.1] - 2025-06-26

### Added

- `Falco.Markup` [version 1.2.0](https://www.nuget.org/packages/Falco.Markup/1.2.0) support, which includes a unified DSL module introduces consistent prefix-based naming conventions for elements, text shortcuts, and attributes, making code cleaner and more readable.

### Fixed

- `Request.mapCookies` and `Request.mapHeaders` re-added, which were accidentally removed in the 5.0.0 release.

## [5.0.0] - 2025-01-28

### Added

- Declarative OpenAPI support.
- `RequestData` (and `RequestValue`) to support complex form & query submissions,
  - Provided by an HTTP key/value pair (i.e., `name=falco&classification=toolkit`) parser.
  - A derivative `FormData` contains parsed `RequestValue` and access to `IFormFileCollection`.
- `HttpContext.Plug<T>` for generic injection support of dependencies within `HttpHandler`'s (service locator pattern).
- `Request.getJson<T>` for generic JSON request deserialization, using default settings (property name case-insensitive, trailing commas allowed).
- `Request.getCookies`, replacing `Request.getCookie`.
- `Response.signInOptions` to sign in claim principal for provided scheme and options then responds with a 301 redirect to provided URL.
- `Response.challengeAndRedirect`, replacing `Response.challengeWithRedirect`.
- `Routing.map[Get|Head|Post|Put|Patch|Delete|Options|Trace|Any]` which produces `HttpEndpoint` by associating a route pattern to an `HttpHandler` after mapping route.
- `Routing.setDisplayName` to set the display name of the endpoint.
- `Routing.setOrder` to set the order number of the endpoint.
- `WebApplication.run`, registers the provided `HttpHandler` as the terminal middleware and runs the application.

### Changed

- `Xss` module renamed to `Xsrf`. Functions: `Xsrf.antiforgeryInput`, `Xsrf.getToken` & `Xsrf.validateToken`.

### Fixed

- Missing cancellation token pass-through during form reading, `multipart/form-data` streaming and JSON serialization/deserialization.
- Empty request body support for JSON request methods.
- `WebApplication.UseFalcoNotFound` & `IApplicationBuilder.UseFalcoNotFound` to correctly terminate by returning `unit` akin to the native method.

### Removed

- `net6.0` support dropped (end of life 2024-11-12).
- `webHost [||] {}` builder removed.
- `config {}` builder removed.
- `HttpContext.GetLogger<T>()` extension removed.
- `IApplicationBuilder.IsDevelopment()`, `IApplicationBuilder.UseWhen()` extensions removed.
- `Services.inject<T>` (and overloads) removed.
- `Response.withContentLength` removed (unsupported).
- `StringCollectionReader` and derivatives removed (`FormCollectionReader`, `QueryCollectionReader`, `RouteCollectionReader`, `HeaderCollectionReader`, and `CookieCollectionReader`).
    - All replaced by homogenous `RequestData` type.
- `Request.streamForm`, `Request.streamFormSecure`, `Request.mapFormStream` and `Request.mapFormStreamSecure` removed.
- `Falco.Security.Crypto` and `Falco.Security.Auth` modules removed.
- Removed `Request.getCookie`, renamed `Request.getCookies`.
- Removed `Response.challengeWithRedirect`, renamed `Response.challengeAndRedirect`.
- Removed `Response.debugRequest`.

## [4.0.6] - 2023-12-12

- `net7.0` and `net8.0` support added.
- Added ability to configure `IWebHostBuilder` to host builder (i.e., `webHost [||] { web_host (fun webHost -> ...) }`).

## [4.0.5] - 2023-11-16

- Execution order of configuration builder (i.e., `configuration { add_env }`) set to match [default](https://learn.microsoft.com/en-us/dotnet/core/extensions/configuration#alternative-hosting-approach) configuration behaviour.

## [4.0.4] - 2023-03-13

### Added

- `Request.getFormSecure` and `Request.streamFormSecure`.
- `use_cors` to host builder (i.e., `webHost [||] { use_cors }`).

### Removed

- Unused types `HttpContextAccessor` and `AsyncHttpContextAccessor`.

## [4.0.3] - 2023-01-01

### Added

- Working tutorial sample.
- Documentation website generator `/site`, and output `/docs`.

### Removed

- Internal utility functions `httpPipe` and `httpPipeTask`. See issue #94, #95.

## [4.0.2] - 2022-11-30

### Fixed

- NuGet package metadata, invalid readme.

### Changed

- Hello world sample to use ASP.NET static file middleware.
- Spelling and grammar of comments. See #96.

### Removed

- Unused internal function `String.parseInt`.

## [4.0.1] - 2022-11-23

### Added

- `Response.debugRequest`, which pretty prints the content of the current request to the screen.
- Related community projects and libraries to README.md.

### Fixed

- NuGet package metadata, invalid icon path.

## [4.0.0] - 2022-11-07

The project no longer intends to support anything prior to net6.0, which enables the built-in `task {}` computation expression.

### Added

- `StringCollectionReader.GetChildren`, safely retrieves a collection of readers. Intended to be used with the "dot notation" collection wire format (i.e., Person.First=John&Person.Last=Doe&Person.First=Jane&Person.Last=Doe).
- `MultipartReader.StreamSectionsAsync` for async streaming of multipart/form-data, following MSFT [spec](https://learn.microsoft.com/en-us/aspnet/core/mvc/models/file-uploads).
- `Services.inject` helpers, for CPS-style dependency injection, supporting up to five generic input types.
- `in_memory`, `required_ini`, `optional_ini`, `required_xml`, `optional_xml` custom operations added to the configuration builder.

### Changed

- `StringCollectionReader` abstract attribute removed, to support nested readers.
- `StringCollectionReader.Get{String|StringNonEmpty|Int16|Int32|Int|Int64|Boolean|Float|Decimal|DateTime|DateTimeOffset|Guid|TimeSpan}` default value made optional.
- Upgraded host builder expression from `IWebHostBuilde` to `WebApplication`.

### Removed

- `Falco.Markup`, module has been extracted into it's own [project](https://github.com/FalcoFramework/Falco.Markup).
- Additional `StringCollectionReader` constructors, per-collection type.
- `StringCollectionReader.TryArrayString`, use `StringCollectionReader.GetStringArray`
- `StringCollectionReader.TryArrayInt16`, use `StringCollectionReader.GetInt16Array`
- `StringCollectionReader.TryArrayInt32`, use `StringCollectionReader.GetInt32Array`
- `StringCollectionReader.TryArrayInt`, use `StringCollectionReader.GetIntArray`
- `StringCollectionReader.TryArrayInt64`, use `StringCollectionReader.GetInt64Array`
- `StringCollectionReader.TryArrayBoolean`, use `StringCollectionReader.GetBooleanArray`
- `StringCollectionReader.TryArrayFloat`, use `StringCollectionReader.GetFloatArray`
- `StringCollectionReader.TryArrayDecimal`, use `StringCollectionReader.GetDecimalArray`
- `StringCollectionReader.TryArrayDateTime`, use `StringCollectionReader.GetDateTimeArray`
- `StringCollectionReader.TryArrayDateTimeOffset`, use `StringCollectionReader.GetDateTimeOffsetArray`
- `StringCollectionReader.TryArrayGuid`, use `StringCollectionReader.GetGuidArray`
- `StringCollectionReader.TryArrayTimeSpan`, use `StringCollectionReader.GetTimeSpanArray`
- `HttpRequest.IsMultipart`, `HttpRequest.TryStreamFormAsync`, use `HttpRequest.StreamFormAsync()`
- `Request.tryBindRoute`, use `Request.getRoute`.
- `Request.tryBindQuery`, use `Request.getQuery`.
- `Request.tryBindForm`, use `Request.getForm`.
- `Request.tryBindFormStream`, use `Request.tryStreamForm`.
- `Request.tryBindCookie`, use `Request.getCookie`.
- `Request.getJson`, use `Request.getJsonOptions Constants.defaultJsonOptions`.
- `Request.tryBindJsonOptions`, use `Request.getJsonOptions`.
- `Request.tryBindJson`, use `Request.getJsonOptions Constants.defaultJsonOptions`.
- `Request.bindJson`, use `Request.mapJson`.
- `Request.bindRoute`, use `Request.mapRoute`.
- `Request.bindQuery`, use `Request.mapQuery`.
- `Request.bindCookie`, use `Request.mapCookie`.
- `Request.bindForm`, use `Request.mapForm`.
- `Request.bindFormStream`, use `Request.mapFormStream`.
- `Request.bindFormSecure`, use `Request.mapFormSecure`.
- `Request.bindFormStreamSecure`, use `Request.mapFormStreamSecure`.
- `Response.withHeader`, use `Response.withHeaders [ x ]`.
- `Response.redirect`, use `Response.redirectTemporarily` or `Response.redirectPermanently`

## [3.1.14] - 2022-08-29

### Added

- Embedded readme and project icon into NuGet package.
- Additional obsolete attributes to functions in preparation for v4.x.

### Fixed

- `Request.mapJson` failing to invoke next handler, caused by a bad merge which left the valid function body commented out.

## [3.1.13] - 2022-08-11

### Added

- Obsolete attributes to `Request.bind{Json|Route|Query|Cookie|Form|FormStream|FormSecure|FormStreamSecure} functions in preparation for v4.x.

### Fixed

- Typo in `Markup.Attr.httpEquiv`.

## [3.1.12] - 2022-05-20

### Added

- `Auth.signInOptions` to establish an authenticated context for the provide scheme, options and principal
- `Markup.Attr.open'`.
- Missing .gitignore items for JetBrains.

## [3.1.11] - 2/8/2022

### Added

- `Auth.challenge` to challenge the specified authentication scheme.
- `Response.challengeWithRedirect` to challenge the specified authentication scheme and redirect URI.

### Fixed

- Website to address certain accessibility issues.

## [3.1.10] - 12/14/2021

### Changed

- Incorporated built-in `System.Task` expression, with compiler directives to continue supporting Ply usage.

## [3.1.9] - 12/6/2021

### Changed

- `StringCollectionReader` lookups made case-insensitive.

## [3.1.8] - 12/3/2021

### Added

- `net6.0` support.

### Changed

- Embedded PDBs to faciliate sourcelink.

## [3.1.7] - 9/24/2021

### Added

- `HttpVerb.toHttpMethodMetadata` to properly capture the `HttpVerb.ANY` to produce an empty `HttpMethodData` (not `HttpMethodData [| "ANY" |]`).

## [3.1.6] - 9/24/2021

### Removed

- Mistakenly added, experimental `Request.signOut` function.

## [3.1.5] - 9/24/2021

### Added

- Route name metadata to support ASP.NET link generator.
- Null check to internal `Response.writeString`.
- Explicit starting size for the internal `StringBuilder` within `XmlNodeSerializer`

## [3.1.4] - 8/24/2021

### Added

- Comparative view engine benchmarks.
- `XmlNodeSerializer` type.
- Source link support.

## [3.1.3] - 8/4/2021

### Added

- Dependency on [Ply](https://github.com/crowded/ply).
- `Request.authenticate` to authenticate the current request using the provided scheme.

### Removed

- TPL helpers.

## [3.1.2] - 7/30/2021

### Changed

- CSRF validation switched to occur _after_ form is streamed, which includes enabling buffering for the request body.


## [3.1.1] - 7/27/2021

### Added

- `stringf` function.

### Removed

- Dependency on [Taskbuilder.fs](https://github.com/rspeele/Taskbuilder.fs), replaced with TPL helpers.

## [3.1.0] - 7/27/2021

### Added

- `FalcoEndpointDataSource` to properly integrate with ASP.NET endpoint routing.
- Binary response handlers `Response.ofBinary`, `Response.ofAttachment`.
- `IConfiguration` builder expression with JSON input hooks.
- `Auth.getClaimValue`
- `IServiceCollection` operations to the HostBuilder expression: `add_service`, `add_antiforgery`, `add_cookie`, `add_conf_cookies`, `add_authorization`, `add_data_protection`, `add_http_client`.
- `IApplicationBuilder` operations to the HostBuilder expression:
`use_middleware`, `use_if`, `use_ifnot`, `use_authentication`, `use_authorization`, `use_caching`, `use_compression`, `use_hsts`, `use_https`, `use_static_files`.
- `not_found` operation added to HostBuilder expression to serve default document when no endpoints are matched.

### Changed

- Internal `Response.writeBytes` to use `BodyWriter`.

### Fixed

- Optional JSON config file fix, misassigned.

### Removed

- MVC and REST templates

## [3.0.5] - 6/14/2021

### Added

- PowerShell website build script.

### Fixed

- Null reference exception when consuming `IFormCollection.Files`.

## [3.0.4] - 5/5/2021

### Added

- `Response.signInAndRedirect`.
- `IEndpointRouteBuilder` extension method `UserFalcoEndpoints`.


## [3.0.3] - 4/10/2021

### Added

- `Auth.hasScope`, `Auth.tryFindClaim`, `Auth.getClaim`.
- `Request.ifAuthenticatedWithScope`.
- `CookieCollectionReader`, accessible get `Request.getCookie`, `Request.tryBindCookie`, `Request.bindCookie`, `Request.mapCookie`.
- `StringUtils.strSplit`.

## [3.0.2] - 12/8/2020

### Added

- `Markup.Elem.form`, `Markup.Elem.button`, `Markup.Elem.script`

## [3.0.1] - 12/1/2020

### Fixed

- `Markup.Templates.html5` not using provided language code.

## [3.0.0] - 11/27/2020

### Added

- `net5.0` support.
- `IHost` builder expression, `webHost [||] {}`.
- `IServiceCollection.AddFalco`.
- `IServiceCollection.AddFalco (routeOptions : RouteOptions -> unit)`.
- `IApplicationBuilder.UseFalco (endpoints : HttpEndpoint list)`.
- `IApplicationBuilder.UseFalcoExceptionHandler (exceptionHandler : HttpHandler)`.
- `QueryCollectionReader`.
- `HeaderCollectionReader`.
- `RouteCollectionReader`.

### Removed

- Extensions, `HttpRequest.GetHeader`, `HttpRequest.GetRouteValues`, `HttpRequest.GetRouteReader`.
- Exceptions. `ExceptionHandler`, `ExceptionHandlingMiddleware`.
- Host module, `Host.defaultExceptionHandler`, `Host.defaultNotFoundHandler`, `Host.startWebHostDefault`, `Host.startWebHost`.
- `IApplicationBuilder.UseHttpEndpoints (endpoints : HttpEndpoint list)` replaced by `IApplicationBuilder.UseFalco (endpoints : HttpEndpoint list)`.
- `Request.getHeader`, `Request.getRouteValues` replaced by `Request.getRoute`, `Request.tryGetRouteValue`.
- `StringCollectionReader` ? dynamic operator

## [2.1.0] - 11/11/2020

### Added

- Multimethod `HttpEndpoint` support.
- `StringCollectionReader.TryGetStringNonEmpty` which returns `None` for empty, whitespace and null value strings.

## [2.0.4] - 11/9/2020

### Added

- `Request.tryBindRoute`, `Request.mapRoute` and `Request.bindRoute`.
- `Request.bindQuery`.
- `Request.bindJson` which uses `System.Text.Json`.

## [2.0.3] - 10/31/2020

### Added

- Dependency on [Taskbuilder.fs](https://github.com/rspeele/Taskbuilder.fs), with internal extesion for `Task<unit> -> Task` conversion.

## [2.0.2] - 7/31/2020

### Added

- `Request.validateCsrfToken` which uses `Microsoft.AspNetCore.Antiforgery`.
- `Response.ofJson` which uses `System.Text.Json` and references `Constants.defaultJsonOptions`.
- `Response.ofEmpty`.

## [2.0.1] - 7/20/2020

### Changed

- Parameter ordering for `Response.withCookieOptions`, `Response.ofJsonOptions` to ensure configuration uniformly occured first.

## [2.0.0] - 7/12/2020

### Added

- `HttpResponseModifier` defined as `HttpContext -> HttpContext` used to make non-IO modifications to the `HttpResponse`.
- `Response` and `Request` modules, which provide functional access to the `HttpResponse` and `HttpRequest` respectively.

    - `Response.redirect`
    - `Response.withHeader`
    - `Response.withContentLength`
    - `Response.withContentType`
    - `Response.withStatusCode`
    - `Response.withCookie`
    - `Response.withCookieOptions`
    - `Response.ofString`
    - `Response.ofPlainText`
    - `Response.ofHtml`
    - `Response.ofJson`
    - `Response.ofJsonOptions    `
    - `Request.getVerb`
    - `Request.getRouteValues`
    - `Request.tryGetRouteValue`
    - `Request.getQuery`
    - `Request.tryBindQuery`
    - `Request.getForm`
    - `Request.tryBindForm`
    - `Request.tryStreamForm`
    - `Request.tryBindJson`
    - `Request.tryBindJsonOptions`

### Changed

- `HttpHandler` definition changed to `HttpContext -> Task`.
- `Falco.ViewEngine` becomes `Falco.Markup`
- Markup functions are now fully qualified (i.e., `Elem.h1` instead of `h1`).
- `webApp

## [1.2.3] - 7/2/2020
## [1.2.2] - 6/29/2020
## [1.2.1] - 6/28/2020
## [1.2.0] - 6/23/2020
## [1.1.0] - 6/6/2020

Still kicking myself over this brainfart. Starting version `1.1` for the win 🙄.
