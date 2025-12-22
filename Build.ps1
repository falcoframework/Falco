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
$srcDir = Join-Path $rootDir 'src'
$testDir = Join-Path $rootDir 'test'
$docsOutputDir = Join-Path $rootDir 'docs'

switch ($Action) {
    "Test"            { $projectdir = Join-Path $testDir 'Falco.Tests' }
    "IntegrationTest" { $projectdir = Join-Path $testDir 'Falco.IntegrationTests' }
    "Pack"            { $projectDir = Join-Path $srcDir 'Falco' }
    "BuildSite"       { $projectDir = Join-Path $rootDir 'site' }
    "DevelopSite"     { $projectDir = Join-Path $rootDir 'site' }
    Default           { $projectDir = Join-Path $srcDir 'Falco' }
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
    "BuildSite"       { RunCommand "dotnet run --project `"$projectDir`" -- `"$docsOutputDir`"" }
    "DevelopSite"     { RunCommand "dotnet watch --project `"$projectDir`" -- run `"$docsOutputDir`"" }
    Default           { RunCommand "dotnet build `"$projectDir`" -c $Configuration" }
}
