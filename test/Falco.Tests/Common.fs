[<AutoOpen>]
module Falco.Tests.Common

#nowarn "44"

open System
open System.IO
open System.IO.Pipelines
open System.Security.Claims
open System.Threading.Tasks
open FsUnit.Xunit
open Microsoft.AspNetCore.Antiforgery
open Microsoft.AspNetCore.Authentication
open Microsoft.AspNetCore.Authentication.Cookies
open Microsoft.AspNetCore.Http
open Microsoft.AspNetCore.Routing
open Microsoft.Extensions.DependencyInjection
open Microsoft.Net.Http.Headers
open NSubstitute
open System.Collections.Generic

let shouldBeSome pred (option : Option<'a>) =
    match option with
    | Some o -> pred o
    | None   -> sprintf "Should not be None" |> should equal false

let shouldBeNone (option : Option<'a>) =
    match option with
    | Some o -> sprintf "Should not be Some" |> should equal false
    | None   -> ()

[<CLIMutable>]
type FakeRecord = { Name : string }

let getResponseBody (ctx : HttpContext) =
    task {
        ctx.Response.Body.Position <- 0L
        use reader = new StreamReader(ctx.Response.Body)
        return! reader.ReadToEndAsync()
    }

[<Literal>]
let AuthScheme = "Testing"

let CookieScheme = CookieAuthenticationDefaults.AuthenticationScheme

let AuthRoles = ["admin"; "user"]

let getHttpContextWriteable (authenticated : bool) =
    let ctx = Substitute.For<HttpContext>()

    let req = Substitute.For<HttpRequest>()
    req.Headers.Returns(Substitute.For<HeaderDictionary>()) |> ignore
    req.RouteValues.Returns(Substitute.For<RouteValueDictionary>()) |> ignore

    let resp = Substitute.For<HttpResponse>()
    let respBody = new MemoryStream()
    resp.Headers.Returns(Substitute.For<HeaderDictionary>()) |> ignore
    resp.BodyWriter.Returns(PipeWriter.Create respBody) |> ignore
    resp.Body <- respBody

    let antiforgery = Substitute.For<IAntiforgery>()
    antiforgery.GetAndStoreTokens(Arg.Any<HttpContext>()).Returns(
        AntiforgeryTokenSet("requestToken", "cookieToken", "formFieldName", "headerName")
    ) |> ignore
    antiforgery.IsRequestValidAsync(ctx).Returns(Task.FromResult(true)) |> ignore

    let authService = Substitute.For<IAuthenticationService>()

    let claims = AuthRoles |> List.map (fun role -> Claim(ClaimTypes.Role, role))
    let identity = ClaimsIdentity(claims, AuthScheme)
    let principal = ClaimsPrincipal identity
    let authResult =
        if authenticated
        then AuthenticateResult.Success(AuthenticationTicket(principal, AuthScheme))
        else AuthenticateResult.NoResult()

    authService.AuthenticateAsync(Arg.Any<HttpContext>(), Arg.Any<string>())
        .Returns(Task.FromResult(authResult)) |> ignore

    authService.SignInAsync(Arg.Any<HttpContext>(), Arg.Any<string>(), Arg.Any<ClaimsPrincipal>(), Arg.Any<AuthenticationProperties>())
        .Returns(Task.CompletedTask) |> ignore

    authService.SignOutAsync(Arg.Any<HttpContext>(), Arg.Any<string>(), Arg.Any<AuthenticationProperties>())
        .Returns(Task.CompletedTask) |> ignore

    authService.ChallengeAsync(Arg.Any<HttpContext>(), Arg.Any<string>(), Arg.Any<AuthenticationProperties>())
        .Returns(fun args ->
            let ctx = args.Arg<HttpContext>()
            let scheme = args.Arg<string>()
            Task.CompletedTask
        ) |> ignore

    let serviceCollection = ServiceCollection()

    serviceCollection
        .AddLogging()
        .AddAuthorization()
        .AddSingleton<IAuthenticationService>(authService)
        .AddSingleton<IAntiforgery>(antiforgery)
        |> ignore

    if authenticated then
        serviceCollection
            .AddAuthentication(AuthScheme)
            .AddCookie(CookieScheme)
    else
        serviceCollection
            .AddAuthentication(AuthScheme)
            .AddCookie(CookieScheme)
    |> ignore

    let provider = serviceCollection.BuildServiceProvider()

    ctx.Request.Returns req |> ignore
    ctx.Response.Returns resp |> ignore
    ctx.RequestServices
        .GetService(Arg.Any<Type>())
        .Returns(fun args ->
            let serviceType = args.Arg<Type>()
            provider.GetService(serviceType)
        ) |> ignore

    ctx

let cookieCollection cookies =
  { new IRequestCookieCollection with
    member __.ContainsKey(key: string) = Map.containsKey key cookies
    member __.Count = Map.count cookies
    member __.GetEnumerator() = (Map.toSeq cookies |> Seq.map KeyValuePair).GetEnumerator()
    member __.GetEnumerator() = __.GetEnumerator() :> Collections.IEnumerator
    member __.Item with get (key: string): string = Map.find key cookies
    member __.Keys = Map.toSeq cookies |> Seq.map fst |> ResizeArray :> Collections.Generic.ICollection<string>
    member __.TryGetValue(key: string, value: byref<string>): bool =
      match Map.tryFind key cookies with
      | Some _ -> true
      | _ -> false }
