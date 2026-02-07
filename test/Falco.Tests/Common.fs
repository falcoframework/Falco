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

let AuthRoles = ["admin"; "user"]

type TestingHandlerOptions() =
  inherit AuthenticationSchemeOptions()

type TestDenyAuthHandler(options, logger, encoder, clock) =
  inherit AuthenticationHandler<TestingHandlerOptions>(options, logger, encoder, clock)

  member val AuthenticateResult : AuthenticateResult = AuthenticateResult.NoResult() with get, set

  override _.HandleAuthenticateAsync() =
      Task.FromResult(AuthenticateResult.NoResult())

  override x.HandleChallengeAsync properties =
      x.Context.Response.StatusCode <- 401
      x.Context.Response.Headers.SetCommaSeparatedValues(HeaderNames.WWWAuthenticate, AuthScheme)

      if not (String.IsNullOrEmpty properties.RedirectUri) then
        x.Context.Response.Headers.Add(HeaderNames.Location, properties.RedirectUri)
      Task.CompletedTask

type TestAllowAuthHandler(options, logger, encoder, clock) =
  inherit AuthenticationHandler<TestingHandlerOptions>(options, logger, encoder, clock)

  member val AuthenticateResult : AuthenticateResult = AuthenticateResult.NoResult() with get, set

  override _.HandleAuthenticateAsync() =
    let claims = AuthRoles |> List.map (fun role -> Claim(ClaimTypes.Role, role))
    let identity = ClaimsIdentity(claims, AuthScheme)
    let principal = ClaimsPrincipal identity
    Task.FromResult(AuthenticateResult.Success(AuthenticationTicket(principal, AuthScheme)))

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
    antiforgery.IsRequestValidAsync(ctx).Returns(Task.FromResult(true)) |> ignore

    let serviceCollection = ServiceCollection()

    serviceCollection
        .AddLogging()
        .AddAuthorization()
        .AddSingleton<IAntiforgery> antiforgery |> ignore

    if authenticated then
        serviceCollection
            .AddAuthentication()
            .AddScheme<TestingHandlerOptions, TestAllowAuthHandler>(AuthScheme, ignore)
    else
        serviceCollection
            .AddAuthentication()
            .AddScheme<TestingHandlerOptions, TestDenyAuthHandler>(AuthScheme, ignore)
    |> ignore

    let provider = serviceCollection.BuildServiceProvider()

    ctx.Request.Returns req |> ignore
    ctx.Response.Returns resp |> ignore
    ctx.RequestServices.Returns provider |> ignore

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
