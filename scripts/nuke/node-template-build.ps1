#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Builds a Node.js documentation app from a template repository, copying only missing files and preserving existing customizations.

.DESCRIPTION
    This script automates the setup of Node.js documentation projects (typically Docusaurus) by:
    1. Cloning a template repository from GitHub
    2. Copying template files to the target directory (preserving existing files)
    3. Executing setup scripts for customization
    4. Auto-detecting or using specified package manager (npm, pnpm, yarn)
    5. Installing dependencies and building for production if requested
    
    The script is designed to be non-destructive, allowing teams to update templates
    without losing custom configurations or content.

.PARAMETER WorkingDir
    The root directory of the project where the documentation will be created.
    Defaults to the current directory. Must be a valid directory path.

.PARAMETER AppDir
    The directory name for the documentation app (relative to WorkingDir).
    This is where the template files will be copied and the app will be built.
    Defaults to 'documentation'.

.PARAMETER NodeTemplateRepositoryUrl
    The Git repository URL containing the documentation template.
    Should be a public repository accessible via HTTPS.
    Defaults to 'https://github.com/The-Running-Dev/Docusaurus-Template.git'.

.PARAMETER NodeTemplateDirPath
    The local directory path where the template repository will be cloned.
    This is a temporary directory that gets created during the build process.
    Defaults to '/node-template'. The directory is not cleaned up automatically.

.PARAMETER SkipInstall
    When specified, skips running the package manager install command.
    Useful for CI/CD scenarios where dependencies are installed separately,
    or when only template files need to be updated.

.PARAMETER PackageManager
    Manually specify which package manager to use (npm, pnpm, or yarn).
    If not specified, the script will auto-detect based on lock files in the target directory.
    Auto-detection priority: pnpm-lock.yaml > yarn.lock > defaults to npm.

.PARAMETER IsProduction
    Controls whether to build the documentation for production deployment.
    When $true (default), runs 'build:prod' script after installing dependencies.
    When $false, only installs dependencies without building.

.EXAMPLE
    ./node-template-build -AppDir docs-ui
    # Creates documentation in docs-ui directory, auto-detects package manager, 
    # installs dependencies, and builds for production

.EXAMPLE
    ./node-template-build -PackageManager pnpm -SkipInstall -IsProduction:$false
    # Uses pnpm as package manager, skips dependency installation, 
    # and doesn't build for production (useful for development setup)

.EXAMPLE
    ./node-template-build -WorkingDir /workspace -AppDir documentation -IsProduction
    # Creates documentation in /workspace/documentation directory,
    # installs dependencies and builds for production deployment

.EXAMPLE
    ./node-template-build -NodeTemplateRepositoryUrl "https://github.com/custom/template.git" -AppDir custom-docs
    # Uses a custom template repository instead of the default Docusaurus template

.NOTES
    Version: 2.2
    Author: Docker-BuildAgent Project
    Dependencies: Git, PowerShell 5.1+, Node.js with npm/pnpm/yarn, nuke-helpers.psm1
    
    The script expects:
    - Git to be available in PATH for cloning the template repository
    - Node.js and a package manager (npm/pnpm/yarn) to be installed
    - Network access to clone the template repository
    - Write permissions in the target directory
    - nuke-helpers.psm1 module for enhanced error handling and build operations
    
    Optional files that enhance functionality:
    - set-environment.ps1: Executed to set up environment variables
    - template-setup.ps1: Executed after copying files for custom setup
    - .copy.ignore: In template repository to exclude specific files from copying
    
    Error Handling:
    - All critical operations use Invoke-SafeCommand for automatic error handling
    - Git clone failures will terminate the script with detailed error information
    - Package manager operations are validated before execution
    - File system operations include proper path validation

.LINK
    https://github.com/The-Running-Dev/Docker-BuildAgent
    https://github.com/The-Running-Dev/Docusaurus-Template
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $false, HelpMessage = "Root directory of the project")]
    [ValidateNotNullOrEmpty()]
    [string]$WorkingDir = $(Convert-Path .),

    [Parameter(Mandatory = $false, HelpMessage = "Directory name for the documentation app")]
    [ValidateNotNullOrEmpty()]
    [string]$AppDir = 'documentation',
    
    [Parameter(Mandatory = $false, HelpMessage = "Git repository URL for the template")]
    [ValidateNotNullOrEmpty()]
    [string]$NodeTemplateRepositoryUrl = 'https://github.com/The-Running-Dev/Docusaurus-Template.git',
    
    [Parameter(Mandatory = $false, HelpMessage = "Local path where template will be cloned")]
    [ValidateNotNullOrEmpty()]
    [string]$NodeTemplateDirPath = '/node-template',
    
    [Parameter(Mandatory = $false, HelpMessage = "Skip package manager install step")]
    [switch]$SkipInstall = $false,
    
    [Parameter(Mandatory = $false, HelpMessage = "Package manager to use (npm, pnpm, yarn)")]
    [ValidateSet('npm', 'pnpm', 'yarn', '')]
    [string]$PackageManager,

    [Parameter(Mandatory = $false, HelpMessage = "Build for production deployment")]
    [switch]$IsProduction
)
# Import the helper module for common build operations
Import-Module (Join-Path $PSScriptRoot 'nuke-helpers.psm1') -Force

