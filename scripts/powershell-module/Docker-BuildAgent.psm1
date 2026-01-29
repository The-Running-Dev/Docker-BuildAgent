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

    Write-Host "[OK] Build agent configuration updated." -ForegroundColor Green
}

# --- Private Helper Functions ---

function Convert-ToKebabCase {
    param([string]$InputString)
    return ($InputString -replace '([a-z])([A-Z])', '$1-$2').ToLower()
}

# --- Dynamic Function Generation ---

function New-BuildFunction {
    param(
        [string]$FunctionName,
        $Parameters
    )

    $paramBlock = $Parameters | ForEach-Object {
        @"
    # $($_.Description)
    [Parameter()]
    [string]`$($_.Name),
"@
    }
    $paramBlock = ($paramBlock -join "`n").TrimEnd(",`n")

    $argumentBlock = $Parameters | ForEach-Object {
        $kebabCase = Convert-ToKebabCase -InputString $_.Name
        "if (`$PSBoundParameters.ContainsKey('$($_.Name)')) { `$arguments += '--$kebabCase', `$($_.Name) }"
    }
    $argumentBlock = $argumentBlock -join "`n    "

    $nukeTargetName = $FunctionName -replace "Forge", ""

    $functionBody = @"
function Invoke-$FunctionName {
    [CmdletBinding()]
    param(
        $paramBlock
    )

    Write-Host "Executing '$nukeTargetName' build..."

    `$baseArgs = @(
        "run", "--rm",
        "-v", "`"$(`$script:BuildAgentConfig.WorkspacePath):/workspace`"",
        "-w", "/workspace",
        "-e", "DOCKER_HOST=`$(`$script:BuildAgentConfig.DockerHost)",
        "`$(`$script:BuildAgentConfig.DockerImage)"
    )

    `$nukeTarget = @("build", "$nukeTargetName")
    `$arguments = @()
    
    $argumentBlock

    `$command = "docker `" + (`$baseArgs + `$nukeTarget + `$arguments) -join " `"
    Write-Host "Executing command: `$command"
    Invoke-Expression -Command `$command
}
"@

    return [scriptblock]::Create($functionBody)
}

# Load parameters from JSON and generate functions
$parametersJsonPath = Join-Path $PSScriptRoot "parameters.json"
if (-not (Test-Path $parametersJsonPath)) {
    Write-Warning "parameters.json not found. Please run Update-ModuleParameters.ps1 from the module directory."
} else {
    $buildConfigs = Get-Content $parametersJsonPath | ConvertFrom-Json

    foreach ($config in $buildConfigs) {
        if ($config.Name -and $config.Parameters.Count -gt 0) {
            $functionName = "Forge" + $config.Name
            $scriptBlock = New-BuildFunction -FunctionName $functionName -Parameters $config.Parameters
            Invoke-Command -ScriptBlock $scriptBlock
            Export-ModuleMember -Function "Invoke-$functionName"
        }
    }
}

Export-ModuleMember -Function 'Set-BuildAgentConfig' -Variable 'BuildAgentConfig'
