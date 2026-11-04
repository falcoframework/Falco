namespace BasicRestApi

open System
open System.Data
open Donald
// ^-- external package that makes using databases simpler
open Falco
open Falco.Routing
open Microsoft.AspNetCore.Builder
open Microsoft.Extensions.DependencyInjection
open Microsoft.Extensions.Options
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

type SqliteConnectionSettings() =
    member val ConnectionString = "" with get, set

type SqliteConnectionFactory(settings : SqliteConnectionSettings) =
    interface IDbConnectionFactory with
        member _.Create() =
            new SQLiteConnection(settings.ConnectionString) :> IDbConnection

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
    let badRequest error =
        Response.withStatusCode 400
        >> Response.ofJson error

    let notFound =
        Response.withStatusCode 404 >>
        Response.ofJson { Code = "404"; Message = "Not Found" }

    let serverException =
        Response.withStatusCode 500 >>
        Response.ofJson { Code = "500"; Message = "Server Error" }

type UserModule(userStore : IStore<string, User>) =
    let describe =
        dict [
            "GET /users", "Lists all users"
            "GET /users/{username}", "Get user by username"
            "POST /users", "Add user (required fields: username)"
            "DELETE /users/{username}", "Delete user by username"
        ]
        |> Response.ofJson

    let index : HttpHandler = fun ctx ->
        let allUsers = userStore.List()
        Response.ofJson allUsers ctx

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

    let view username =
        let userResponse =
            match userStore.Read username with
            | Some user -> Response.ofJson user
            | None -> ErrorResponse.notFound
        userResponse

    let remove username =
        let response =
            match userStore.Delete username with
            | Ok result -> Response.ofJson result
            | Error error -> ErrorResponse.badRequest error
        response

    let mapName (r : RequestData) =
        r?username.AsString()

    interface IFalcoModule with
        member _.Endpoints = seq {
            get "/" describe
            get "/users" index
            post "/users" add
            mapGet "/users/{username}" mapName view
            mapDelete "/users/{username}" mapName remove
        }

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

        let wapp = bldr.Build()

        let isDevelopment = wapp.Environment.EnvironmentName = "Development"

        wapp.UseIf(isDevelopment, DeveloperExceptionPageExtensions.UseDeveloperExceptionPage)
            .UseIf(not isDevelopment, FalcoExtensions.UseFalcoExceptionHandler ErrorResponse.serverException)
            .UseRouting()
            .UseFalco()
            .Run(ErrorResponse.notFound)

        0
