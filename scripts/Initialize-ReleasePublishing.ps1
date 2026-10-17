#requires -Version 5.1

<#
.SYNOPSIS
    Sets up everything the Release and Release-from-Tag workflows need to publish, and can start a release.

.DESCRIPTION
    Idempotent: every step checks the current state first and changes only what differs, so the script
    can be re-run until the summary shows no gaps.

    It sets up, through the GitHub CLI (gh):
      - the `release` environment, deployable from the branch `main` and from `v*` tags, with a required
        reviewer (the signed-in GitHub user unless -Reviewer or -NoReviewer says otherwise)
      - the environment variable NUGET_USER, the nuget.org profile name used for Trusted Publishing
      - the environment secret PSGALLERY_API_KEY, when -PSGalleryApiKey is given

    It checks that REGISTRY_TOKEN exists and that main's publish-release action uses NuGet Trusted
    Publishing. nuget.org has no API for trusted publishing policies, so the two policies (one per release
    workflow file) are printed for you to add; -OpenBrowser opens the pages where they and the PowerShell
    Gallery key are created.

    With -StartRelease it dispatches the Release workflow on main, after every check it can make passed.

    The script never prompts. Pass the PowerShell Gallery key as a SecureString, for example:
        -PSGalleryApiKey (Read-Host -AsSecureString 'PowerShell Gallery API key')

.PARAMETER Repository
    owner/name of the repository. Defaults to The-Running-Dev/Docker-BuildAgent.

.PARAMETER NuGetUser
    The nuget.org profile name (not the email address) that owns BuildAgent.Tool and the trusted
    publishing policies. Required the first time; later runs keep the stored value.

.PARAMETER PSGalleryApiKey
    The PowerShell Gallery API key, scoped to push Docker-BuildAgent. Stored as the environment secret
    PSGALLERY_API_KEY, replacing any earlier value.

.PARAMETER Reviewer
    GitHub login that must approve each release run. Defaults to the signed-in gh user.

.PARAMETER NoReviewer
    Release runs start without approval.

.PARAMETER OpenBrowser
    Open the nuget.org trusted publishing page and the PowerShell Gallery API key page.

.PARAMETER StartRelease
    Dispatch the Release workflow on main once setup is complete.

.PARAMETER PreReleaseLabel
    Pre-release label for -StartRelease (letters and digits, e.g. rc1). Omit for a stable release.

.PARAMETER Watch
    With -StartRelease, follow the run until it finishes and exit with its result.

.EXAMPLE
    pwsh scripts/Initialize-ReleasePublishing.ps1 -NuGetUser TheRunningDev -OpenBrowser

.EXAMPLE
    pwsh scripts/Initialize-ReleasePublishing.ps1 -PSGalleryApiKey (Read-Host -AsSecureString 'PowerShell Gallery API key')

.EXAMPLE
    pwsh scripts/Initialize-ReleasePublishing.ps1 -StartRelease -PreReleaseLabel rc1 -Watch

.EXAMPLE
    pwsh scripts/Initialize-ReleasePublishing.ps1 -NuGetUser TheRunningDev -WhatIf
#>
[CmdletBinding(SupportsShouldProcess)]
param(
    [string]$Repository = 'The-Running-Dev/Docker-BuildAgent',
    [string]$NuGetUser,
    [securestring]$PSGalleryApiKey,
    [string]$Reviewer,
    [switch]$NoReviewer,
    [switch]$OpenBrowser,
    [switch]$StartRelease,
    [ValidatePattern('^[A-Za-z0-9]*$')]
    [string]$PreReleaseLabel,
    [switch]$Watch
)

$ErrorActionPreference = 'Stop'

$environmentName = 'release'
$workflowFiles = @('release.yml', 'release-tag.yml')
$deploymentPolicies = @(
    @{ name = 'main'; type = 'branch' },
    @{ name = 'v*'; type = 'tag' }
)
$nuGetPolicyUrl = 'https://www.nuget.org/account/trustedpublishing'
$galleryKeyUrl = 'https://www.powershellgallery.com/account/apikeys'

$summary = New-Object System.Collections.Generic.List[object]
function Add-Result([string]$item, [string]$state, [string]$detail) {
    $summary.Add([pscustomobject]@{ Item = $item; State = $state; Detail = $detail })
}

