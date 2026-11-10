# Example - Basic REST API

This example demonstrates how to create a basic REST API using Falco. The API will allow users to perform CRUD (Create, Read, Update, Delete) operations on a simple resource, users in this case.

The API will be built using the following components, in addition to the Falco framework:
- [System.Data.SQLite](https://www.nuget.org/packages/System.Data.SQLite/), which provides SQLite support, built and maintained by the SQLite developers.
- [Donald](https://www.nuget.org/packages/Donald/) which simplifies database access, built and maintained by the Falco developers.

> For simplicity, we'll stick to synchronous database access in this example. However, you can easily adapt the code to use asynchronous database access if needed. Specific to SQLite, in many cases it is better to use synchronous access, and let SQLite handle serialization for you.

The code for this example can be found [here](https://github.com/FalcoFramework/Falco/tree/master/examples/BasicRestApi).

## Creating the Application Manually

```shell
> dotnet new falco -o BasicRestApiApp
> cd BasicRestApiApp
> dotnet add package System.Data.SQLite
> dotnet add package Donald
```

## Overview

The API will consist of five endpoints:

- `GET /users`: Retrieve all users.
- `GET /users/{username}`: Retrieve a user by username.
- `POST /users`: Create a new user.
- `POST /users/{username}`: Update an existing user.
- `DELETE /users/{username}`: Delete a user by username.

Users will be stored in a SQLite database, and the API will use Donald to interact with the database. Our user model will be a simple record type with two properties: `Username` and `FullName`.

```fsharp
type User =
    { Username : string
      FullName : string }
```

It's also valuable to have a concrete type to represent API errors. This will be used to return error messages in a consistent format.

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

To implement this we'll use SQLite and a small configuration object to pass in the connection string.

```fsharp
type SqliteConnectionSettings() =
    member val ConnectionString = "" with get, set

type SqliteConnectionFactory(settings : SqliteConnectionSettings) =
    interface IDbConnectionFactory with
        member _.Create() =
            new SQLiteConnection(settings.ConnectionString) :> IDbConnection
```

We'll also define an interface for performing list, create, read and delete operations against a set of entities.

```fsharp
type IStore<'TKey, 'TItem> =
    abstract member List : unit   -> 'TItem list
    abstract member Create : 'TItem -> Result<unit, Error>
    abstract member Read : 'TKey -> 'TItem option
    abstract member Delete : 'TKey -> Result<unit, Error>
```
The `IStore` interface is generic, allowing us to use it with any type of entity. In our case, we'll create a concrete implementation for the `User` entity with `string` as the key type. Our implementation will depend on `IDbConnectionFactory` for creating new database connections. Each method will create its own `IDbConnection` using the provided `IDbConnectionFactory`.

Let's define the `List()` method first, which performs a basic `SELECT` statement, queries for the results and maps them using a locally defined function.

```fsharp
type UserStore(dbConnection : IDbConnectionFactory) =
    let userOfDataReader (rd : IDataReader) =
        { Username = rd.ReadString "username"
          FullName = rd.ReadString "full_name" }

    interface IStore<string, User> with
        member _.List() =
            use conn = dbConnection.Create()
            conn
            |> Db.newCommand "SELECT username, full_name FROM user"
            |> Db.query userOfDataReader

```

Next we'll implement the `Create()` method, which executes an `INSERT` using the provided new user details. In the case of an execution exception, the method will return an `Error` Result, otherwise `Ok`.

```fsharp
type UserStore(dbConnection : IDbConnectionFactory) =
    // .. rest of code

    interface IStore<string, User> with
        // .. rest of code
        member _.Create(user : User) =
            use conn = dbConnection.Create()
            try
                conn
                |> Db.newCommand "
                    INSERT INTO user (username, full_name)
                    SELECT    @username
                            , @full_name
                    WHERE     @username NOT IN (
                                SELECT username FROM user)"
                |> Db.setParams [
                    "username", SqlType.String user.Username
                    "full_name", SqlType.String user.FullName ]
                |> Db.exec
                |> Ok
            with
            | :? DbExecutionException ->
                Error { Code = "FAILED"; Message = "Could not add user" }
```

We won't go over the remaining implementation here, but you can review the full source code [here](https://github.com/FalcoFramework/Falco/tree/master/examples/BasicRestApi).

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

Here you can see our error type in action, which is used to return a JSON response with the error code and message. The signature of the `badRequest` function is a bit different, as it takes an error object as input and returns an `HttpHandler`. The reason for this is that we intend to invoke this function from within our handlers, and we want to be able to pass the error object directly to it.

## Defining the Endpoints

In Falco version 6, the `IFalcoModule` was introduced. It helps organize endpoints and facilitates dependency injection through constructor arguments. Using the `IFalcoModule` is entirely optional, but does have the added advantage of resolving all dependencies at startup instead of runtime (when using `ctx.Plug<T>()`).

```fsharp
type UserModule(userStore : IStore<string, User>) =
    // .. HttpHandlers
    interface IFalcoModule with
        member _.Endpoints = seq {
            // .. HttpEndpoints
        }
```

Next, let's implement the handlers for each of the endpoints. First, we'll implement the `GET /users` endpoint, which retrieves all users from the database.

```fsharp
type UserModule(userStore : IStore<string, User>) =
    let index : HttpHandler = fun ctx ->
        let allUsers = userStore.List()
        Response.ofJson allUsers ctx
```
The `index` function retrieves the `IStore` instance from the dependency container and calls the `List` method to get all users. The result is then returned as a JSON response.

Next, we'll implement the `POST /users` endpoint, which creates a new user.

```fsharp
type UserModule(userStore : IStore<string, User>) =
    // .. HttpHandlers
    let add : HttpHandler = fun ctx -> task {
        let! userJson = Request.getJson<User> ctx
        if String.IsNullOrWhiteSpace(userJson.Username)
           || String.IsNullOrWhiteSpace(userJson.FullName) then
            return! ErrorResponse.badRequest
                { Code = "400"; Message = "Username and Full name are both required" } ctx
        else
            let userAddResponse =
                match userStore.Create userJson with
                | Ok result -> Response.ofJson result
                | Error error -> ErrorResponse.badRequest error

            return! userAddResponse ctx }
```

The `add` function uses the `IStore<string, User>` and calls the `Create` method to add a new user. The result is then returned as a JSON response. If the user creation fails, we return a bad request error.

Next, we'll implement the `GET /users/{username}` endpoint, which retrieves a user by username.

```fsharp
type UserModule(userStore : IStore<string, User>) =
    // .. HttpHandlers
    let view username =
        let userResponse =
            match userStore.Read username with
            | Some user -> Response.ofJson user
            | None -> ErrorResponse.notFound
        userResponse
```

The `view` function uses the `IStore<string, User>` and calls the `Read` method to get a user by username. If the user is found, it is returned as a JSON response. If not, we return a not found error.

Finally, we'll implement the `DELETE /users/{username}` endpoint, which deletes a user by username.

```fsharp
type UserModule(userStore : IStore<string, User>) =
    // ... HttpHandlers ...
    let remove username =
        let response =
            match userStore.Delete username with
            | Ok result -> Response.ofJson result
            | Error error -> ErrorResponse.badRequest error
        response
```

The `remove` function uses the `IStore<string, User>` and calls the `Delete` method to remove a user by username. The result is then returned as a JSON response. If the user deletion fails, we return a bad request error.

## Configuring the Application

Conventionally, you'll configure your database outside of your application scope. For the purpose of this example, we'll define and initialize the database during startup. First we extract the database settings from the configuration (appsettings.json) and make them available through dependency injection. Then we register our SQLite connection factory using these settings, while at the same time initializing the database.

Next we register our `UserStore` as `IStore<string, User>` and our `UserModule`, which enables endpoint discovery.

```fsharp
module Program =
    [<EntryPoint>]
    let main args =
        let bldr = WebApplication.CreateBuilder()

        bldr.Services
            .Configure<SqliteConnectionSettings>(bldr.Configuration.GetSection "Sqlite")
            .AddSingleton<SqliteConnectionSettings>(fun ctx ->
                ctx.GetRequiredService<IOptions<SqliteConnectionSettings>>().Value)
            // ^-- extract the SQLite settings from the configuration
            //     and register in the container
            .AddSingleton<IDbConnectionFactory>(fun ctx ->
                let dbSettings = ctx.GetRequiredService<SqliteConnectionSettings>()
                let db = new SqliteConnectionFactory(dbSettings) :> IDbConnectionFactory
                // setup our db during connection factory initialization
                use conn = db.Create()
                conn
                |> Db.newCommand "CREATE TABLE IF NOT EXISTS user (username, full_name)"
                |> Db.exec
                db)
            // ^-- register the SQLite IDbConnectionFactory implementation
            //     and initialize the database
            .AddSingleton<IStore<string, User>, UserStore>()
            // ^-- register the User store
            .AddSingleton<IFalcoModule, UserModule>()
            // ^-- register the Falco module to expose the endpoints
            |> ignore

        // .. web server build
```

Finally, we need to build and run the application.

```fsharp
module Program =
    [<EntryPoint>]
    let main args =
        // ... database initialization & dependency registration ...
        let wapp = bldr.Build()

        let isDevelopment = wapp.Environment.EnvironmentName = "Development"

        wapp.UseIf(isDevelopment, DeveloperExceptionPageExtensions.UseDeveloperExceptionPage)
            .UseIf(not isDevelopment, FalcoExtensions.UseFalcoExceptionHandler ErrorResponse.serverException)
            .UseRouting()
            .UseFalco()
            .Run(ErrorResponse.notFound)

        0
```

The `UseFalco` method is used to register the endpoints, and the `Run` method is used to handle requests that don't match any of the defined endpoints.

## Wrapping Up

And there you have it! A simple REST API built with Falco, SQLite and Donald. This example demonstrates how to create a basic CRUD API, but you can easily extend it to include more complex functionality, such as authentication, validation, and more.

[Next: Example - Open API](example-open-api.md)
