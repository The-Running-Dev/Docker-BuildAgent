#!/usr/bin/env pwsh

<#
.SYNOPSIS
    Sets up the Docusaurus template as a Git submodule.

.DESCRIPTION
    This script initializes the Docusaurus template as a Git submodule, enabling:
    - Template changes to be version controlled in the submodule
    - Local template modifications with push-back capability
    - Documentation to be baked into the Docker image
    - Public publishing to GitHub Pages

.PARAMETER TemplateRepoUrl
    The Git repository URL for the Docusaurus template.
    Default: https://github.com/The-Running-Dev/Docusaurus-Template.git

.EXAMPLE
    .\setup-docs-submodule.ps1
    
    Sets up the submodule with default template repository.

.NOTES
    This script should be run once during project setup.
#>

[CmdletBinding()]
param(
    [string]$templateRepoUrl = "https://github.com/The-Running-Dev/Docusaurus-Template.git"
)

$submodulePath = "docs-template"
$docsSourceDir = "documentation"

Write-Host "[START] Setting Up Docusaurus Template as Git Submodule..." -ForegroundColor Cyan

# 1. Add submodule
Write-Host "[SETUP] Adding Git Submodule..." -ForegroundColor Yellow
if (Test-Path $submodulePath) {
    Write-Host "[WARN] Submodule Path Already Exists. Initializing..." -ForegroundColor Yellow
    git submodule add --force $templateRepoUrl $submodulePath
} else {
    git submodule add $templateRepoUrl $submodulePath
}

# 2. Initialize and update submodule
Write-Host "[SETUP] Initializing Submodule..." -ForegroundColor Yellow
git submodule update --init --recursive

# 3. Instructions
Write-Host ""
Write-Host "[OK] Submodule Setup Complete!" -ForegroundColor Green
Write-Host ""
Write-Host "[INFO] Next Steps:" -ForegroundColor Cyan
Write-Host "  1. Keep docs in $docsSourceDir/docs/" -ForegroundColor White
Write-Host "  2. Run Local Dev: pwsh ./scripts/build-docs-local.ps1" -ForegroundColor White
Write-Host "  3. Build Docker Image: docker build -t build-agent:latest ." -ForegroundColor White
Write-Host ""
Write-Host "[INFO] Note:" -ForegroundColor Cyan
Write-Host "  - Documentation remains in documentation/docs/" -ForegroundColor White
Write-Host "  - The template reads docs directly from documentation/" -ForegroundColor White
Write-Host ""
Write-Host "[INFO] To Update Template:" -ForegroundColor Cyan
Write-Host "  cd docs-template" -ForegroundColor White
Write-Host "  git pull" -ForegroundColor White
Write-Host "  cd .." -ForegroundColor White
Write-Host "  git add docs-template" -ForegroundColor White
Write-Host "  git commit -m 'Update Template to Latest'" -ForegroundColor White
Write-Host ""