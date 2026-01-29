#Requires -Version 5.1
[CmdletBinding()]
param()

# --- Module Configuration ---
$script:BuildAgentConfig = @{
    DockerImage   = "ghcr.io/the-running-dev/build-agent:latest"
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
        [ValidatePattern('^tcp://.*:\d+$')]
        [string]$DockerHost,

        [Parameter(Mandatory = $true)]
        [ValidateScript({ Test-Path $_ -PathType Container })]
        [string]$WorkspacePath,

        [string]$ArtifactsDir = "./artifacts",

        [ValidateSet('development', 'production')]
        [string]$Environment = 'development',

        [hashtable]$AdditionalParameters = @{}
    )

    $script:BuildAgentConfig.DockerImage = $DockerImage
    $script:BuildAgentConfig.DockerHost = $DockerHost
    $script:BuildAgentConfig.WorkspacePath = $WorkspacePath
    $script:BuildAgentConfig.ArtifactsDir = $ArtifactsDir
    $script:BuildAgentConfig.Environment = $Environment
    $script:BuildAgentConfig.Parameters = $AdditionalParameters

    Write-Host "[OK] Build Agent Configuration Updated." -ForegroundColor Green
}

# --- Private Helper Functions ---

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

    $result = @{}
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

    $buildConfigs = Get-Content $parametersJsonPath | ConvertFrom-Json
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

        [hashtable]$args = @{},

        [switch]$validateArgs
    )

    Write-Host "Executing '$type' build..."

    $mergedArgs = @{}
    foreach ($key in $script:BuildAgentConfig.Parameters.Keys) {
        $mergedArgs[$key] = $script:BuildAgentConfig.Parameters[$key]
    }
    foreach ($key in $args.Keys) {
        $mergedArgs[$key] = $args[$key]
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

    $argsList = @(
        "run", "--rm",
        "-v", "`"$($script:BuildAgentConfig.WorkspacePath):/workspace`"",
        "-w", "/workspace",
        "-e", "DOCKER_HOST=$($script:BuildAgentConfig.DockerHost)",
        "$($script:BuildAgentConfig.DockerImage)",
        "build", "$type"
    )

    $argsList += Convert-HashtableToArgs -Parameters $mergedArgs

    Write-Host "Executing: docker $($argsList -join ' ')"
    & docker @argsList

    if ($LASTEXITCODE -ne 0) { throw "Docker invocation failed with exit code $LASTEXITCODE" }
}

Export-ModuleMember -Function 'Set-BuildAgentConfig', 'Invoke-Build' -Variable 'BuildAgentConfig'
