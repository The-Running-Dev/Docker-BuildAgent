#Requires -Version 5.1
[CmdletBinding()]
param()

# --- Module Configuration ---
# The default image is pinned to this module's own version (never `latest`); an override is used verbatim.
# A pre-release module (PSData Prerelease = 'rc1') pins the image of the same pre-release, 2.1.0-rc1.
$script:ModuleManifestText = Get-Content -LiteralPath (Join-Path $PSScriptRoot 'Docker-BuildAgent.psd1') -Raw
$script:ModuleVersion = [regex]::Match($script:ModuleManifestText, "(?m)^\s*ModuleVersion\s*=\s*'([^']+)'").Groups[1].Value
$script:ModulePrerelease = [regex]::Match($script:ModuleManifestText, "(?m)^\s*Prerelease\s*=\s*'([^']+)'").Groups[1].Value
if ($script:ModulePrerelease) {
    $script:ModuleVersion = "$script:ModuleVersion-$script:ModulePrerelease"
}

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
    <#
    .SYNOPSIS
    Sets the configuration that Invoke-Build uses for the rest of the session.

    .DESCRIPTION
    Replaces every field of the session's build-agent configuration: the image to run, the Docker
    daemon the build talks to, the workspace to build, the artifacts directory, the environment
    label and the build parameters applied to every build. A field you do not pass is reset to the
    default shown for its parameter, not left at its previous value.

    The configuration is held in the module for the current session and is also visible as the
    exported $BuildAgentConfig variable. Before this is called it holds the module defaults: the
    image pinned to this module's own version (never latest), the daemon
    tcp://host.docker.internal:2375, the module directory as the workspace, an artifacts directory
    of "artifacts", the development environment and no parameters.

    The workspace path is checked before anything is changed, so a rejected call leaves the previous
    configuration in place. On success the command writes "[OK] Build Agent Configuration Updated."
    to the host.

    .PARAMETER DockerImage
    The image to run, used exactly as given. A reference you pass is never replaced with another
    version or with latest. Mandatory.

    .PARAMETER DockerHost
    The Docker daemon the build talks to, passed to the container as DOCKER_HOST. It must look like
    tcp://host:port, unix:///path or npipe:////./pipe/name, or parameter validation rejects it. For
    a unix:// host, Invoke-Build also mounts the socket into the container. Mandatory.

    .PARAMETER WorkspacePath
    The directory to build. It is mounted at /workspace in the container and must exist as a
    directory, otherwise the command fails with the WorkspaceInvalid error (exit status 3). A
    relative path is resolved against the current directory and stored as an absolute path.
    Mandatory.

    .PARAMETER ArtifactsDir
    The output directory for the node, node-in-docker and node-template build types. Invoke-Build
    passes it as the artifactsDir build parameter unless the build parameters already set one.
    Defaults to artifacts.

    .PARAMETER Environment
    The environment label, development or production. It is stored in the configuration and is not
    passed to the build. Defaults to development.

    .PARAMETER AdditionalParameters
    A hashtable of build parameters applied to every Invoke-Build. Parameters passed to a single
    Invoke-Build call through its buildArgs parameter win over these. Defaults to an empty hashtable.

    .EXAMPLE
    Set-BuildAgentConfig -DockerImage 'ghcr.io/the-running-dev/build-agent:2.0.0' -DockerHost 'tcp://host.docker.internal:2375' -WorkspacePath $PWD

    Points the build at the current directory, using a pinned image and the default daemon. The
    artifacts directory, environment and additional parameters take their defaults.

    .EXAMPLE
    Set-BuildAgentConfig -DockerImage 'example.com/team/agent:1.4.0' -DockerHost 'unix:///var/run/docker.sock' -WorkspacePath '/src/app' -ArtifactsDir './out' -Environment production -AdditionalParameters @{ imageTag = 'my-app' }

    Uses a custom image over the local Unix socket, writes Node artifacts to ./out, and applies the
    imageTag parameter to every build that follows.

    .EXAMPLE
    try {
        Set-BuildAgentConfig -DockerImage 'a/b:1' -DockerHost 'tcp://localhost:2375' -WorkspacePath 'C:\missing'
    }
    catch {
        exit $_.Exception.Data['ExitCode']
    }

    Handles a workspace that does not exist: the error id is WorkspaceInvalid and the exit status
    to use is 3.

    .INPUTS
    None. Set-BuildAgentConfig does not accept pipeline input.

    .OUTPUTS
    None. The command writes a confirmation message to the host and returns nothing.

    .NOTES
    A workspace path that is absent or is not a directory raises a terminating error whose
    FullyQualifiedErrorId is WorkspaceInvalid and whose Exception.Data['ExitCode'] is 3. A DockerHost
    that does not match an accepted form, or an Environment other than development or production,
    is rejected by parameter validation with an ordinary parameter binding error that carries no
    code.

    Canonical contract (PowerShell module): PSModule.requirements.md, R-CONFIG-001 to R-CONFIG-004.
    The PowerShell Module page of the project documentation describes the same behavior.
    #>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)]
        [string]$DockerImage,

        [Parameter(Mandatory = $true)]
        [ValidatePattern('^(tcp://[^\s:]+:\d+|unix:///[^\s]+|npipe:////\./pipe/[^\s]+)$')]
        [string]$DockerHost,

        [Parameter(Mandatory = $true)]
        [string]$WorkspacePath,

        [string]$ArtifactsDir = "artifacts",

        [ValidateSet('development', 'production')]
        [string]$Environment = 'development',

        [hashtable]$AdditionalParameters = @{}
    )

    if (-not (Test-Path -LiteralPath $WorkspacePath -PathType Container)) {
        $PSCmdlet.ThrowTerminatingError((New-LauncherError -Code WorkspaceInvalid -ExitCode 3 -Message "Workspace path '$WorkspacePath' does not exist or is not a directory."))
    }

    $script:BuildAgentConfig.DockerImage = $DockerImage
    $script:BuildAgentConfig.DockerHost = $DockerHost
    # Stored absolute: docker reads a bare relative name in -v as a named volume, not a path.
    $script:BuildAgentConfig.WorkspacePath = (Resolve-Path -LiteralPath $WorkspacePath).ProviderPath
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

    # -creplace: -replace ignores case, so [a-z][A-Z] would split every pair of letters.
    return ($inputString -creplace '([a-z])([A-Z])', '$1-$2').ToLower()
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
    <#
    .SYNOPSIS
    Runs one build in the build-agent container.

    .DESCRIPTION
    Runs `docker run --rm` against the image set by Set-BuildAgentConfig, with the workspace mounted
    at /workspace, DOCKER_HOST set, and `build <type>` followed by the build parameters.

    The command does the following, in order:

    1. Merges the parameters from Set-BuildAgentConfig with buildArgs. buildArgs wins.
    2. For the node, node-in-docker and node-template types, adds artifactsDir from the
       configuration unless you passed it and unless the configured value is blank.
    3. With validateArgs, rejects any key that is not a known parameter of the build type.
    4. Checks that the workspace is still a directory, then makes sure the image is present locally,
       pulling it if not. It never substitutes a different image, version or latest.
    5. Runs the container. For a unix:// Docker host it also mounts the socket into the container.

    Each parameter key becomes a kebab-case option, so imageTag becomes --image-tag. A list value
    becomes the option repeated once per item, and a null value is left out. The build's own output
    streams to the console. Parameter values are never printed: the command line is logged with its
    arguments hidden.

    A project configuration file in the workspace is read by the build itself, and values passed here
    rank above it.

    .PARAMETER type
    The build type: docker, node, node-in-docker, node-template or forge. Any other value is rejected
    by parameter validation. Mandatory.

    .PARAMETER buildArgs
    A hashtable of build parameters for this run, keyed by parameter name. These win over the
    AdditionalParameters set through Set-BuildAgentConfig. Also accepted as -args. Defaults to an
    empty hashtable.

    .PARAMETER validateArgs
    Fails before anything runs when a merged parameter key is not a known parameter of the build
    type. The known names are read from parameters.json in the module directory. When that file is
    absent, or has no entry for the build type, nothing is rejected. The failure is an ordinary
    error that names the unknown keys and carries no code.

    .EXAMPLE
    Invoke-Build -type docker -buildArgs @{ imageTag = 'my-app'; registryUrl = 'ghcr.io/example' }

    Builds a Docker image from the configured workspace, passing --image-tag my-app and
    --registry-url ghcr.io/example to the build.

    .EXAMPLE
    Invoke-Build -type docker -buildArgs @{ tags = @('a', 'b') } -validateArgs

    Runs a Docker build with validation. The tags list becomes --tags a --tags b. With parameters.json
    present in the module directory, a key the docker build type does not know fails the call before
    docker is started. The buildArgs parameter can also be written as -args.

    .EXAMPLE
    try {
        Invoke-Build -type forge
    }
    catch {
        $code = $_.FullyQualifiedErrorId
        $status = $_.Exception.Data['ExitCode']
        exit $status
    }

    Turns a launcher failure into a process exit. Match on the error id, not the message.

    .INPUTS
    None. Invoke-Build does not accept pipeline input.

    .OUTPUTS
    None. The command returns nothing on success. The container's own output streams to the host
    rather than to the pipeline.

    .NOTES
    A failure is a terminating error. Its FullyQualifiedErrorId is a stable code and its
    Exception.Data['ExitCode'] is the status a script should exit with:

    WorkspaceInvalid (3): the workspace path no longer exists or is not a directory.
    DockerUnavailable (5): the Docker daemon cannot be reached.
    ImageUnavailable (5): the configured image is not present locally and cannot be pulled.
    BuildFailed (the container's own status): the build exited non-zero. The status is carried
    unchanged, so 2 for invalid configuration passes through.

    The validateArgs failure is the exception: it is an ordinary error without a code. Success
    throws nothing.

    Canonical contract (PowerShell module): PSModule.requirements.md, R-INVOKE-001 to R-INVOKE-006
    and R-VALIDATE-001 to R-VALIDATE-004. The PowerShell Module page of the project documentation
    describes the same behavior.
    #>
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
