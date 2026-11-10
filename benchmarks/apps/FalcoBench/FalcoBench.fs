open System
open Falco
open Falco.Markup
open Falco.Routing
open Microsoft.AspNetCore.Builder
open Microsoft.Extensions.Logging

let bldr = WebApplication.CreateSlimBuilder()
bldr.Logging.ClearProviders() |> ignore

let wapp = bldr.Build()

let template (dt : DateTime) =
    _html [] [
        _head [] [
            _title [] [ _text "Hello" ]
        ]
        _body [] [
            _h1' "Hello World!"
            _p' (sprintf "Current time is %s" (dt.ToString "R"))
        ]
    ]

wapp.UseRouting()
    .UseFalco([
        get "/" (fun ctx -> Response.ofHtml (template DateTime.UtcNow) ctx)
    ])
    .Run(Response.ofPlainText "Not found")
