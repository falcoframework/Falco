namespace BasicRestApi

open System.Data
open Donald
// ^-- external package that makes using databases simpler
open Falco
open Falco.Routing
open Microsoft.AspNetCore.Builder
open Microsoft.Extensions.DependencyInjection
open System.Data.SQLite
// ^-- official SQLite package

type Error =
    { Code : string
      Message : string }

type User =
    { Username : string
      FullName : string }

type IDbConnectionFactory =
    abstract member Create : unit -> IDbConnection

type IStore<'TKey, 'TItem> =
    abstract member List : unit   -> 'TItem list
    abstract member Create : 'TItem -> Result<unit, Error>
    abstract member Read : 'TKey -> 'TItem option
    abstract member Delete : 'TKey -> Result<unit, Error>

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

        member _.Read(username : string) =
            use conn = dbConnection.Create()
            conn
            |> Db.newCommand "
                SELECT    username
                        , full_name
                FROM      user
                WHERE     username = @username"
            |> Db.setParams [ "username", SqlType.String username ]
            |> Db.querySingle userOfDataReader

        member _.Delete(username : string) =
            use conn = dbConnection.Create()
            try
                conn
                |> Db.newCommand "DELETE FROM user WHERE username = @username"
                |> Db.setParams [ "username", SqlType.String username ]
                |> Db.exec
                |> Ok
            with
            | :? DbExecutionException ->
                Error { Code = "FAILED"; Message = "Could not add user" }

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

module Route =
    let userIndex = "/users"
    let userAdd = "/users"
    let userView = "/users/{username}"
    let userRemove = "/users/{username}"

module UserEndpoint =
    let index : HttpHandler = fun ctx ->
        let userStore = ctx.Plug<IStore<string, User>>()
        let allUsers = userStore.List()
        Response.ofJson allUsers ctx

    let add : HttpHandler = fun ctx -> task {
        let userStore = ctx.Plug<IStore<string, User>>()
        let! userJson = Request.getJson<User> ctx
        let userAddResponse =
            match userStore.Create(userJson) with
            | Ok result -> Response.ofJson result ctx
            | Error error -> ErrorResponse.badRequest error ctx
        return! userAddResponse }

    let view : HttpHandler = fun ctx ->
        let userStore = ctx.Plug<IStore<string, User>>()
        let route = Request.getRoute ctx
        let username = route?username.AsString()
        match userStore.Read(username) with
        | Some user -> Response.ofJson user ctx
        | None -> ErrorResponse.notFound ctx

    let remove : HttpHandler = fun ctx ->
        let userStore = ctx.Plug<IStore<string, User>>()
        let route = Request.getRoute ctx
        let username = route?username.AsString()
        match userStore.Delete(username) with
        | Ok result -> Response.ofJson result ctx
        | Error error -> ErrorResponse.badRequest error ctx

module App =
    let endpoints =
        [ get Route.userIndex UserEndpoint.index
          post Route.userAdd UserEndpoint.add
          get Route.userView UserEndpoint.view
          delete Route.userRemove UserEndpoint.remove ]

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

        let bldr = WebApplication.CreateBuilder(args)

        bldr.Services
            .AddAntiforgery()
            .AddSingleton<IDbConnectionFactory>(dbConnectionFactory)
            .AddScoped<IStore<string, User>, UserStore>()
            |> ignore

        let wapp = bldr.Build()

        let isDevelopment = wapp.Environment.EnvironmentName = "Development"

        wapp.UseIf(isDevelopment, DeveloperExceptionPageExtensions.UseDeveloperExceptionPage)
            .UseIf(not(isDevelopment), FalcoExtensions.UseFalcoExceptionHandler ErrorResponse.serverException)
            .UseRouting()
            .UseFalco(App.endpoints)
            .Run(ErrorResponse.notFound)

        0
