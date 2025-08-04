#!/usr/bin/env pwsh
param(
    [string]$WorkingDir = $(Get-Location).Path,
    [string]$ArtifactsDir = "/nuke/forge"
)
# Import the helper module for common build operations
Import-Module (Join-Path $PSScriptRoot 'nuke-helpers.psm1') -Force

# Execute standard build workflow for Forge
Invoke-Forge `
    -BuildTypes @("forge") `
    -Arguments $args `
    -WorkingDir $WorkingDir `
    -ArtifactsDir $ArtifactsDir