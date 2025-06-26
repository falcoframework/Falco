# Example - Dependency Injection

An important and nuanced subject to discuss is dependency injection. There's a myriad of beliefs and approaches, all of which have their merit. In the case of Falco, you are living in the world of ASP.NET which has [built-in support](https://learn.microsoft.com/en-us/dotnet/core/extensions/dependency-injection) for this. It works very well and you should use it. But make sure you follow through their [docs](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/dependency-injection?view=aspnetcore-8.0) on how it works and integrates with ASP.NET.

Going back to our basic [Hello World](example-hello-world.md) app, let's add in an external dependency to demonstrate some of the basics of dependency injection in Falco.

The code for this example can be found [here](https://github.com/FalcoFramework/Falco/tree/master/examples/DependencyInjection).

## Creating the Application Manually

```shell
> dotnet new falco -o DependencyInjectionApp
```

## Creating Abstraction

The benefit of abstracting functionality is that it removes the coupling between your implementation and the calling code. You instead rely on an accepted definition of what something does.

F# has excellent support for [object programming](https://learn.microsoft.com/en-us/dotnet/fsharp/language-reference/classes). There might be an urge to avoid this type of approach because "ugh classes are gross". But suck it up buttercup, they are wickedly useful in many cases and a reminder that F# code doesn't have to adhere to some functional purism.

In the case of our application, we're going to define an abstraction for greeting patrons. Then write a simple implementation.

> This is a completely contrived example, created purely to demonstrate how to register and consume dependencies.

```fsharp
type IGreeter =
    abstract member Greet : name : string -> string

type FriendlyGreeter() =
    interface IGreeter with
        member _.Greet(name : string) =
            $"Hello {name} 😀"
```

Simple enough, we describe an `IGreeter` as having the ability to `Greet` in the form of receiving a name string and return a string message. Next we define an implementation that fulfills this interface in a friendly way.

## Registering the Dependency

To provide runtime access to our greeter, we have to register the dependency in the container. The abstraction from ASP.NET for this is called `IServiceCollection`. You can register dependencies in a number of ways, but fundamental to all is the concept of [service lifetime](https://learn.microsoft.com/en-us/dotnet/core/extensions/dependency-injection#service-lifetimes). It distills down to:

- Transient = new for every container access
- Scoped = new for every client request
- Singleton = created at startup, or first container access

Our greeter is both stateless and cheap to construct. So any of the lifetimes will suffice. But let's register it as a singleton. This time however, we'll create our web server in two stages, to gain access to the dependency container.

```fsharp
let bldr = WebApplication.CreateBuilder() // <-- create a configurable web application builder

bldr.Services
    .AddSingleton<IGreeter, FriendlyGreeter>() // <-- register the greeter as singleton in the container
    |> ignore

let wapp = bldr.Build() // <-- manifest our WebApplication

let endpoints =
    [
        mapGet "/{name?}"
            (fun r -> r?name.AsString("world"))
            (fun name ctx ->
                let greeter = ctx.Plug<IGreeter>() // <-- access our dependency from the container
                let greeting = greeter.Greet(name) // <-- invoke our greeter.Greet(name) method
                Response.ofPlainText greeting ctx)
    ]

wapp.UseRouting()
    .UseFalco(endpoints)
    .Run()
```

Following through you can see the web server being created in two phases. The first to establish the context (i.e., logging, server configuration and dependencies). Second, freezing the final state and creating a configurable web application.

Within the handler you can see the interaction with the dependency container using `ctx.Plug<IGreeter>()`. This code tells the container to return the implementation it has registered for that abstraction. In our case `FriendlyGreeter`.

## Wrapping Up

Now that we're finished introducing dependency injection, let's move on to a real world example by integrating with an external view engine.

[Next: Example - External View Engine](example-external-view-engine.md)
