# Request Handling

Falco exposes a __uniform API__ to obtain typed values from `IFormCollection`, `IQueryCollection`, `RouteValueDictionary`, `IHeaderCollection`, and `IRequestCookieCollection`. This is achieved by means of the `RequestData` type and it's derivative `FormData`, abstractions intended to make it easier to work with the url-encoded key/value collections.

> Take note of the similarities when interacting with the different sources of request data.

## Key/Value Structure

`RequestData` is supported by a recursive discriminated union called `RequestValue` which represents a parsed key/value collection.

The `RequestValue` parsing process provides some simple, yet powerful, syntax to submit objects and collections over-the-wire, to facilitate complex form and query submissions. 

### Key Syntax: Object Notation

Keys using dot notation are interpreted as complex (i.e., nested values) objects. 

### Key Syntax: List Notation

Keys using square bracket notation are interpreted as lists, which includes both primitives and [complex objects](#key-syntax-object-notation).

## API Semantics

`RequestData` provides the ability to safely read primitive types from flat and nested key/value collections.

```fsharp
let requestData : RequestData = // From Route | Query | Form

// Retrieve primitive options
let str : string option = requestData.TryGetString "name" 
let flt : float option = requestData.TryGetFloat "temperature"

// Retrieve primitive, or default
let str : string = requestData.GetString "name"
let strOrDefault : string = requestData.GetString ("name", "John Doe")
let flt : float = requestData.GetFloat "temperature"

// Retrieve primitive list 
let strList : string list = requestData.GetStringList "hobbies"
let grades : int list = requestData.GetInt32List "grades"

// Dynamic access, useful for nested/complex collections
// Equivalent to:
// requestData.Get("user").Get("email_address").AsString()
let userEmail = requestData?user?email_address.AsString()

```

## Route Binding

Provides access to the values found in the `RouteValueDictionary`. 

```fsharp
open Falco

// Assuming a route pattern of /{Name}
let manualRouteHandler : HttpHandler = fun ctx ->
    let r = Request.getRoute ctx
    // Or, let name = r?Name.AsString()
    // Or, let name = r.TryGetString "Name" |> Option.defaultValue ""
    let name = r.GetString "Name"
    Response.ofPlainText name ctx

let mapRouteHandler : HttpHandler =
    Request.mapRoute (fun r ->
        r.GetString "Name")
        Response.ofPlainText

```

## Query Binding

Provides access to the values found in the `IQueryCollection`, as well as the `RouteValueDictionary`. In the case of matching keys, the values in the `IQueryCollection` take precedence.

```fsharp
open Falco

type Person =
    { FirstName : string
      LastName : string }

let form : HttpHandler =
    Response.ofHtmlCsrf view

let manualQueryHandler : HttpHandler = fun ctx ->
    let q = Request.getQuery ctx

    let person =
        { FirstName = q.GetString ("FirstName", "John") // Get value or return default value
          LastName  = q.GetString ("LastName", "Doe") }

    Response.ofJson person ctx

let mapQueryHandler : HttpHandler =
    Request.mapQuery (fun q ->
        let first = q.GetString ("FirstName", "John") // Get value or return default value
        let last = q.GetString ("LastName", "Doe")
        { FirstName = first; LastName = last })
        Response.ofJson
```

## Form Binding

Provides access to the values found in he `IFormCollection`, as well as the `RouteValueDictionary`. In the case of matching keys, the values in the `IFormCollection` take precedence.

The `FormData` inherits from `RequestData` type also exposes the `IFormFilesCollection` via the `_.Files` member and `_.TryGetFile(name : string)` method. 

```fsharp
type Person =
    { FirstName : string
      LastName : string }

let manualFormHandler : HttpHandler = fun ctx ->
    task {
        let! f : FormData = Request.getForm ctx

        let person =
            { FirstName = f.GetString ("FirstName", "John") // Get value or return default value
              LastName = f.GetString ("LastName", "Doe") }

        return! Response.ofJson person ctx
    }

let mapFormHandler : HttpHandler =
    Request.mapForm (fun f ->
        let first = f.GetString ("FirstName", "John") // Get value or return default value
        let last = f.GetString ("LastName", "Doe")
        { FirstName = first; LastName = last })
        Response.ofJson

let mapFormSecureHandler : HttpHandler =
    Request.mapFormSecure (fun f -> // `Request.mapFormSecure` will automatically validate CSRF token for you.
        let first = f.GetString ("FirstName", "John") // Get value or return default value
        let last = f.GetString ("LastName", "Doe")
        { FirstName = first; LastName = last })
        Response.ofJson
        (Response.withStatusCode 400 >> Response.ofEmpty)

```

### `multipart/form-data` Binding

Microsoft defines [large upload](https://docs.microsoft.com/en-us/aspnet/core/mvc/models/file-uploads#upload-large-files-with-streaming) as anything **> 64KB**, which well... is most uploads. Anything beyond this size and they recommend streaming the multipart data to avoid excess memory consumption.

To make this process **a lot** easier Falco's form handlers will attempt to stream multipart form-data, or return an error message indicating the likely problem.

```fsharp
let imageUploadHandler : HttpHandler =
    let formBinder (f : FormData) : IFormFile option =
        f.TryGetFormFile "profile_image"

    let uploadImage (profileImage : IFormFile option) : HttpHandler =
        // Process the uploaded file ...

    // Safely buffer the multipart form submission
    Request.mapForm formBinder uploadImage

let secureImageUploadHandler : HttpHandler =
    let formBinder (f : FormData) : IFormFile option =
        f.TryGetFormFile "profile_image"

    let uploadImage (profileImage : IFormFile option) : HttpHandler =
        // Process the uploaded file ...

    let handleInvalidCsrf : HttpHandler =
        Response.withStatusCode 400 >> Response.ofEmpty

    // Safely buffer the multipart form submission
    Request.mapFormSecure formBinder uploadImage handleInvalidCsrf
```

## JSON

These handlers use the .NET built-in `System.Text.Json.JsonSerializer`.

```fsharp
type Person =
    { FirstName : string
      LastName : string }

let jsonHandler : HttpHandler =
    { FirstName = "John"
      LastName = "Doe" }
    |> Response.ofJson

let mapJsonHandler : HttpHandler =
    let handleOk person : HttpHandler =
        let message = sprintf "hello %s %s" person.First person.Last
        Response.ofPlainText message

    Request.mapJson handleOk

let mapJsonOptionsHandler : HttpHandler =
    let options = JsonSerializerOptions()
    options.DefaultIgnoreCondition <- JsonIgnoreCondition.WhenWritingNull

    let handleOk person : HttpHandler =
        let message = sprintf "hello %s %s" person.First person.Last
        Response.ofPlainText message

    Request.mapJsonOption options handleOk
```


[Next: View engine](markup.md)