# Runs gh and returns its standard output; throws with everything gh wrote when it fails. gh is judged
# by its exit code alone: under Stop, Windows PowerShell would turn its stderr into a terminating error.
function Invoke-Gh {
    param([Parameter(Mandatory)][string[]]$Arguments, [string]$InputText, [switch]$AllowNotFound)
    $ErrorActionPreference = 'Continue'
    $output = if ($PSBoundParameters.ContainsKey('InputText')) {
        $InputText | & gh @Arguments 2>&1
    } else {
        & gh @Arguments 2>&1
    }
    if ($LASTEXITCODE -ne 0) {
        $text = ($output | ForEach-Object { "$_" }) -join [Environment]::NewLine
        if ($AllowNotFound -and $text -match 'HTTP 404') { return $null }
        throw "gh $($Arguments -join ' ') failed: $text"
    }
    return @($output | Where-Object { $_ -isnot [System.Management.Automation.ErrorRecord] })
}

# GETs an API path as an object; $null when it answers 404.
function Get-GhJson([string]$path) {
    $output = Invoke-Gh -Arguments @('api', $path) -AllowNotFound
    if ($null -eq $output) { return $null }
    return ($output -join "`n") | ConvertFrom-Json
}

function ConvertTo-PlainText([securestring]$secure) {
    $pointer = [Runtime.InteropServices.Marshal]::SecureStringToBSTR($secure)
    try { return [Runtime.InteropServices.Marshal]::PtrToStringBSTR($pointer) }
    finally { [Runtime.InteropServices.Marshal]::ZeroFreeBSTR($pointer) }
}

# --- Preconditions -------------------------------------------------------------------------------

if (-not (Get-Command gh -ErrorAction SilentlyContinue)) {
    throw 'The GitHub CLI (gh) is not on PATH. Install it from https://cli.github.com and run gh auth login.'
}
try { Invoke-Gh @('auth', 'status') | Out-Null } catch { throw 'gh is not signed in. Run gh auth login, then re-run this script.' }

$repo = Get-GhJson "repos/$Repository"
if (-not $repo) { throw "Repository $Repository was not found, or this gh account cannot see it." }
if (-not $repo.permissions.admin) {
    throw "Setting up environments needs admin rights on $Repository; the signed-in gh account does not have them."
}

# --- The release environment ---------------------------------------------------------------------

$reviewerIds = @()
if (-not $NoReviewer) {
    $reviewerLogin = if ($Reviewer) { $Reviewer } else { (Invoke-Gh @('api', 'user', '--jq', '.login')) -join '' }
    $reviewerUser = Get-GhJson "users/$reviewerLogin"
    if (-not $reviewerUser) { throw "GitHub user $reviewerLogin was not found." }
    $reviewerIds = @([int64]$reviewerUser.id)
}

$environment = Get-GhJson "repos/$Repository/environments/$environmentName"
$currentReviewerIds = @()
if ($environment) {
    $currentReviewerIds = @($environment.protection_rules |
        Where-Object { $_.type -eq 'required_reviewers' } |
        ForEach-Object { $_.reviewers } |
        ForEach-Object { [int64]$_.reviewer.id })
}
$policy = if ($environment) { $environment.deployment_branch_policy } else { $null }
$environmentCurrent = $environment -and $policy -and $policy.custom_branch_policies -and
    -not $policy.protected_branches -and
    ((@($currentReviewerIds | Sort-Object) -join ",") -eq (@($reviewerIds | Sort-Object) -join ","))

if ($environmentCurrent) {
    Add-Result 'Environment' 'OK' "'$environmentName' exists with the expected protection"
} elseif ($PSCmdlet.ShouldProcess("$Repository environment '$environmentName'", 'Create or update')) {
    $body = @{
        wait_timer               = 0
        prevent_self_review      = $false
        reviewers                = @($reviewerIds | ForEach-Object { @{ type = 'User'; id = $_ } })
        deployment_branch_policy = @{ protected_branches = $false; custom_branch_policies = $true }
    } | ConvertTo-Json -Depth 5 -Compress
    Invoke-Gh -Arguments @('api', '-X', 'PUT', "repos/$Repository/environments/$environmentName", '--input', '-') -InputText $body | Out-Null
    $reviewerText = if ($reviewerIds.Count) { "reviewer $reviewerLogin" } else { 'no reviewer' }
    Add-Result 'Environment' 'SET' "'$environmentName' created or updated ($reviewerText)"
} else {
    Add-Result 'Environment' 'PENDING' "'$environmentName' would be created or updated"
}

