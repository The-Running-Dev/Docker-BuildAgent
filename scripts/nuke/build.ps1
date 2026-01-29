#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Unified build script for Docker-BuildAgent container.

.DESCRIPTION
    Routes to the appropriate build workflow based on the specified type.
    This consolidates all build types into a single entry point.

    Build Types:
    - docker: Build and push Docker images
    - node: Build Node.js applications
    - node-in-docker: Build Node.js app and Docker image
    - node-template: Build Node.js documentation from template
    - forge: Run Forge build targets (changelog, etc.)

.PARAMETER Type
    The build type to execute.

.PARAMETER WorkingDir
    The root directory of the project. Defaults to current directory.

.PARAMETER ArtifactsDir
    Directory containing compiled Nuke DLLs. Defaults to /nuke/forge.

.EXAMPLE
    build.ps1 forge --change-log-source all

.EXAMPLE
    build.ps1 docker --image-tag myapp:latest

.EXAMPLE
    build.ps1 node-template -AppDir documentation
#>
[CmdletBinding()]
param(
    [Parameter(Position = 0, Mandatory)]
    [ValidateSet('docker', 'node', 'node-in-docker', 'node-template', 'forge')]
    [string]$type,

    [Parameter(Mandatory = $false)]
    [string]$workingDir = $(Get-Location).Path,

    [Parameter(Mandatory = $false)]
    [string]$artifactsDir = "/nuke/forge",

    # Node-template specific parameters
    [Parameter(Mandatory = $false)]
    [string]$appDir = 'documentation',

    [Parameter(Mandatory = $false)]
    [string]$nodeTemplateRepositoryUrl = 'https://github.com/The-Running-Dev/Docusaurus-Template.git',

    [Parameter(Mandatory = $false)]
    [string]$nodeTemplateDirPath = '/node-template',

    [Parameter(Mandatory = $false)]
    [switch]$skipInstall = $false,

    [Parameter(Mandatory = $false)]
    [ValidateSet('npm', 'pnpm', 'yarn', '')]
    [string]$packageManager,

    [Parameter(Mandatory = $false)]
    [switch]$isProduction,

    [Parameter(ValueFromRemainingArguments = $true)]
    [string[]]$nukeArgs
)

# Import the helper module for common build operations
Import-Module (Join-Path $PSScriptRoot 'nuke-helpers.psm1') -Force

# Route based on build type
switch ($type) {
    'node-template' {
        # Node-template has its own workflow (doesn't use Invoke-Forge)
        Invoke-NodeTemplateBuild
    }
    default {
        # Standard Nuke-based builds (docker, node, node-in-docker, forge)
        Invoke-Forge `
            -BuildTypes @($type) `
            -Arguments $nukeArgs `
            -WorkingDir $workingDir `
            -ArtifactsDir $artifactsDir
    }
}

