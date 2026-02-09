# Migrating from v5.x to v6.x

The objective of Falco v6.x is to continue to simplify the API and improve the overall development experience long term. The idea being provide only what is necessary, or provides the most value in the most frequently developed areas.

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

        match form.IsValid with
        | true -> // do something with form
        | false -> // handle failed csrf validation
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
        match form.IsValid with
        | true -> Some // do something with form
        | false -> None // handle failed csrf validation

    let handler (myForm : MyForm option) : HttpHandler =
        match myForm with
        | Some myForm -> // do something with myForm
        | None -> // handle failed csrf validation

    Request.mapForm formMap handler
```

</td>
</tr>
</table>
