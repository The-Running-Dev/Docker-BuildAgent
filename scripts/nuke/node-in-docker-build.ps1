#!/usr/bin/env pwsh
param(
    [string]$WorkingDir = $(Get-Location).Path,
    [string]$ArtifactsDir = "/nuke/forge"
)
# Import the helper module for common build operations
Import-Module (Join-Path $PSScriptRoot 'nuke-helpers.psm1') -Force

# Execute standard build workflow for Node-in-Docker (node build followed by docker build)
Invoke-ForgeBuild `
    -BuildName "Node-in-Docker Build" `
    -BuildTypes @("node", "docker") `
    -Arguments $args `
    -WorkingDir $WorkingDir `
    -ArtifactsDir $ArtifactsDir