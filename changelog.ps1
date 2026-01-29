#!/usr/bin/env pwsh

<#
    .SYNOPSIS
    Generates the CHANGELOG.md from git commit history using the Docker-BuildAgent.
    
    .DESCRIPTION
    This script runs the Forge build target with --change-log-source 'all' to generate
    a comprehensive changelog from all git commits.
    
    Supports two execution methods:
    1. Docker container (preferred) - requires Docker to be installed and ghcr.io/the-running-dev/build-agent:latest available
    2. Direct Nuke execution (fallback) - requires .NET SDK and compiled Forge project
    
    .EXAMPLE
    .\changelog.ps1
#>

Write-Host "Generating Changelog from Git History..." -ForegroundColor Cyan

# Try to use Docker first if available
$dockerAvailable = $null -ne (Get-Command docker -ErrorAction SilentlyContinue)

if ($dockerAvailable) {
    Write-Host "Using Docker Container for Changelog Generation..." -ForegroundColor Yellow
    
    & docker run `
        --rm `
        -v ./:/workspace `
        -it ghcr.io/the-running-dev/build-agent:latest `
        build forge --change-log-source all
} else {
    Write-Host "Docker not Found, Falling Back to Direct Nuke Execution..." -ForegroundColor Yellow
    
    & dotnet run --project forge/Forge/Forge.csproj -- --change-log-source 'all'
}

if ($LASTEXITCODE -eq 0) {
    Write-Host "Changelog Generated Successfully!" -ForegroundColor Green
    
    # Copy the generated changelog to the documentation site
    $sourceFile = Join-Path $PSScriptRoot "CHANGELOG.md"
    $destFile = Join-Path $PSScriptRoot "documentation\src\pages\CHANGELOG.md"
    
    if (Test-Path $sourceFile) {
        Write-Host "Copying Changelog to Documentation Site..." -ForegroundColor Yellow
        
        Move-Item -Path $sourceFile -Destination $destFile -Force
        
        Write-Host "Changelog Copied to: $destFile" -ForegroundColor Green
    }
    
    Write-Host "Check CHANGELOG.md and documentation\src\pages\CHANGELOG.md for Updates." -ForegroundColor Green
} else {
    Write-Host "Failed to Generate Changelog. Exit Code: $LASTEXITCODE" -ForegroundColor Red
    
    exit $LASTEXITCODE
}
