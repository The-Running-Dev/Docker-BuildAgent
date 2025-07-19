[CmdletBinding()]
Param(
    [Parameter()][string]$type = 'docker',
    [Parameter(ValueFromRemainingArguments)][string[]]$buildArguments
)

$artifactsDir = "$PSScriptRoot\artifacts"
$buildProjectFile = "$PSScriptRoot\forge\Forge.csproj"
$tempDirectory = "$PSScriptRoot\\.nuke\temp"
$dotNetGlobalFile = "$PSScriptRoot\\global.json"
$dotNetInstallUrl = "https://dot.net/v1/dotnet-install.ps1"
$dotNetChannel = "STS"

$env:DOTNET_CLI_TELEMETRY_OPTOUT = 1
$env:DOTNET_NOLOGO = 1

function ExecSafe([scriptblock] $cmd) {
    & $cmd

    if ($LASTEXITCODE) {
        exit $LASTEXITCODE
    }
}

# If dotnet CLI is installed globally and it matches requested version, use for execution
if ($null -ne (Get-Command "dotnet" -ErrorAction SilentlyContinue) -and `
     $(dotnet --version) -and $LASTEXITCODE -eq 0) {
    $env:DOTNET_EXE = (Get-Command "dotnet").Path
}
else {
    # Download install script
    $DotNetInstallFile = "$TempDirectory\dotnet-install.ps1"
    New-Item -ItemType Directory -Path $TempDirectory -Force | Out-Null
    [Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
    (New-Object System.Net.WebClient).DownloadFile($DotNetInstallUrl, $DotNetInstallFile)

    # If global.json exists, load expected version
    if (Test-Path $DotNetGlobalFile) {
        $DotNetGlobal = $(Get-Content $DotNetGlobalFile | Out-String | ConvertFrom-Json)
        if ($DotNetGlobal.PSObject.Properties["sdk"] -and $DotNetGlobal.sdk.PSObject.Properties["version"]) {
            $DotNetVersion = $DotNetGlobal.sdk.version
        }
    }

    # Install by channel or version
    $DotNetDirectory = "$TempDirectory\dotnet-win"
    if (!(Test-Path variable:DotNetVersion)) {
        ExecSafe { & powershell $DotNetInstallFile -InstallDir $DotNetDirectory -Channel $DotNetChannel -NoPath }
    } else {
        ExecSafe { & powershell $DotNetInstallFile -InstallDir $DotNetDirectory -Version $DotNetVersion -NoPath }
    }
    $env:DOTNET_EXE = "$DotNetDirectory\dotnet.exe"
    $env:PATH = "$DotNetDirectory;$env:PATH"
}

Write-Output "PowerShell $($PSVersionTable.PSEdition) v$($PSVersionTable.PSVersion)"
Write-Output "Microsoft (R) .NET SDK version $(& $env:DOTNET_EXE --version)"

if (Test-Path (Join-Path $PSScriptRoot 'set-environment.ps1')) {
    & (Join-Path $PSScriptRoot 'set-environment.ps1')
}

ExecSafe {
    & $env:DOTNET_EXE build $buildProjectFile -o $artifactsDir -c Release /nodeReuse:false /p:UseSharedCompilation=false -nologo -clp:NoSummary --verbosity quiet
}

ExecSafe {
    & $env:DOTNET_EXE $(Join-Path $artifactsDir 'Forge.dll') --no-logo --type $type -- $($buildArguments -join ' ')
}