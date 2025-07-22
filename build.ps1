[CmdletBinding()]
Param(
    [Parameter(HelpMessage = "Build type to execute")]
    [string]$type = 'docker',
    
    [Parameter(HelpMessage = "Build for development (faster, no restore)")]
    [switch]$isDevelopment,
    
    [Parameter(ValueFromRemainingArguments)]
    [string[]]$buildArguments
)

# Import the helper module for common build operations
Import-Module (Join-Path $PSScriptRoot 'scripts/nuke/nuke-helpers.psm1') -Force

# Initialize the build script with common settings and load environment
Initialize-BuildScript `
    -Name "Forge Build" `
    -WorkingDir $PSScriptRoot `
    -Arguments $buildArguments

# Initialize and validate build paths
$paths = Initialize-BuildPaths -ProjectRoot $PSScriptRoot

# Initialize .NET SDK environment
Initialize-DotNetEnvironment -TempDirectory $paths.TempDir

# Validate .NET environment is properly configured
if (-not $env:DOTNET_EXE) {
    Write-Error "❌ Failed to Configure .NET Environment. DOTNET_EXE Not Set."
    
    exit 1
}

# Execute the build using helper function
Invoke-BuildProject `
    -ProjectFile $paths.ProjectFile `
    -OutputDirectory $paths.ArtifactsDir `
    -IsDevelopment:$isDevelopment

# Execute the Forge application using helper function
Invoke-ForgeApplication `
    -ArtifactsDir $paths.ArtifactsDir `
    -Type $type `
    -BuildArguments $buildArguments