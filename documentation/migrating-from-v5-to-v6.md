# Migrating from v5.x to v6.x

The objective of Falco v6.x is to continue to simplify the API and improve the overall development experience long term. The idea being to provide only what is necessary, or what provides the most value in the most frequently developed areas.

## `Request.getFormSecure`

<table>
<tr>
<td>

```fsharp
// Falco v5.x
let myHandler : HttpHandler = fun ctx ->
    task {
        let! form = Request.getFormSecure ctx

        match form with
        | Some form -> // do something with form
        | None -> // handle failed csrf validation
    }
```

</td>
<td>

```fsharp
// Falco v6.x
let myHandler : HttpHandler = fun ctx ->
    task {
        let! form = Request.getForm ctx

        match form with
        | Some form -> // do something with form
        | None -> // handle failed CSRF validation or an oversized form
    }
```

</td>
</tr>
</table>

## `Request.mapFormSecure`


<table>
<tr>
<td>

```fsharp
// Falco v5.x
type MyForm =
    { Name : string }

let myHandler : HttpHandler =
    let formMap (form : FormData) : MyForm  =
        // do something with form

    let handler (myForm : MyForm) : HttpHandler =
        // do something with myForm

    let handleInvalid : HttpHandler =
        // handle failed csrf validation

    Request.mapFormSecure formMap handler handleInvalid
```

</td>
<td>

```fsharp
// Falco v6.x
type MyForm =
    { Name : string }

let myHandler : HttpHandler =
    let formMap (form : FormData) : MyForm  =
        // do something with form

    let handler (myForm : MyForm) : HttpHandler =
        // do something with myForm

    let handleInvalid : HttpHandler =
        // handle failed CSRF validation or an oversized form

    Request.mapForm formMap handler handleInvalid
```

</td>
</tr>
</table>
