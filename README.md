# Falco

[![NuGet Version](https://img.shields.io/nuget/v/Falco.svg)](https://www.nuget.org/packages/Falco)
[![build](https://github.com/FalcoFramework/Falco/actions/workflows/build.yml/badge.svg)](https://github.com/FalcoFramework/Falco/actions/workflows/build.yml)

```fsharp
open Falco
open Microsoft.AspNetCore.Builder

let wapp = WebApplication.Create()

wapp.Run(Response.ofPlainText "Hello world")
```

[Falco](https://github.com/FalcoFramework/Falco) is a toolkit for building functional-first, full-stack web applications using F#.

- Built on the high-performance components of ASP.NET Core.
- Seamlessly integrates with existing .NET Core middleware and libraries.
- Designed to be simple, lightweight and easy to learn.

## Key Features

- Simple and powerful [routing](documentation/routing.md) API.
- Uniform API for [accessing _any_ request data](documentation/request.md).
- Native F# [view engine](documentation/markup.md).
- Asynchronous [request handling](documentation/response.md).
- [Authentication](documentation/authentication.md) and [security](documentation/cross-site-request-forgery.md) utilities.
- Built-in support for [large uploads](documentation/request.md#multipartform-data-binding) and [binary responses](documentation/response.md#content-disposition).


## Design Goals

- Provide a toolset to build full-stack web application in F#.
- Should be simple, extensible and integrate with existing .NET libraries.
- Can be easily learned.

## Learn

The best way to get started is by visiting the [documentation](https://falcoframework.com/docs). For questions and support please use [discussions](https://github.com/FalcoFramework/Falco/discussions). For chronological updates refer to the [changelog](CHANGELOG.md) is the best place to find chronological updates.

### Related Libraries

- [Falco.Markup](https://github.com/FalcoFramework/Falco.Markup) - an XML markup module primary used as the syntax for [authoring HTML with Falco](https://www.falcoframework.com/docs/markup.html).
- [Falco.Htmx](https://github.com/dpraimeyuu/Falco.Htmx) - a full featured integration with [htmx JS package](https://htmx.org/).
- [Falco.OpenApi](https://github.com/FalcoFramework/Falco.OpenApi) - a library for generating OpenAPI documentation from Falco applications.
- [Falco.Template](https://github.com/FalcoFramework/Falco.Template) - a .NET SDK [project template](https://learn.microsoft.com/en-us/dotnet/core/tools/custom-templates) to help get started with Falco quickly.
- [Falco.UnionRoutes](https://github.com/michaelglass/Falco.UnionRoutes/) - a library for expressing routes as a descriminated union, inspired by Haskell's [Servant](https://docs.servant.dev/).
- [CloudSeed](https://cloudseed.xyz/) - a simple, scalable project boilerplate for F# / .NET.

### Community Projects

- [Falco GraphQL Sample](https://github.com/adelarsq/falco_graphql_sample) - A sample showing how to use GraphQL on Falco using .NET 6.
- [Falco API with Tests Sample](https://github.com/jasiozet/falco-api-with-tests-template) - A sample project using Falco and unit testing.
- [Falco + SQLite + Donald](https://github.com/galassie/FalcoSample) - A demo project using Falco, [Donald](https://github.com/pimbrouwers/Donald), and SQLite
- [FShopOnWeb](https://github.com/NitroDevs/FShopOnWeb) - An adaptation of the classic [ASP.NET Core sample application](https://github.com/dotnet-architecture/eShopOnWeb) using Falco and an F# architecture.

### Articles

- Hamilton Greene - [Spin up a Fullstack F# WebApp in 10 minutes with the CloudSeed Project Template](https://hamy.xyz/blog/2025-01_fsharp-webapp-10-minutes)
- Hamilton Greene - [Why I'm Ditching F# + Giraffe For Falco For Building WebApps](https://hamy.xyz/blog/2025-01_ditching-giraffe-for-falco)
- Istvan - [Running ASP.Net web application with Falco on AWS Lambda](https://dev.l1x.be/posts/2020/12/18/running-asp.net-web-application-with-falco-on-aws-lambda/)

### Videos

- Hamilton Greene - [Build a Fullstack Webapp with F# + Falco](https://www.youtube.com/watch?v=ELPdHdtEIY8)
- Hamilton Greene - [Build a Single-File Web API with F# + Falco](https://www.youtube.com/watch?v=SJCHBqrc3sE)
- Hamilton Greene - [Why I'm Ditching F# + Giraffe For Falco For Building WebApps](https://www.youtube.com/watch?v=tonPeWfu_WM)
- Ben Gobeil - [Why I'm Using Falco Instead Of Saturn | How To Switch Your Backend In SAFE Stack | StonkWatch Ep.13](https://youtu.be/DTy5gIUWvpo)


## Contribute

We kindly ask that before submitting a pull request, you first submit an [issue](https://github.com/FalcoFramework/Falco/issues) or open a [discussion](https://github.com/FalcoFramework/Falco/discussions).

If functionality is added to the API, or changed, please kindly update the relevant [document](/docs). Unit tests must also be added and/or updated before a pull request can be successfully merged.

Only pull requests which pass all build checks and comply with the general coding standard can be approved.

If you have any further questions, submit an [issue](https://github.com/FalcoFramework/Falco/issues) or open a [discussion](https://github.com/FalcoFramework/Falco/discussions) or reach out on [Twitter](https://twitter.com/falco_framework).


## Why "Falco"?

[Kestrel](https://docs.microsoft.com/en-us/aspnet/core/fundamentals/servers/kestrel) has been a game changer for the .NET web stack. In the animal kingdom, "Kestrel" is a name given to several members of the falcon genus. Also known as "Falco".


## Find a bug?

There's an [issue](https://github.com/FalcoFramework/Falco/issues) for that.


## License

Licensed under [Apache License 2.0](https://github.com/FalcoFramework/Falco/blob/master/LICENSE).
