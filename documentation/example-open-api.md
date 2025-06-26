# Example - Open API

Open API is a specification for defining APIs in a machine-readable format. It allows developers to describe the structure of their APIs, including endpoints, request/response formats, and authentication methods.

[Falco.OpenAPI](https://github.com/FalcoFramework/Falco.OpenAPI) is a library for generating OpenAPI documentation for Falco applications. It provides a set of combinators for annotating Falco routes with OpenAPI metadata, which can be used to generate OpenAPI documentation.

We'll dial back the complexity a bit from the [Basic REST API](example-basic-rest-api.md) example and create a simple "fortune teller" Falco application that serves OpenAPI documentation.

The code for this example can be found [here](https://github.com/FalcoFramework/Falco/tree/master/examples/OpenApi).

## Creating the Application Manually

```shell
> dotnet new falco -o OpenApiApi
> cd OpenApiApp
> dotnet add package Falco.OpenApi
```

## Fortunes

Our fortune teller will return fortune for the name of the person specified. To model this, we'll create two simple record types.

```fsharp
type FortuneInput =
    { Name : string }

type Fortune =
    { Description : string }
```

For simplicity, we'll use a static member to return a fortune. In a real application, you would likely retrieve this from a database or an external service.

```fsharp
module Fortune =
    let create age input =
        match age with
        | Some age when age > 0 ->
            { Description = $"{input.Name}, you will experience great success when you are {age + 3}." }
        | _ ->
            { Description = $"{input.Name}, your future is unclear." }
```

## OpenAPI Annotations

Next, we'll annotate our route with OpenAPI metadata. This is done using the `OpenApi` module from the `Falco.OpenAPI` package. Below is the startup code for our fortune teller application. We'll dissect it after the code block, and then add the OpenAPI annotations.

```fsharp
[<EntryPoint>]
let main args =
    let bldr = WebApplication.CreateBuilder(args)

    bldr.Services
        .AddFalcoOpenApi()
        // ^-- add OpenAPI services
        .AddSwaggerGen()
        // ^-- add Swagger services
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
                // we'll add OpenAPI annotations here
        ]

    wapp.UseRouting()
        .UseFalco(endpoints)
        .Run()

    0
```

We've created a simple Falco application that listens for POST requests to the `/fortune` endpoint. The request body is expected to be a JSON object with a `name` property. The response will be a JSON object with a `description` property.

Now, let's add the OpenAPI annotations to our route.

```fsharp
[<EntryPoint>]
let main args =
    // ... application setup code ...
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

    // ... application startup code ...

    0 // Exit code
```

In the code above, we use the `OpenApi` module to annotate our route with metadata.

Here's a breakdown of the annotations:
- `OpenApi.name`: Sets the name of the operation.
- `OpenApi.summary`: Provides a short summary of the operation.
- `OpenApi.description`: Provides a detailed description of the operation.
- `OpenApi.query`: Specifies the query parameters for the operation. In this case, we have an optional `age` parameter.
- `OpenApi.acceptsType`: Specifies the expected request body type. In this case, we expect a JSON object that can be deserialized into a `FortuneInput` record.
- `OpenApi.returnType`: Specifies the response type. In this case, we return a JSON object that can be serialized into a `Fortune` record.

## Wrapping Up

That's it! You've successfully created a simple Falco application with OpenAPI documentation. You can now use the generated OpenAPI specification to generate client code, create API documentation, or integrate with other tools that support OpenAPI.

[Next: Example - htmx](example-htmx.md)
