#!/usr/bin/env pwsh

<#
.SYNOPSIS
    Builds and serves documentation locally with hot-reload for development.

.DESCRIPTION
    This script provides a streamlined way to develop documentation locally with
    hot-reload capabilities. It installs dependencies and starts a Docusaurus
    development server at http://localhost:3000.

    The script runs from the docs-template directory and reads docs directly
    from documentation/docs via the Docusaurus config.

.PARAMETER None
    This script takes no parameters.

.EXAMPLE
    .\build-docs-local.ps1
    
    Starts the development server with hot-reload at http://localhost:3000

.NOTES
    Requirements:
    - pnpm must be installed globally
    - Docusaurus template must be set up via setup-docs-submodule.ps1
    
    When editing:
    - ./documentation/docs/* changes appear immediately
    - Style and component changes appear immediately
    
    Author: Docker Build Agent Team
    Version: 2.0
#>

$templateDir = './docs-template'
$docsSourceDir = './documentation/docs'

Write-Host "[START] Starting Documentation Development Server..." -ForegroundColor Cyan
Write-Host ""

# Verify template directory exists
if (-not (Test-Path $templateDir)) {
    Write-Error "Template Directory not Found at $templateDir"
    Write-Host "Run ./scripts/setup-docs-submodule.ps1 first" -ForegroundColor Yellow
    exit 1
}

# Verify docs source exists
if (-not (Test-Path $docsSourceDir)) {
    Write-Error "Documentation Directory not Found at $docsSourceDir"
    exit 1
}

# Change to template directory
Push-Location $templateDir

Write-Host "[SETUP] Installing Dependencies..." -ForegroundColor Yellow
$installResult = & pnpm install

if ($LASTEXITCODE -ne 0) {
    Write-Error "Failed to Install Dependencies"

    exit 1
}

Write-Host ""
Write-Host "=" * 80 -ForegroundColor Cyan
Write-Host "[RUN] Development Server Starting..." -ForegroundColor Green
Write-Host "[URL] http://localhost:3000" -ForegroundColor Cyan
Write-Host "[INFO] Hot-Reload: Enabled (Changes Save Instantly)" -ForegroundColor Cyan
Write-Host "[INFO] Edit files in: ./documentation/docs/" -ForegroundColor Cyan
Write-Host "[WARN] Press Ctrl+C to Stop the Server" -ForegroundColor Yellow
Write-Host "=" * 80 -ForegroundColor Cyan
Write-Host ""

# Start the development server
& pnpm start

if ($LASTEXITCODE -ne 0) {
    Write-Error "Development Server Exited with Error"

    exit 1
}

# Pop location back when exited
Pop-Location
