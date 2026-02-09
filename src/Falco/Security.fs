module Falco.Security

module Xsrf =
    open System
    open System.Threading.Tasks
    open Falco.Markup
    open Microsoft.AspNetCore.Antiforgery
    open Microsoft.AspNetCore.Http
    open Microsoft.Extensions.DependencyInjection

    /// Outputs an antiforgery <input type="hidden" />.
    let antiforgeryInput
        (token : AntiforgeryTokenSet) =
        Elem.input [
            Attr.type' "hidden"
            Attr.name token.FormFieldName
            Attr.value token.RequestToken ]

    /// Generates an antiforgery token and stores it in the user's cookies.
    let getToken (ctx : HttpContext) : AntiforgeryTokenSet =
        let antiFrg = ctx.RequestServices.GetRequiredService<IAntiforgery>()
        antiFrg.GetAndStoreTokens ctx

    /// Validates the antiforgery token within the provided HttpContext.
    let validateToken (ctx : HttpContext) : Task<bool> =
        if ctx.Request.Method = HttpMethods.Get
           || ctx.Request.Method = HttpMethods.Options
           || ctx.Request.Method = HttpMethods.Head
           || ctx.Request.Method = HttpMethods.Trace then
           Task.FromResult true
        else
            task {
                try
                    let antiFrg = ctx.RequestServices.GetRequiredService<IAntiforgery>()
                    do! antiFrg.ValidateRequestAsync ctx
                    return true
                with
                | :? InvalidOperationException ->
                    return true  // Antiforgery not registered, consider valid
                | :? AntiforgeryValidationException ->
                    return false  // Token present but invalid
            }