# Set default value for production build if not specified
if (-not $PSBoundParameters.ContainsKey('IsProduction')) {
    $IsProduction = $true
}

# Define file paths and setup variables
$templateSetupFile = "template-setup.ps1"
$templateSetupFilePath = Join-Path $WorkingDir $templateSetupFile
$templateBuildFile = "template-build.ps1"
$templateBuildFilePath = Join-Path $WorkingDir $templateBuildFile

$appDirPath = Join-Path $WorkingDir $AppDir -Resolve

#region Template Repository Setup
Write-Host "[CLONE] Cloning Template..." -ForegroundColor Cyan
Write-Host "   Repository: $NodeTemplateRepositoryUrl" -ForegroundColor Gray
Write-Host "   Destination: $NodeTemplateDirPath" -ForegroundColor Gray

# Clone the template repository with minimal history for faster download
# --depth 1 creates a shallow clone with only the latest commit
Invoke-SafeCommand {
    # Parse repository URL and branch if specified with #branch format
    if ($NodeTemplateRepositoryUrl -match '^(.+?)#(.+)$') {
        # URL contains a branch specification
        $repoUrl = $Matches[1]
        $branch = $Matches[2]
        
        Write-Host "   Using branch: $branch" -ForegroundColor Gray
        
        # Clone with specific branch - don't suppress errors
        git clone --depth 1 -b $branch $repoUrl $NodeTemplateDirPath
        
        # Throw an error if git clone fails
        if ($LASTEXITCODE -ne 0) {
            throw "Git Clone Failed: $LASTEXITCODE"
        }
    } else {
        # Regular clone without branch specification - don't suppress errors
        git clone --depth 1 $NodeTemplateRepositoryUrl $NodeTemplateDirPath
        
        # Throw an error if git clone fails
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
Write-Host "   Working Directory: $WorkingDir" -ForegroundColor Gray
Write-Host "   App Directory: $AppDir" -ForegroundColor Gray
Write-Host "   App Path: $appDirPath" -ForegroundColor Gray
Write-Host "   Template URL: $NodeTemplateRepositoryUrl" -ForegroundColor Gray
Write-Host "   Template Path: $NodeTemplateDirPath" -ForegroundColor Gray
Write-Host "   Skip Install: $SkipInstall" -ForegroundColor Gray
Write-Host "   Production Build: $IsProduction" -ForegroundColor Gray
Write-Host ""
#endregion

#region Template File Copying
Write-Host "[COPY] Copying Template Files..." -ForegroundColor Cyan
Write-Host "   From: $NodeTemplateDirPath" -ForegroundColor Gray
Write-Host "   To: $appDirPath" -ForegroundColor Gray
Write-Host "   Mode: Preserve Existing Files" -ForegroundColor Gray

# Copy template files to the target directory
# -overwrite:$false ensures existing files are preserved, allowing customizations to persist
Copy-Directory `
    -sourceDir $NodeTemplateDirPath `
    -destinationDir (Join-Path $WorkingDir $AppDir) `
    -overwrite:$false

Write-Host "[OK] Template Files Copied Successfully" -ForegroundColor Green
#endregion

#region Template Setup Script Execution
# Execute the template-specific setup script if it was copied from the template
# This script typically handles template customization and configuration
Invoke-Script `
    -WorkingDir $WorkingDir `
    -ScriptFile $templateSetupFile `
    -Message "Running Template Setup..."

# Clean up the template setup script after execution
# This prevents it from being committed to the project repository
if (Test-Path $templateSetupFilePath) {
    Write-Host "[CLEAN] Removing Template Setup Script..." -ForegroundColor Yellow
    
    Invoke-SafeCommand {
        Remove-Item $templateSetupFilePath -Force
    }
    
    Write-Host "[OK] Template Setup Script Removed" -ForegroundColor Green
}
#endregion

#region Package Manager Setup and Installation
# Change to the app directory for package manager operations
Write-Host "[SETUP] Preparing Package Management..." -ForegroundColor Cyan
Set-Location $appDirPath

# Auto-detect package manager if not explicitly specified
# Detection is based on lock files: pnpm-lock.yaml > yarn.lock > defaults to npm
if (-not $PackageManager) {
    $PackageManager = Get-PackageManager -ProjectDir $appDirPath
}
else {
    Write-Host "[CONFIG] Using Specified Package Manager: $PackageManager" -ForegroundColor Green
}

# Install dependencies unless explicitly skipped
if (-not $SkipInstall) {
    Write-Host "[INSTALL] Installing Dependencies..." -ForegroundColor Cyan
    Write-Host "   Command: $PackageManager install" -ForegroundColor Gray

    Invoke-SafeCommand {
        & $PackageManager install
    }
    
    Write-Host "[OK] Dependencies Installed Successfully" -ForegroundColor Green
}
else {
    Write-Host "[SKIP] Skipping Dependency Installation (skipInstall Flag Specified)" -ForegroundColor Yellow
}
#endregion

#region Production Build
# Build for production if requested (default behavior)
if ($IsProduction) {
    Write-Host "[BUILD] Building for Production..." -ForegroundColor Cyan
    Write-Host "   Command: $PackageManager run build:prod" -ForegroundColor Gray

    Invoke-SafeCommand {
        & $PackageManager run build:prod
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

if ($IsProduction) {
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