$existingPolicies = @()
$policyList = Get-GhJson "repos/$Repository/environments/$environmentName/deployment-branch-policies"
if ($policyList) { $existingPolicies = @($policyList.branch_policies) }
foreach ($wanted in $deploymentPolicies) {
    $label = "Deploys from $($wanted.type) $($wanted.name)"
    $present = $existingPolicies | Where-Object { $_.name -eq $wanted.name -and $_.type -eq $wanted.type }
    if ($present) {
        Add-Result $label 'OK' ''
    } elseif ($PSCmdlet.ShouldProcess("$Repository environment '$environmentName'", "Allow $($wanted.type) $($wanted.name)")) {
        $body = $wanted | ConvertTo-Json -Compress
        Invoke-Gh -Arguments @('api', '-X', 'POST', "repos/$Repository/environments/$environmentName/deployment-branch-policies", '--input', '-') -InputText $body | Out-Null
        Add-Result $label 'SET' ''
    } else {
        Add-Result $label 'PENDING' ''
    }
}

# --- NUGET_USER and PSGALLERY_API_KEY ------------------------------------------------------------

$variables = Get-GhJson "repos/$Repository/environments/$environmentName/variables"
$storedNuGetUser = if ($variables) { ($variables.variables | Where-Object { $_.name -eq 'NUGET_USER' }).value } else { $null }
if ($NuGetUser -and $NuGetUser -ne $storedNuGetUser) {
    if ($PSCmdlet.ShouldProcess("$Repository environment '$environmentName'", "Set variable NUGET_USER to $NuGetUser")) {
        Invoke-Gh @('variable', 'set', 'NUGET_USER', '--env', $environmentName, '--body', $NuGetUser, '--repo', $Repository) | Out-Null
        $storedNuGetUser = $NuGetUser
        Add-Result 'NUGET_USER' 'SET' $NuGetUser
    } else {
        Add-Result 'NUGET_USER' 'PENDING' "would be set to $NuGetUser"
    }
} elseif ($storedNuGetUser) {
    Add-Result 'NUGET_USER' 'OK' $storedNuGetUser
} else {
    Add-Result 'NUGET_USER' 'MISSING' 'pass -NuGetUser <nuget.org profile name>'
}

if ($PSGalleryApiKey) {
    if ($PSCmdlet.ShouldProcess("$Repository environment '$environmentName'", 'Set secret PSGALLERY_API_KEY')) {
        # Through stdin, so the key never appears on a command line.
        Invoke-Gh -Arguments @('secret', 'set', 'PSGALLERY_API_KEY', '--env', $environmentName, '--repo', $Repository) -InputText (ConvertTo-PlainText $PSGalleryApiKey) | Out-Null
        Add-Result 'PSGALLERY_API_KEY' 'SET' "environment '$environmentName'"
    } else {
        Add-Result 'PSGALLERY_API_KEY' 'PENDING' 'would be stored'
    }
}

# Secret names only; their values cannot be read back.
$environmentSecrets = @()
$repositorySecrets = @((Get-GhJson "repos/$Repository/actions/secrets").secrets.name)
$secretList = Get-GhJson "repos/$Repository/environments/$environmentName/secrets"
if ($secretList) { $environmentSecrets = @($secretList.secrets.name) }
foreach ($name in @('REGISTRY_TOKEN', 'PSGALLERY_API_KEY')) {
    if ($summary | Where-Object { $_.Item -eq $name }) { continue }
    if ($environmentSecrets -contains $name) {
        Add-Result $name 'OK' "environment '$environmentName'"
    } elseif ($repositorySecrets -contains $name) {
        Add-Result $name 'OK' 'repository'
    } else {
        $hint = if ($name -eq 'PSGALLERY_API_KEY') { "pass -PSGalleryApiKey (key from $galleryKeyUrl)" } else { 'a token that can push to ghcr.io' }
        Add-Result $name 'MISSING' $hint
    }
}
if ($repositorySecrets -contains 'NUGET_API_KEY' -or $environmentSecrets -contains 'NUGET_API_KEY') {
    Add-Result 'NUGET_API_KEY' 'UNUSED' 'Trusted Publishing replaced it; delete it with gh secret delete NUGET_API_KEY'
}

