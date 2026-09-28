#Requires -Version 5.1
[CmdletBinding()]
param()

# --- Module Configuration ---
# The default image is pinned to this module's own version (never `latest`); an override is used verbatim.
$script:ModuleVersion = [regex]::Match((Get-Content -LiteralPath (Join-Path $PSScriptRoot 'Docker-BuildAgent.psd1') -Raw), "(?m)^\s*ModuleVersion\s*=\s*'([^']+)'").Groups[1].Value

$script:BuildAgentConfig = @{
    DockerImage   = "ghcr.io/the-running-dev/build-agent:$script:ModuleVersion"
    DockerHost    = "tcp://host.docker.internal:2375"
    WorkspacePath = $PSScriptRoot
    ArtifactsDir  = "artifacts"
    Environment   = "development"
    Parameters    = @{}
}

# --- Configuration Management ---
function Set-BuildAgentConfig {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)]
        [string]$DockerImage,

        [Parameter(Mandatory = $true)]
        [ValidatePattern('^(tcp://[^\s:]+:\d+|unix:///[^\s]+|npipe:////\./pipe/[^\s]+)$')]
        [string]$DockerHost,

        [Parameter(Mandatory = $true)]
        [string]$WorkspacePath,

        [string]$ArtifactsDir = "./artifacts",

        [ValidateSet('development', 'production')]
        [string]$Environment = 'development',

        [hashtable]$AdditionalParameters = @{}
    )

    if (-not (Test-Path -LiteralPath $WorkspacePath -PathType Container)) {
        $PSCmdlet.ThrowTerminatingError((New-LauncherError -Code WorkspaceInvalid -ExitCode 3 -Message "Workspace path '$WorkspacePath' does not exist or is not a directory."))
    }

    $script:BuildAgentConfig.DockerImage = $DockerImage
    $script:BuildAgentConfig.DockerHost = $DockerHost
    $script:BuildAgentConfig.WorkspacePath = $WorkspacePath
    $script:BuildAgentConfig.ArtifactsDir = $ArtifactsDir
    $script:BuildAgentConfig.Environment = $Environment
    $script:BuildAgentConfig.Parameters = $AdditionalParameters

    Write-Host "[OK] Build Agent Configuration Updated." -ForegroundColor Green
}

# --- Private Helper Functions ---

# A launcher error carries its stable code as FullyQualifiedErrorId and its process exit status in
# Exception.Data['ExitCode'], so a caller can map it to a process exit without parsing text.
function New-LauncherError {
    param(
        [string]$Code,
        [int]$ExitCode,
        [string]$Message
    )

    $exception = New-Object System.InvalidOperationException($Message)
    $exception.Data['Code'] = $Code
    $exception.Data['ExitCode'] = $ExitCode
    return New-Object System.Management.Automation.ErrorRecord($exception, $Code, [System.Management.Automation.ErrorCategory]::InvalidOperation, $null)
}

# The only place `docker` is called. With -Capture the output is collected; without it the output
# streams to the host (the build's own output) and only the exit status is returned.
function Invoke-Docker {
    param(
        [string[]]$Arguments,
        [switch]$Capture
    )

    if ($Capture) {
        $previous = $ErrorActionPreference
        $ErrorActionPreference = 'Continue'
        try { $output = & docker @Arguments 2>&1 | ForEach-Object { "$_" } }
        catch { $output = @("$_"); $global:LASTEXITCODE = 1 }
        finally { $ErrorActionPreference = $previous }
        return [pscustomobject]@{ ExitCode = $LASTEXITCODE; Output = @($output) }
    }

    & docker @Arguments | Out-Host
    return [pscustomobject]@{ ExitCode = $LASTEXITCODE; Output = @() }
}

# Makes the configured image present locally. Never substitutes another reference (I27).
function Confirm-BuildAgentImage {
    param([string]$Image)

    if ((Invoke-Docker -Arguments @('image', 'inspect', $Image) -Capture).ExitCode -eq 0) { return }

    $pull = Invoke-Docker -Arguments @('pull', $Image) -Capture
    if ($pull.ExitCode -eq 0) { return }

    if ((Invoke-Docker -Arguments @('version', '--format', '{{.Server.Version}}') -Capture).ExitCode -ne 0) {
        return New-LauncherError -Code DockerUnavailable -ExitCode 5 -Message "Docker is not available: $($pull.Output -join ' ')"
    }
    return New-LauncherError -Code ImageUnavailable -ExitCode 5 -Message "Image '$Image' could not be obtained: $($pull.Output -join ' ')"
}

function Convert-ToKebabCase {
    param([string]$inputString)

    return ($inputString -replace '([a-z])([A-Z])', '$1-$2').ToLower()
}

# --- Build Invocation ---

