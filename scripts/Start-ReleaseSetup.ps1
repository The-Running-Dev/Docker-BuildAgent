#requires -Version 5.1

<#
.SYNOPSIS
    Asks for the nuget.org profile name and the PowerShell Gallery key, then runs Initialize-ReleasePublishing.ps1.

.DESCRIPTION
    Initialize-ReleasePublishing.ps1 never prompts and takes the key as a SecureString, which cannot
    cross a process boundary: `pwsh <script> -PSGalleryApiKey (Read-Host -AsSecureString ...)` hands it
    the text "System.Security.SecureString" and fails. This script reads both values in the current
    session and calls it in-process, so run it with & or ./, not through pwsh.

    Leave an answer empty to keep the value already stored in the `release` environment.

.PARAMETER OpenBrowser
    Passed on: opens the nuget.org trusted publishing page and the PowerShell Gallery API key page.

.EXAMPLE
    ./scripts/Start-ReleaseSetup.ps1 -OpenBrowser
#>
[CmdletBinding()]
param(
    [switch]$OpenBrowser
)

$ErrorActionPreference = 'Stop'

$setup = @{}
$nuGetUser = Read-Host 'nuget.org profile name, not your email (empty keeps the stored one)'
if ($nuGetUser) { $setup.NuGetUser = $nuGetUser.Trim() }
$key = Read-Host -AsSecureString 'PowerShell Gallery API key (empty keeps the stored one)'
if ($key.Length -gt 0) { $setup.PSGalleryApiKey = $key }
if ($OpenBrowser) { $setup.OpenBrowser = $true }

& (Join-Path $PSScriptRoot 'Initialize-ReleasePublishing.ps1') @setup
