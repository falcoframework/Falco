open System
open Falco
open Falco.Markup
open Falco.Routing
open Falco.Htmx
open Microsoft.AspNetCore.Builder

module View =
    let template content =
        Elem.html [ Attr.lang "en" ] [
            Elem.head [] [
                Elem.script [ Attr.src HtmxScript.cdnSrc ] [] ]
            Elem.body []
                content ]

    module Components =
        let clicker =
            Elem.button
                [ Hx.get "/click"
                  Hx.swapOuterHtml ]
                [ Text.raw "Click Me" ]

        let resetter =
            Elem.div [ Attr.id "wrapper" ] [
                Text.h2 "Way to go! You clicked it!"
                Elem.br []
                Elem.button
                    [ Hx.get "/reset"
                      Hx.swapOuterHtml
                      Hx.targetCss "#wrapper" ]
                    [ Text.raw "Reset" ] ]

module App =
    let handleIndex : HttpHandler =
        let html =
            View.template [
                Text.h1 "Example: Click & Swap"
                View.Components.clicker ]

        Response.ofHtml html

    let handleClick : HttpHandler =
        Response.ofHtml View.Components.resetter

    let handleReset : HttpHandler =
        Response.ofHtml View.Components.clicker

    let endpoints =
        [
            get "/" handleIndex
            get "/click" handleClick
            get "/reset" handleReset
        ]

[<EntryPoint>]
let main args =
    let wapp = WebApplication.Create()


    wapp.UseRouting()
        .UseFalco(App.endpoints)
        .Run()
    0 // Exit code
