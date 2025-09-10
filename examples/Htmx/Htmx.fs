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

    let clickAndSwap =
        template [
            _h1' "Example: Click & Swap"
            _div [ _id_ "content" ] [
                _button [
                    _id_ "clicker"
                    Hx.get "/click"
                    Hx.swapOuterHtml ]
                    [ _text "Click Me" ] ] ]

    module Components =
        let resetter =
            _div [ _id_ "resetter" ] [
                _h2' "Way to go! You clicked it!"
                _br []
                _button [
                    Hx.get "/reset"
                    Hx.swapOuterHtml
                    Hx.targetCss "#resetter" ]
                    [ _text "Reset" ] ]


module App =
    let handleIndex : HttpHandler =
        Response.ofHtml View.clickAndSwap

    let handleClick : HttpHandler =
        Response.ofHtml View.Components.resetter

    let handleReset : HttpHandler =
        Response.ofFragment "clicker" View.clickAndSwap

    let endpoints =
        [
            get "/" handleIndex
            get "/click" handleClick
            get "/reset" handleReset
        ]

let wapp = WebApplication.Create()

wapp.UseRouting()
    .UseFalco(App.endpoints)
    .Run()
