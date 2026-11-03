open Falco
open Falco.Routing
open Microsoft.AspNetCore.Builder
open Microsoft.Extensions.DependencyInjection

type IGreeter =
    abstract member Greet : name : string -> string

type FriendlyGreeter() =
    interface IGreeter with
        member _.Greet(name : string) =
            $"Hello {name} 😀"

type GreetingModule(greeter : IGreeter) =
                    // ^-- the IGreeter is provided automatically
    let detail name =
        let greeting = greeter.Greet name
        Response.ofPlainText greeting

    let mapName (r : RequestData) =
        r?name.AsStringNonEmpty "World"

    interface IFalcoModule with
        member _.Endpoints = seq {
            mapGet "/{name?}" mapName detail
        }

let bldr = WebApplication.CreateBuilder()
// ^-- create a configurable web application builder

bldr.Services
    .AddSingleton<IGreeter, FriendlyGreeter>()
    // ^-- register the greeter as singleton in the container
    .AddSingleton<IFalcoModule, GreetingModule>()
    // ^-- register the Falco module to expose the endpoint
    |> ignore

let wapp = bldr.Build()
// ^-- manifest our WebApplication

wapp.UseRouting()
    .UseFalco()
    .Run()
