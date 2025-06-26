# Deployment

One of the key features of Falco is that it contains little to no "magic" (i.e., no hidden reflection or dynamic code). This means that Falco is both [trimmable](https://learn.microsoft.com/en-us/dotnet/core/deploying/trimming/trim-self-contained) and [AOT](https://learn.microsoft.com/en-us/dotnet/core/deploying/native-aot) compatible out of the box.

This means that you can deploy your Falco application as a self-contained executable, or as a native AOT executable, with no additional configuration. A huge benefit of this is that you can deploy your Falco application to any environment, without having to worry about the underlying runtime or dependencies.

> Important! If you're in a __scale-to-zero__ hosting environment consider using a [ReadyToRun](https://learn.microsoft.com/en-us/dotnet/core/deploying/ready-to-run) deployment. This will ensure that your application will experience faster cold start times.

## Self-contained deployments

It is highly recommended to deploy your Falco application as a self-contained executable. This means that the .NET runtime and all dependencies are included in the deployment package, so you don't have to worry about the target environment having the correct version of .NET installed. This will result in a slightly larger deployment package, but it will ensure that your application runs correctly in any environment. The larger binary size can also be offset by using trim.

Below is an example [Directory.Build.props] that will help enable the non-AOT features. These properties can also be added to you fsproj file.

```xml
<Project>
    <PropertyGroup>
        <SelfContained>true</SelfContained>
        <PublishSingleFile>true</PublishSingleFile>
        <PublishTrimmed>true</PublishTrimmed>
        <TrimMode>Link</TrimMode>
        <IncludeNativeLibrariesForSelfExtract>true</IncludeNativeLibrariesForSelfExtract>
        <EnableCompressionInSingleFile>true</EnableCompressionInSingleFile>
        <!-- Optional: enable if in scale-to-zero hosting environment -->
        <!-- <PublishReadyToRun>true</PublishReadyToRun> -->
    </PropertyGroup>
</Project>
```

## Native AOT deployments

Publishing your app as Native AOT produces an app that's self-contained and that has been ahead-of-time (AOT) compiled to native code. Native AOT apps have faster startup time and smaller memory footprints. These apps can run on machines that don't have the .NET runtime installed.

Since AOT deployments require trimming, and are single file by nature the only required msbuild property is:

```xml
<Project>
    <PropertyGroup>
        <PublishAot>true</PublishAot>
    </PropertyGroup>
</Project>
```

[Next: Example - Hello World](example-hello-world.md)
