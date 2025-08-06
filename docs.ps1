#!/usr/bin/env pwsh

<#
.SYNOPSIS
    Builds and serves the Docker Build Agent documentation locally for development.

.DESCRIPTION
    This script provides a streamlined way to build and serve the Docusaurus documentation
    for local development. It performs two main phases:
    
    1. Template Build Phase: Uses the Docker Build Agent to prepare the documentation
       environment and copy necessary template files
    2. Development Server Phase: Installs dependencies and starts a local development
       server with hot-reload capabilities
    
    The script is designed for documentation contributors who need to preview changes
    locally before committing.

.PARAMETER None
    This script takes no parameters. Configuration is hardcoded for consistency.

.EXAMPLE
    .\build-docs.ps1
    
    Builds the documentation and starts a development server at http://localhost:3000

.NOTES
    Requirements:
    - Docker must be installed and running
    - pnpm must be available in the host system PATH
    - The documentation directory must contain a valid Docusaurus project
    
    The script will:
    - Mount the current directory to the Docker container
    - Use the Build Agent's node-template-build command
    - Skip container-based package installation (handled by host pnpm)
    - Start a new PowerShell window with the development server
    
    Author: Docker Build Agent Team
    Version: 1.0
#>

# Configuration
$appDir = '.\documentation'

# Phase 1: Template Build using Docker Build Agent
# This phase prepares the documentation environment by:
# - Mounting the workspace to the container
# - Using node-template-build to copy templates and setup files
# - Configuring for pnpm package manager
# - Skipping container-based installation (we'll install on host)
# - Setting development mode (not production)
Write-Host "🔨 Phase 1: Preparing Documentation Environment..." -ForegroundColor Cyan

& docker run `
    --rm `
    -v ./:/workspace `
    -it ghcr.io/the-running-dev/build-agent:latest `
    node-template-build `
        -AppDir $appDir `
        -PackageManager 'pnpm' `
        -NodeTemplateRepositoryUrl 'https://github.com/The-Running-Dev/Docusaurus-Template.git' `
        -SkipInstall `
        -IsProduction:$false

# Phase 2: Local Development Server Setup
# This phase sets up and starts the Docusaurus development server:
# - Resolves the full path to the documentation directory
# - Creates a command script for the new PowerShell session
# - Installs dependencies using pnpm
# - Runs prebuild steps (if any)
# - Starts the development server with hot-reload
Write-Host "🚀 Phase 2: Starting Development Server..." -ForegroundColor Green

$appDirPath = Join-Path . $appDir -Resolve

# Create command script for the new PowerShell session
# This will run in the documentation directory
$command = @"
Write-Host "📁 Changing to App Directory: $appDirPath" -ForegroundColor Yellow
Set-Location '$appDirPath'

Write-Host "📦 Installing Dependencies with pnpm..." -ForegroundColor Yellow
& pnpm install

Write-Host "⚙️ Running Pre-Build Step..." -ForegroundColor Yellow
& pnpm run prebuild:prod

Write-Host "🌐 Starting Development Server..." -ForegroundColor Green
Write-Host "   📍 URL: http://localhost:3000" -ForegroundColor Cyan
Write-Host "   🔄 Hot-Reload Enabled for Live Editing" -ForegroundColor Cyan
& pnpm start
"@

# Start the development server in a new PowerShell window
# -NoExit keeps the window open after the server starts
# This allows you to see server logs and stop it with Ctrl+C
Write-Host "🪟 Opening New Window for Development Server..." -ForegroundColor Magenta

Start-Process pwsh `
    -ArgumentList  `
    "-NoExit", `
    "-Command", $command

Write-Host "✅ Documentation Build Complete!" -ForegroundColor Green
Write-Host "   The Development Server Should Start Shortly in the New Window." -ForegroundColor White
Write-Host "   Visit http://localhost:3000 to View Your Documentation." -ForegroundColor White