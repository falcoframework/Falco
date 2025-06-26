[CmdletBinding()]
param (
    [Parameter(HelpMessage="The action to execute.")]
    [ValidateSet("Build", "Test", "IntegrationTest", "Pack", "BuildSite", "DevelopSite")]
    [string] $Action = "Build",

    [Parameter(HelpMessage="The msbuild configuration to use.")]
    [ValidateSet("Debug", "Release")]
    [string] $Configuration = "Debug",

    [switch] $NoRestore,

    [switch] $Clean
)

function RunCommand {
    param ([string] $CommandExpr)
    Write-Verbose "  $CommandExpr"
    Invoke-Expression $CommandExpr
}

$rootDir = $PSScriptRoot
$srcDir = Join-Path -Path $rootDir -ChildPath 'src'
$testDir = Join-Path -Path $rootDir -ChildPath 'test'
$docsOutputDir = Join-Path -Path $rootDir -ChildPath 'docs'

switch ($Action) {
    "Test"            { $projectdir = Join-Path -Path $testDir -ChildPath 'Falco.Tests' }
    "IntegrationTest" { $projectdir = Join-Path -Path $testDir -ChildPath 'Falco.IntegrationTests' }
    "Pack"            { $projectDir = Join-Path -Path $srcDir -ChildPath 'Falco' }
    "BuildSite"       { $projectDir = Join-Path -Path $rootDir -ChildPath 'site' }
    "DevelopSite"     { $projectDir = Join-Path -Path $rootDir -ChildPath 'site' }
    Default           { $projectDir = Join-Path -Path $srcDir -ChildPath 'Falco' }
}

if(!$NoRestore.IsPresent) {
    RunCommand "dotnet restore $projectDir --force --force-evaluate --nologo --verbosity quiet"
}

if ($Clean) {
    RunCommand "dotnet clean $projectDir -c $Configuration --nologo --verbosity quiet"
}

switch ($Action) {
    "Test"            { RunCommand "dotnet test `"$projectDir`"" }
    "IntegrationTest" { RunCommand "dotnet test `"$projectDir`"" }
    "Pack"            { RunCommand "dotnet pack `"$projectDir`" -c $Configuration --include-symbols --include-source" }
    "BuildSite"       { RunCommand "dotnet run --project `"$projectDir`" `"$docsOutputDir`"" }
    "DevelopSite"     { RunCommand "dotnet watch --project `"$projectDir`" -- run `"$docsOutputDir`"" }
    Default           { RunCommand "dotnet build `"$projectDir`" -c $Configuration" }
}