# --- The workflow on main ------------------------------------------------------------------------

$action = Get-GhJson "repos/$Repository/contents/.github/actions/publish-release/action.yml?ref=$($repo.default_branch)"
$actionText = if ($action) { [Text.Encoding]::UTF8.GetString([Convert]::FromBase64String($action.content)) } else { '' }
if ($actionText -match 'NuGet/login@') {
    Add-Result 'Workflow on main' 'OK' 'publish-release uses NuGet Trusted Publishing'
} else {
    Add-Result 'Workflow on main' 'MISSING' 'merge the Trusted Publishing change to main first'
}

# --- What only the browser can do ----------------------------------------------------------------

$policyOwner = if ($NuGetUser) { $NuGetUser } elseif ($storedNuGetUser) { $storedNuGetUser } else { '<your nuget.org profile name>' }
$repositoryOwner, $repositoryName = $Repository.Split('/')
Write-Host ''
Write-Host "nuget.org trusted publishing policies ($nuGetPolicyUrl) - add one per workflow file:" -ForegroundColor Cyan
foreach ($file in $workflowFiles) {
    Write-Host "  Policy owner: $policyOwner   Repository owner: $repositoryOwner   Repository: $repositoryName   Workflow file: $file   Environment: $environmentName"
}
Write-Host '  This script cannot see nuget.org policies; a missing one fails the run at "Log in to NuGet".'
Add-Result 'nuget.org policies' 'MANUAL' "$($workflowFiles -join ', ') with environment $environmentName"

if ($OpenBrowser) {
    Start-Process $nuGetPolicyUrl
    if (-not ($summary | Where-Object { $_.Item -eq 'PSGALLERY_API_KEY' -and $_.State -in 'OK', 'SET' })) {
        Write-Host "PowerShell Gallery key ($galleryKeyUrl): glob pattern Docker-BuildAgent, scope 'Push new packages and package versions'." -ForegroundColor Cyan
        Start-Process $galleryKeyUrl
    }
}

Write-Host ''
$summary | Format-Table -AutoSize | Out-String -Width 200 | Write-Host

# --- Start the release ---------------------------------------------------------------------------

$gaps = @($summary | Where-Object { $_.State -in 'MISSING', 'PENDING' })
if (-not $StartRelease) {
    if ($gaps.Count) { exit 1 }
    exit 0
}
if ($gaps.Count) {
    throw "Not starting a release: $(($gaps | ForEach-Object { $_.Item }) -join ', ') still need setting up."
}

$version = if ($PreReleaseLabel) { "the pre-release labelled $PreReleaseLabel" } else { 'a stable release' }
if (-not $PSCmdlet.ShouldProcess("$Repository main", "Dispatch release.yml ($version)")) { exit 0 }

$dispatchedAfter = (Get-Date).ToUniversalTime().AddSeconds(-5)
$runArguments = @('workflow', 'run', 'release.yml', '--repo', $Repository, '--ref', $repo.default_branch)
if ($PreReleaseLabel) { $runArguments += @('-f', "prerelease-label=$PreReleaseLabel") }
Invoke-Gh $runArguments | Out-Null

# The dispatch does not return the run, so find the one it created.
$run = $null
for ($attempt = 0; $attempt -lt 15 -and -not $run; $attempt++) {
    Start-Sleep -Seconds 2
    $runs = (Invoke-Gh @('run', 'list', '--workflow', 'release.yml', '--repo', $Repository, '--event', 'workflow_dispatch',
            '--limit', '5', '--json', 'databaseId,url,createdAt')) -join "`n" | ConvertFrom-Json
    $run = $runs | Where-Object { ([datetime]$_.createdAt).ToUniversalTime() -ge $dispatchedAfter } | Select-Object -First 1
}
if (-not $run) { throw "Release dispatched, but its run did not appear; see https://github.com/$Repository/actions/workflows/release.yml" }

Write-Host "Release run: $($run.url)" -ForegroundColor Green
if ($reviewerIds.Count -or $currentReviewerIds.Count) {
    Write-Host 'The Release job waits for approval: open the run and choose Review deployments.'
}
if ($Watch) {
    & gh run watch $run.databaseId --repo $Repository --exit-status
    exit $LASTEXITCODE
}
