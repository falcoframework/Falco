module HelloWorld.Program

open Falco
open Falco.Markup
open Falco.Routing
open Microsoft.AspNetCore.Builder
open Microsoft.Extensions.Hosting

/// GET /
let handlePlainText : HttpHandler =
    Response.ofPlainText "Hello world"

/// GET /json
let handleJson : HttpHandler =
    let message = {| Message = "Hello from /json" |}
    Response.ofJson message

/// GET /html
let handleHtml : HttpHandler =
    let html =
        Templates.html5 "en"
            [ Elem.link [ Attr.href "style.css"; Attr.rel "stylesheet" ] ]
            [ Elem.h1 [] [ Text.raw "Hello from /html" ] ]

    Response.ofHtml html

/// GET /greet/{name}
let handleGreet : HttpHandler = fun ctx ->
    let route = Request.getRoute ctx
    let greeting = sprintf "Hello %s" (route.Get "name")
    Response.ofPlainText greeting ctx

[<EntryPoint>]
let main args =
    let bldr = WebApplication.CreateBuilder(args)
    let wapp = bldr.Build()
    wapp.UseStaticFiles()
        .UseFalco([
            get "/" handlePlainText
            get "/json" handleJson
            get "/html" handleHtml
            get "/greet/{name}" handleGreet
        ]) |> ignore
    wapp.Run()
    0 // Exit code