function Invoke-NodeTemplateBuild {
    <#
    .SYNOPSIS
        Builds a Node.js documentation app from a template repository.
    #>

    # Set default value for production build if not specified
    if (-not $PSBoundParameters.ContainsKey('isProduction')) {
        $script:isProduction = $true
    }

    # Define file paths and setup variables
    $templateSetupFile = "template-setup.ps1"
    $templateSetupFilePath = Join-Path $workingDir $templateSetupFile
    $templateBuildFile = "template-build.ps1"
    $templateBuildFilePath = Join-Path $workingDir $templateBuildFile

    $appDirPath = Join-Path $workingDir $appDir -Resolve

    #region Template Repository Setup
    Write-Host "[CLONE] Cloning Template..." -ForegroundColor Cyan
    Write-Host "   Repository: $nodeTemplateRepositoryUrl" -ForegroundColor Gray
    Write-Host "   Destination: $nodeTemplateDirPath" -ForegroundColor Gray

    Invoke-SafeCommand {
        if ($nodeTemplateRepositoryUrl -match '^(.+?)#(.+)$') {
            $repoUrl = $Matches[1]
            $branch = $Matches[2].Trim()

            if ([string]::IsNullOrWhiteSpace($branch)) {
                throw "Branch Cannot be Empty"
            }

            Write-Host "   Using Branch: $branch" -ForegroundColor Gray
            git clone --depth 1 -b $branch $repoUrl $nodeTemplateDirPath

            if ($LASTEXITCODE -ne 0) {
                throw "Git Clone Failed: $LASTEXITCODE"
            }
        }
        else {
            git clone --depth 1 $nodeTemplateRepositoryUrl $nodeTemplateDirPath

            if ($LASTEXITCODE -ne 0) {
                throw "Git Clone Failed: $LASTEXITCODE"
            }
        }
    }

    Write-Host "[OK] Template Repository Cloned Successfully" -ForegroundColor Green
    #endregion

    #region Build Information Display
    Write-Host ""
    Write-Host "[CONFIG] Build Configuration:" -ForegroundColor Cyan
    Write-Host "   Working Directory: $workingDir" -ForegroundColor Gray
    Write-Host "   App Directory: $appDir" -ForegroundColor Gray
    Write-Host "   App Path: $appDirPath" -ForegroundColor Gray
    Write-Host "   Template URL: $nodeTemplateRepositoryUrl" -ForegroundColor Gray
    Write-Host "   Template Path: $nodeTemplateDirPath" -ForegroundColor Gray
    Write-Host "   Skip Install: $skipInstall" -ForegroundColor Gray
    Write-Host "   Production Build: $isProduction" -ForegroundColor Gray
    Write-Host ""
    #endregion

    #region Template File Copying
    Write-Host "[COPY] Copying Template Files..." -ForegroundColor Cyan
    Write-Host "   From: $nodeTemplateDirPath" -ForegroundColor Gray
    Write-Host "   To: $appDirPath" -ForegroundColor Gray
    Write-Host "   Mode: Preserve Existing Files" -ForegroundColor Gray

    Copy-Directory `
        -sourceDir $nodeTemplateDirPath `
        -destinationDir (Join-Path $workingDir $appDir) `
        -overwrite:$false

    Write-Host "[OK] Template Files Copied Successfully" -ForegroundColor Green
    #endregion

    #region Template Setup Script Execution
    Invoke-Script `
        -WorkingDir $workingDir `
        -ScriptFile $templateSetupFile `
        -Message "Running Template Setup..."

    if (Test-Path $templateSetupFilePath) {
        Write-Host "[CLEAN] Removing Template Setup Script..." -ForegroundColor Yellow

        Invoke-SafeCommand {
            Remove-Item $templateSetupFilePath -Force
        }

        Write-Host "[OK] Template Setup Script Removed" -ForegroundColor Green
    }
    #endregion

    #region Package Manager Setup and Installation
    Write-Host "[SETUP] Preparing Package Management..." -ForegroundColor Cyan
    Set-Location $appDirPath

    if (-not $packageManager) {
        $packageManager = Get-PackageManager -ProjectDir $appDirPath
    }
    else {
        Write-Host "[CONFIG] Using Specified Package Manager: $packageManager" -ForegroundColor Green
    }

    if (-not $skipInstall) {
        Write-Host "[INSTALL] Installing Dependencies..." -ForegroundColor Cyan
        Write-Host "   Command: $packageManager install" -ForegroundColor Gray

        Invoke-SafeCommand {
            & $packageManager install
        }

        Write-Host "[OK] Dependencies Installed Successfully" -ForegroundColor Green
    }
    else {
        Write-Host "[SKIP] Skipping Dependency Installation (skipInstall Flag Specified)" -ForegroundColor Yellow
    }
    #endregion

    #region Production Build
    if ($isProduction) {
        Write-Host "[BUILD] Building for Production..." -ForegroundColor Cyan
        Write-Host "   Command: $packageManager run build:prod" -ForegroundColor Gray

        Invoke-SafeCommand {
            & $packageManager run build:prod
        }

        Write-Host "[OK] Production Build Completed Successfully" -ForegroundColor Green
    }
    else {
        Write-Host "[SKIP] Skipping Production Build (isProduction Set to False)" -ForegroundColor Yellow
    }
    #endregion

    #region Completion
    Write-Host ""
    Write-Host "[OK] Node Template Build Completed Successfully!" -ForegroundColor Green

    if ($isProduction) {
        Write-Host "   Production Build: Ready for Deployment" -ForegroundColor Gray
    }
    else {
        Write-Host "   Development Setup: Ready for Development" -ForegroundColor Gray
    }
    Write-Host ""

    if ($templateBuildFilePath) {
        Write-Host "[INFO] Run: $templateBuildFilePath for Local Build" -ForegroundColor White
    }
    #endregion
}
