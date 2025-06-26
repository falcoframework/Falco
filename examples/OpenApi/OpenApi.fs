open Falco
open Falco.OpenApi
open Falco.Routing
open Microsoft.AspNetCore.Builder
open Microsoft.Extensions.DependencyInjection
open Microsoft.Extensions.Hosting

type FortuneInput =
    { Name : string }

type Fortune =
    { Description : string }

module Fortune =
    let create age input =
        match age with
        | Some age when age > 0 ->
            { Description = $"{input.Name}, you will experience great success when you are {age + 3}." }
        | _ ->
            { Description = $"{input.Name}, your future is unclear." }

[<EntryPoint>]
let main args =
    let bldr = WebApplication.CreateBuilder(args)

    bldr.Services
        .AddFalcoOpenApi()
        .AddSwaggerGen()
        |> ignore

    let wapp = bldr.Build()

    wapp.UseHttpsRedirection()
        .UseSwagger()
        .UseSwaggerUI()
    |> ignore

    let endpoints =
        [
            mapPost "/fortune"
                (fun r -> r?age.AsIntOption())
                (fun ageOpt ->
                    Request.mapJson<FortuneInput> (Fortune.create ageOpt >> Response.ofJson))
                |> OpenApi.name "Fortune"
                |> OpenApi.summary "A mystic fortune teller"
                |> OpenApi.description "Get a glimpse into your future, if you dare."
                |> OpenApi.query [
                    { Type = typeof<int>; Name = "Age"; Required = false } ]
                |> OpenApi.acceptsType typeof<FortuneInput>
                |> OpenApi.returnType typeof<Fortune>
        ]

    wapp.UseRouting()
        .UseFalco(endpoints)
        .Run()

    0
