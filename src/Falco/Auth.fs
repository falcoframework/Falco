﻿module Falco.Security.Auth

open System.Security.Claims
open System.Threading.Tasks
open Microsoft.AspNetCore.Authentication
open Microsoft.AspNetCore.Http
open Falco.StringUtils
open Falco.Extensions

/// Returns the current user (IPrincipal) or None
let getUser (ctx : HttpContext) =
    match ctx.User with
    | null -> None
    | _    -> Some ctx.User

/// Returns authentication status of IPrincipal, false on null
let isAuthenticated (ctx : HttpContext) : bool =
    let isAuthenciated (user : ClaimsPrincipal) =
        let identity = user.Identity
        match identity with
        | null -> false
        | _    -> identity.IsAuthenticated

    match getUser ctx with
    | None      -> false
    | Some user -> isAuthenciated user

/// Returns bool if IPrincipal is in list of roles, false on None
let isInRole 
    (roles : string list)
    (ctx : HttpContext) : bool =
    match getUser ctx with
    | None      -> false
    | Some user -> List.exists user.IsInRole roles

/// Attempts to return claims from IPrincipal, empty seq on None
let getClaims
    (ctx : HttpContext) : Claim seq =
    match getUser ctx with
    | None      -> Seq.empty
    | Some user -> user.Claims

/// Attempts to return a specific claim from IPrincipal with a generic predicate
let tryFindClaim
    (predicate : Claim -> bool)
    (ctx : HttpContext) : Claim option =
    match getUser ctx with
    | None      -> None
    | Some user ->         
        match user.Claims |> Seq.tryFind predicate with
        | None   -> None
        | Some claim -> Some claim

/// Attempts to return specific claim from IPrincipal
let getClaim
    (claimType : string)
    (ctx : HttpContext) : Claim option =
    tryFindClaim (fun claim -> strEquals claim.Type claimType) ctx

/// Returns bool if IPrincipal has specified scope
let hasScope
    (issuer : string)
    (scope : string)
    (ctx : HttpContext) : bool =
    tryFindClaim (fun claim -> (strEquals claim.Issuer issuer) && (strEquals claim.Type "scope")) ctx
    |> function
        | None       -> false
        | Some claim -> Array.contains scope (strSplit [|' '|] claim.Value)

/// Establish an authenticated context for the provide scheme and principal
let signIn
    (authScheme : string)
    (claimsPrincipal : ClaimsPrincipal)
    (ctx : HttpContext) : Task =
    ctx.SignInAsync(authScheme, claimsPrincipal)

/// Terminate authenticated context for provided scheme
let signOut
    (authScheme : string)
    (ctx : HttpContext) : Task = 
    ctx.SignOutAsync(authScheme)