#requires -Version 5.1

<#
.SYNOPSIS
    Local build script for the Docker Build Agent project using the Forge build system.

.DESCRIPTION
    This PowerShell script provides a convenient wrapper around the Forge build system for local development.
    It compiles the .NET solution and then executes the specified build type using the Forge executables.
    
    The script supports multiple build types (docker, node, node-in-docker, node-template) and can pass
    additional arguments to the underlying build system. It automatically manages artifacts directory
    creation and solution compilation before executing the build workflow.

    Build Process:
    1. Initializes the build environment using nuke-helpers module
    2. Compiles the Forge.sln solution to create build executables
    3. Executes the specified build type with provided arguments

.PARAMETER type
    Specifies the build type to execute. Valid values are:
    - 'docker' (default): Docker image build process
    - 'node': Node.js application build process  
    - 'node-in-docker': Combined Node.js + Docker build process
    - 'node-template': Documentation site build using templates

.PARAMETER isProd
    Switch parameter that enables production build mode. When specified, optimizes builds for production
    deployment with enhanced performance and reduced debug information.

.PARAMETER buildArguments
    Additional arguments to pass to the underlying Forge build system. These arguments are forwarded
    directly to the build executable and can include NUKE parameters like --dry-run, --verbosity, etc.

.EXAMPLE
    .\build.ps1
    Executes a Docker build using default settings.

.EXAMPLE
    .\build.ps1 -type node -isProd
    Executes a Node.js build in production mode.

.EXAMPLE
    .\build.ps1 -type node-in-docker --dry-run true --verbosity Verbose
    Executes a combined Node.js + Docker build in dry-run mode with verbose logging.

.EXAMPLE
    .\build.ps1 -type docker --image-tag myapp:v1.0.0 --registry-url ghcr.io/myorg
    Executes a Docker build with custom image tag and registry URL.

.NOTES
    - Requires PowerShell 5.1 or later
    - The Forge.sln solution must be buildable with .NET 8.0
    - Uses the nuke-helpers PowerShell module for build operations
    - Artifacts are output to ./artifacts directory
    - The script automatically handles solution compilation before build execution

.LINK
    https://github.com/The-Running-Dev/Docker-BuildAgent/blob/main/documentation/docs/build-types.md

.LINK
    https://github.com/The-Running-Dev/Docker-BuildAgent/blob/main/documentation/docs/parameters.md
#>

[CmdletBinding()]
Param(
    [Parameter(HelpMessage = "Build type to execute")]
    [ValidateSet('docker', 'node', 'node-in-docker', 'node-template')]
    [string]$type = 'docker',

    [Parameter(HelpMessage = "Build for production")]
    [switch]$isProd,
    
    [Parameter(ValueFromRemainingArguments)]
    [string[]]$buildArguments = @()
)
# Set up build environment paths and configuration
$artifactsDir = Join-Path $PSScriptRoot "artifacts"
$solutionFile = Join-Path $PSScriptRoot "forge\Forge.sln"

Write-Host "Docker Build Agent - Local Build Script" -ForegroundColor Cyan
Write-Host "Build Type: $type" -ForegroundColor Green
Write-Host "Production Mode: $($isProd.IsPresent)" -ForegroundColor Green
Write-Host "Artifacts Directory: $artifactsDir" -ForegroundColor Yellow

# Import the helper module for common build operations
Write-Host "Loading build helpers..." -ForegroundColor Blue
Import-Module (Join-Path $PSScriptRoot 'scripts/nuke/nuke-helpers.psm1') -Force

# Initialize build environment (sets up paths, validates prerequisites)
Write-Host "Initializing build environment..." -ForegroundColor Blue
Initialize-Build

# Execute dotnet build to create the artifacts
Write-Host "Compiling Forge solution..." -ForegroundColor Blue
Invoke-DotNetBuild `
    -ProjectOrSolution $solutionFile `
    -OutputDirectory $artifactsDir `
    -IsProduction:$isProd

# Execute the build workflow for the specified type
Write-Host "Executing $type build workflow..." -ForegroundColor Blue
Invoke-Forge `
    -BuildTypes $type `
    -Arguments $buildArguments `
    -WorkingDir . `
    -ArtifactsDir $artifactsDir