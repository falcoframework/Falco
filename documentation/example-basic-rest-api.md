# Example - Basic REST API

This example demonstrates how to create a basic REST API using Falco. The API will allow users to perform CRUD (Create, Read, Update, Delete) operations on a simple resource, users in this case.

The API will be built using the following components, in addition to the Falco framework:
- [System.Data.SQLite](https://www.nuget.org/packages/System.Data.SQLite/), which provides SQLite support, built and maintained by the SQLite developers.
- [Donald](https://www.nuget.org/packages/Donald/) which simplifies database access, built and maintained by the Falco developers.

> For simplicity, we'll stick to sychronous database access in this example. However, you can easily adapt the code to use asynchronous database access if needed. Specific to SQLite, in many cases it is better to use synchronous access, and let SQLite handle serialization for you.

The code for this example can be found [here](https://github.com/FalcoFramework/Falco/tree/master/examples/BasicRestApi).

## Creating the Application Manually

```shell
> dotnet new falco -o BasicRestApiApp
> cd BasicRestApiApp
> dotnet add package System.Data.SQLite
> dotnet add package Donald
```

## Overview

The API will consist of four endpoints:

- `GET /users`: Retrieve all users.
- `GET /users/{username}`: Retrieve a user by username.
- `POST /users`: Create a new user.
- `DELETE /users/{username}`: Delete a user by username.

Users will be stored in a SQLite database, and the API will use Donald to interact with the database. Our user model will be a simple record type with two properties: `Username` and `Full Name`.

```fsharp
type User =
    { Username : string
      FullName : string }
```

It's also valueable to have a concrete type to represent API errors. This will be used to return error messages in a consistent format.

```fsharp
type Error =
    { Code : string
      Message : string }
```

## Data Access

To interact with the SQLite database, we'll create some abstractions for establishing new connections and performing database operations.

A connection factory is a useful concept to avoid passing around connection strings. It allows us to create new connections without needing to know the details of how they are created.

```fsharp
type IDbConnectionFactory =
    abstract member Create : unit -> IDbConnection
```

We'll also define an interface for performing list, create, read and delete operations against a set of entities.

```fsharp
type IStore<'TKey, 'TItem> =
    abstract member List : unit   -> 'TItem list
    abstract member Create : 'TItem -> Result<unit, Error>
    abstract member Read : 'TKey -> 'TItem option
    abstract member Delete : 'TKey -> Result<unit, Error>
```
The `IStore` interface is generic, allowing us to use it with any type of entity. In our case, we'll create a concrete implementation for the `User` entity.

## Implementing the Store

## Error Responses

The API will return error responses in a consistent format. To do this, we'll create three functions for the common error cases: `notFound`, `badRequest`, and `serverException`.

```fsharp
module ErrorResponse =
    let badRequest error : HttpHandler =
        Response.withStatusCode 400
        >> Response.ofJson error

    let notFound : HttpHandler =
        Response.withStatusCode 404 >>
        Response.ofJson { Code = "404"; Message = "Not Found" }

    let serverException : HttpHandler =
        Response.withStatusCode 500 >>
        Response.ofJson { Code = "500"; Message = "Server Error" }
```

Here you can see our error type in action, which is used to return a JSON response with the error code and message. The signature of the `badRequest` function is a bit different, as it takes an error object as input and returns a `HttpHandler`. The reason for this is that we intend to invoke this function from within our handlers, and we want to be able to pass the error object directly to it.

## Defining the Endpoints

It can be very useful to define values for the endpoints we want to expose. This allows us to easily change the endpoint paths in one place if needed, and also provides intellisense support when using the endpoints in our code.

```fsharp
module Route =
    let userIndex = "/users"
    let userAdd = "/users"
    let userView = "/users/{username}"
    let userRemove = "/users/{username}"
```

Next, let's implement the handlers for each of the endpoints. First, we'll implement the `GET /users` endpoint, which retrieves all users from the database.

```fsharp
module UserEndpoint =
    let index : HttpHandler = fun ctx ->
        let userStore = ctx.Plug<IStore<string, User>>()
        let allUsers = userStore.List()
        Response.ofJson allUsers ctx
```
The `index` function retrieves the `IStore` instance from the dependency container and calls the `List` method to get all users. The result is then returned as a JSON response.

Next, we'll implement the `POST /users` endpoint, which creates a new user.

```fsharp
module UserEndpoint =
    // ... index handler ...
    let add : HttpHandler = fun ctx -> task {
        let userStore = ctx.Plug<IStore<string, User>>()
        let! userJson = Request.getJson<User> ctx
        let userAddResponse =
            match userStore.Create(userJson) with
            | Ok result -> Response.ofJson result ctx
            | Error error -> ErrorResponse.badRequest error ctx
        return! userAddResponse }
```

The `add` function retrieves the `IStore` instance from the dependency container and calls the `Create` method to add a new user. The result is then returned as a JSON response. If the user creation fails, we return a bad request error.

Next, we'll implement the `GET /users/{username}` endpoint, which retrieves a user by username.

```fsharp
module UserEndpoint =
    // ... index and add handlers ...
    let view : HttpHandler = fun ctx ->
        let userStore = ctx.Plug<IStore<string, User>>()
        let route = Request.getRoute ctx
        let username = route?username.AsString()
        match userStore.Read(username) with
        | Some user -> Response.ofJson user ctx
        | None -> ErrorResponse.notFound ctx
```

The `view` function retrieves the `IStore` instance from the dependency container and calls the `Read` method to get a user by username. If the user is found, it is returned as a JSON response. If not, we return a not found error.

Finally, we'll implement the `DELETE /users/{username}` endpoint, which deletes a user by username.

```fsharp
module UserEndpoint =
    // ... index, add and view handlers ...
    let remove : HttpHandler = fun ctx ->
        let userStore = ctx.Plug<IStore<string, User>>()
        let route = Request.getRoute ctx
        let username = route?username.AsString()
        match userStore.Delete(username) with
        | Ok result -> Response.ofJson result ctx
        | Error error -> ErrorResponse.badRequest error ctx
```

The `remove` function retrieves the `IStore` instance from the dependency container and calls the `Delete` method to remove a user by username. The result is then returned as a JSON response. If the user deletion fails, we return a bad request error.

## Configuring the Application

Conventionally, you'll configure your database outside of your application scope. For the purpose of this example, we'll define and initialize the database during startup.

```fsharp
module Program =
    [<EntryPoint>]
    let main args =
        let dbConnectionFactory =
            { new IDbConnectionFactory with
                member _.Create() = new SQLiteConnection("Data Source=BasicRestApi.sqlite3") }

        let initializeDatabase (dbConnection : IDbConnectionFactory) =
            use conn = dbConnection.Create()
            conn
            |> Db.newCommand "CREATE TABLE IF NOT EXISTS user (username, full_name)"
            |> Db.exec

        initializeDatabase dbConnectionFactory

        // ... rest of the application setup
```

First we implement the `IDbConnectionFactory` interface, which creates a new SQLite connection. Then we define a `initializeDatabase` function, which creates the database and the user table if it doesn't exist. We encapsulate the database initialization in a function, so we can quickly dispose of the connection after use.

Next, we need to register our database connection factory and the `IStore` implementation in the dependency container.

```fsharp
module Program =
    [<EntryPoint>]
    let main args =
        // ... database initialization ...
        let bldr = WebApplication.CreateBuilder(args)

        bldr.Services
            .AddAntiforgery()
            .AddScoped<IDbConnectionFactory>(dbConnectionFactory)
            .AddScoped<IStore<string, User>, UserStore>()
            |> ignore
```

Finally, we need to configure the application to use the defined endpoints.

```fsharp
module Program =
    [<EntryPoint>]
    let main args =
        // ... database initialization & dependency registration ...
        let wapp = bldr.Build()

        let isDevelopment = wapp.Environment.EnvironmentName = "Development"

        wapp.UseIf(isDevelopment, DeveloperExceptionPageExtensions.UseDeveloperExceptionPage)
            .UseIf(not(isDevelopment), FalcoExtensions.UseFalcoExceptionHandler ErrorResponse.serverException)
            .UseRouting()
            .UseFalco(App.endpoints)
            .Run(ErrorResponse.notFound)

        0 // Exit code
```

The `UseFalco` method is used to register the endpoints, and the `Run` method is used to handle requests that don't match any of the defined endpoints.

## Wrapping Up

And there you have it! A simple REST API built with Falco, SQLite and Donald. This example demonstrates how to create a basic CRUD API, but you can easily extend it to include more complex functionality, such as authentication, validation, and more.

[Next: Example - Open API](example-open-api.md)