function Get-BuildConfigName {
    param([string]$type)

    switch ($type) {
        'docker' { return 'Docker' }
        'node' { return 'Node' }
        'node-in-docker' { return 'NodeInDocker' }
        'node-template' { return 'NodeTemplate' }
        'forge' { return 'Forge' }
        default { return $type }
    }
}

function Convert-HashtableToArgs {
    param([hashtable]$parameters)

    $result = @()
    foreach ($key in $parameters.Keys) {
        $value = $parameters[$key]
        if ($null -eq $value) { continue }

        $kebab = Convert-ToKebabCase -InputString $key
        if ($value -is [System.Collections.IEnumerable] -and -not ($value -is [string])) {
            foreach ($item in $value) {
                $result += "--$kebab"
                $result += "$item"
            }
        }
        else {
            $result += "--$kebab"
            $result += "$value"
        }
    }
    return $result
}

function Get-AllowedParametersForType {
    param([string]$type)

    $parametersJsonPath = Join-Path $PSScriptRoot "parameters.json"
    if (-not (Test-Path $parametersJsonPath)) {
        return @()
    }

    $buildConfigs = Get-Content $parametersJsonPath -Raw | ConvertFrom-Json
    $configName = Get-BuildConfigName -Type $type
    $config = $buildConfigs | Where-Object { $_.Name -eq $configName } | Select-Object -First 1

    if (-not $config) { return @() }

    return $config.Parameters | ForEach-Object { $_.Name }
}

function Invoke-Build {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)]
        [ValidateSet('docker', 'node', 'node-in-docker', 'node-template', 'forge')]
        [string]$type,

        [Alias('args')]
        [hashtable]$buildArgs = @{},

        [switch]$validateArgs
    )

    Write-Host "Executing '$type' build..."

    $mergedArgs = @{}
    foreach ($key in $script:BuildAgentConfig.Parameters.Keys) {
        $mergedArgs[$key] = $script:BuildAgentConfig.Parameters[$key]
    }
    foreach ($key in $buildArgs.Keys) {
        $mergedArgs[$key] = $buildArgs[$key]
    }

    if (($type -in @('node', 'node-in-docker', 'node-template')) -and
        -not $mergedArgs.ContainsKey('artifactsDir') -and
        -not [string]::IsNullOrWhiteSpace($script:BuildAgentConfig.ArtifactsDir)) {
        $mergedArgs['artifactsDir'] = $script:BuildAgentConfig.ArtifactsDir
    }

    if ($validateArgs) {
        $allowed = Get-AllowedParametersForType -Type $type
        if ($allowed.Count -gt 0) {
            $unknown = $mergedArgs.Keys | Where-Object { $_ -notin $allowed }
            if ($unknown.Count -gt 0) {
                throw "Unknown parameter(s) for '$Type': $($unknown -join ', ')"
            }
        }
    }

    if (-not (Test-Path -LiteralPath $script:BuildAgentConfig.WorkspacePath -PathType Container)) {
        $PSCmdlet.ThrowTerminatingError((New-LauncherError -Code WorkspaceInvalid -ExitCode 3 -Message "Workspace path '$($script:BuildAgentConfig.WorkspacePath)' does not exist or is not a directory."))
    }

    $imageError = Confirm-BuildAgentImage -Image $script:BuildAgentConfig.DockerImage
    if ($imageError) { $PSCmdlet.ThrowTerminatingError($imageError) }

    $argsList = @(
        "run", "--rm",
        "-v", "$($script:BuildAgentConfig.WorkspacePath):/workspace",
        "-w", "/workspace",
        "-e", "DOCKER_HOST=$($script:BuildAgentConfig.DockerHost)",
        "$($script:BuildAgentConfig.DockerImage)"
    )

    if ($script:BuildAgentConfig.DockerHost -like 'unix:///*') {
        $socketPath = $script:BuildAgentConfig.DockerHost.Substring('unix://'.Length)
        $argsList = @(
            "run", "--rm",
            "-v", "$($script:BuildAgentConfig.WorkspacePath):/workspace",
            "-v", "$socketPath`:$socketPath",
            "-w", "/workspace",
            "-e", "DOCKER_HOST=$($script:BuildAgentConfig.DockerHost)",
            "$($script:BuildAgentConfig.DockerImage)"
        )
    }

    $argsList += @(
        "build", "$type"
    )

    $argsList += Convert-HashtableToArgs -Parameters $mergedArgs

    Write-Host "Executing: docker run ... build $type [arguments hidden for security]"
    $run = Invoke-Docker -Arguments $argsList

    if ($run.ExitCode -ne 0) {
        $PSCmdlet.ThrowTerminatingError((New-LauncherError -Code BuildFailed -ExitCode $run.ExitCode -Message "Docker invocation failed with exit code $($run.ExitCode)"))
    }
}

Export-ModuleMember -Function 'Set-BuildAgentConfig', 'Invoke-Build' -Variable 'BuildAgentConfig'
