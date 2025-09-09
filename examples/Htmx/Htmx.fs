open System
open Falco
open Falco.Markup
open Falco.Routing
open Falco.Htmx
open Microsoft.AspNetCore.Builder

module View =
    let template content =
        _html [ _lang_ "en" ] [
            _head [] [
                _script [ _src_ HtmxScript.cdnSrc ] [] ]
            _body [] content ]

    module Components =
        let clicker =
            _button [
                Hx.get "/click"
                Hx.swapInnerHtml
                Hx.targetCss "#content" ]
                [ _text "Click Me" ]

        let resetter =
            Elem.createFragment [
                _h2' "Way to go! You clicked it!"
                _br []
                _button [
                    Hx.get "/reset"
                    Hx.swapInnerHtml
                    Hx.targetCss "#content" ]
                    [ _text "Reset" ] ]

module App =
    let handleIndex : HttpHandler =
        let html =
            View.template [
                _h1' "Example: Click & Swap"
                _div [ _id_ "content" ] [
                    View.Components.clicker ] ]

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
