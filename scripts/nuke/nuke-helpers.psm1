<#
.SYNOPSIS
    PowerShell helper module for build operations, directory management, and environment setup.
.DESCRIPTION
    This module provides a comprehensive set of functions for common build operations including:
    - Directory copying with ignore pattern support (.copy.ignore files)
    - Build script initialization and environment setup
    - PowerShell script execution with custom messaging
    - .NET SDK environment management and installation
    - .NET command execution with automatic error handling
    - Safe command execution with comprehensive error handling
    - Package manager detection for Node.js projects
    - Argument management for build tools

    All functions include robust parameter validation, error handling, and detailed logging
    to support reliable build automation workflows.

.FUNCTIONS
    Copy-Directory              - Recursively copy directories with optional overwrite and ignore patterns
    Initialize-BuildScript      - Initialize build scripts with startup information and error handling
    Invoke-Script              - Execute PowerShell scripts conditionally with custom messaging
    Add-RootArgument           - Add root directory arguments to command line argument arrays
    Invoke-DotNetCommand       - Execute .NET commands with automatic error checking
    Invoke-SafeCommand         - Execute commands with comprehensive error handling and exit code validation
    Initialize-DotNetEnvironment - Initialize .NET SDK environment with automatic installation and configuration
    Initialize-BuildPaths      - Initialize and validate build paths with directory creation
    Invoke-BuildProject        - Execute .NET builds with development/production mode support
    Invoke-ForgeApplication    - Execute Forge application with type and argument management
    Invoke-StandardBuild       - Execute standard build workflows with initialization and Forge execution
    Get-PackageManager         - Detect Node.js package manager (npm, yarn, pnpm) from lock files

.EXAMPLE
    # Copy a template directory with ignore patterns
    Copy-Directory -sourceDir './template' -destinationDir './docs-ui' -overwrite
    
    # Initialize a build script with proper error handling
    $workingDir = Initialize-BuildScript -scriptName "Docker Build" -arguments $args
    
    # Initialize and validate build paths
    $paths = Initialize-BuildPaths -ProjectRoot $PSScriptRoot
    
    # Initialize .NET environment with automatic SDK management
    Initialize-DotNetEnvironment -TempDirectory $paths.TempDir
    
    # Execute a development or production build
    Invoke-BuildProject -ProjectFile $paths.ProjectFile -OutputDirectory $paths.ArtifactsDir -IsDevelopment
    
    # Execute the Forge application
    Invoke-ForgeApplication -ArtifactsDir $paths.ArtifactsDir -Type "docker" -BuildArguments $args
    
    # Load environment variables conditionally
    Invoke-Script -projectDir $workingDir -scriptName "set-environment.ps1" -message "Loading environment..."
    
    # Execute a .NET build command with error checking
    Invoke-DotNetCommand -dllPath "./artifacts/Forge.dll" -arguments @("Build", "--verbosity", "Normal")
    
    # Execute any command safely with automatic error handling
    Invoke-SafeCommand { & git clone $repoUrl }
    
    # Execute standard build workflows with automatic initialization
    Invoke-StandardBuild -ScriptName "Docker Build" -BuildTypes @("docker") -Arguments $args
    Invoke-StandardBuild -ScriptName "Node-in-Docker Build" -BuildTypes @("node", "docker") -Arguments $args
    
    # Detect package manager for Node.js projects
    $packageManager = Get-PackageManager -projectDir "./frontend"

.NOTES
    Version: 2.1
    Author: Docker-BuildAgent Project
    Dependencies: PowerShell 5.1 or later
    
    All functions follow PowerShell best practices with:
    - Comprehensive parameter validation
    - Approved PowerShell verbs
    - Detailed help documentation
    - Consistent error handling
    - Verbose logging capabilities
    - Automatic .NET SDK management
    - Safe command execution patterns
.LINK
    https://github.com/The-Running-Dev/Docker-BuildAgent
#>

function Initialize-BuildScript {
<#
.SYNOPSIS
    Initialize common build script settings and display startup information.
.PARAMETER ScriptName
    The name of the script being executed (for display purposes).
.PARAMETER WorkingDir
    The working directory (defaults to current directory).
.PARAMETER Arguments
    The arguments passed to the script.
.EXAMPLE
    Initialize-BuildScript -Name "Docker Build" -Arguments $args
#>
    param(
        [Parameter(Mandatory = $true)]
        [ValidateNotNullOrEmpty()]
        [string]$Name,
        
        [Parameter(Mandatory = $false)]
        [ValidateScript({Test-Path $_ -PathType Container})]
        [string]$WorkingDir = (Convert-Path .),
        
        [Parameter(Mandatory = $false)]
        [AllowEmptyCollection()]
        [array]$Arguments = @()
    )
    
    $script:ErrorActionPreference = "Stop"

    Write-Host "🚀 Running $Name`: $WorkingDir"
    Write-Host "🧾 Arguments: $Arguments"

    # Load environment variables using helper function
    Invoke-Script `
        -WorkingDir $WorkingDir `
        -ScriptFile "set-environment.ps1" `
        -Message "Loading Environment Variables..."
}

function Invoke-Script {
<#
.SYNOPSIS
    Executes a PowerShell script file if it exists in the specified directory.

.DESCRIPTION
    This function looks for a PowerShell script in the specified project directory and executes it if found.
    It's commonly used to load environment variables, configuration settings, or run initialization scripts
    as part of the build process. The function will silently skip execution if the script file doesn't exist.

.PARAMETER WorkingDir
    The directory to look for the script in. Should be a valid path to a directory.

.PARAMETER ScriptName
    The name of the PowerShell script file to execute (including .ps1 extension).
    Defaults to "set-environment.ps1" if not specified.

.PARAMETER Message
    An optional message to display when the script is found and executed.
    If empty or not provided, no message will be displayed.

.EXAMPLE
    Invoke-Script -WorkingDir $workingDir
    # Looks for and executes "set-environment.ps1" in the working directory (default behavior)

.EXAMPLE
    Invoke-Script -WorkingDir $workingDir -ScriptName "config.ps1" -Message "Loading Configuration..."
    # Executes "config.ps1" and displays "🔧 Loading Configuration..." when found

.EXAMPLE
    Invoke-Script -WorkingDir "." -ScriptName "dev-settings.ps1" -Message "Loading Development Settings..."
    # Executes "dev-settings.ps1" in the current directory with a custom message

.EXAMPLE
    Invoke-Script -WorkingDir "C:\Project" -ScriptName "azure-config.ps1"
    # Executes "azure-config.ps1" in the specified path without displaying a message

.NOTES
    - The function uses dot-sourcing (.) to execute the script in the current scope
    - This allows the executed script to modify variables and environment in the calling scope
    - No error is thrown if the script file doesn't exist - it simply skips execution
    - The message parameter is prefixed with "🔧 " emoji when displayed

.OUTPUTS
    None. The function executes the script but doesn't return any value.
#>
    param(
        [Parameter(Mandatory = $true)]
        [ValidateNotNullOrEmpty()]
        [string]$WorkingDir,

        [Parameter(Mandatory = $false)]
        [ValidateNotNullOrEmpty()]
        [string]$ScriptFile = "set-environment.ps1",
        
        [Parameter(Mandatory = $false)]
        [string]$Message = ""
    )

    $script = Join-Path $WorkingDir $ScriptFile

    if (Test-Path $script) {
        if ($Message) {
            Write-Host "🔧 $Message"
        }

        . $script
    }
}

function Add-RootArgument {
<#
.SYNOPSIS
    Add --root argument to arguments array if not already present.
.PARAMETER arguments
    The arguments array to modify.
.PARAMETER rootPath
    The root path to add.
.EXAMPLE
    $args = Add-RootArgument -arguments $args -rootPath $workingDir
#>
    param(
        [Parameter(Mandatory = $true)]
        [AllowEmptyCollection()]
        [array]$Arguments,
        
        [Parameter(Mandatory = $true)]
        [ValidateNotNullOrEmpty()]
        [string]$RootPath
    )

    if ($Arguments -notcontains '--root') {
        $Arguments += '--root'
        $Arguments += $RootPath
    }

    return $Arguments
}

function Invoke-DotNetCommand {
<#
.SYNOPSIS
    Execute a dotnet command with error handling.
.PARAMETER dllPath
    Path to the .NET DLL to execute.
.PARAMETER arguments
    Arguments to pass to the command.
.EXAMPLE
    Invoke-DotNetCommand -dllPath "/nuke/forge/Forge.dll" -arguments $args
#>
    param(
        [Parameter(Mandatory = $true)]
        [ValidateNotNullOrEmpty()]
        [ValidateScript({Test-Path $_ -PathType Leaf})]
        [string]$DllPath,
        
        [Parameter(Mandatory = $false)]
        [AllowEmptyCollection()]
        [array]$Arguments = @()
    )
    
    Invoke-SafeCommand {
        & dotnet $DllPath @Arguments
    }
}

function Initialize-BuildPaths {
<#
.SYNOPSIS
    Initialize and validate build paths for a project.
.DESCRIPTION
    Sets up standard build paths and validates that critical project files exist.
    Creates necessary directories and returns a hashtable with all configured paths.
.PARAMETER ProjectRoot
    The root directory of the project. Defaults to current directory.
.PARAMETER ProjectFile
    Relative path to the main project file from ProjectRoot. Defaults to "forge\Forge.csproj".
.PARAMETER ArtifactsDir
    Relative path to artifacts directory from ProjectRoot. Defaults to "artifacts".
.PARAMETER TempDir
    Relative path to temporary directory from ProjectRoot. Defaults to ".nuke\temp".
.EXAMPLE
    $paths = Initialize-BuildPaths
.EXAMPLE
    $paths = Initialize-BuildPaths -ProjectRoot "C:\MyProject" -ProjectFile "src\MyApp.csproj"
.OUTPUTS
    Hashtable containing: ProjectRoot, ProjectFile, ArtifactsDir, TempDir
#>
    param(
        [Parameter(Mandatory = $false)]
        [string]$ProjectRoot = (Get-Location).Path,
        
        [Parameter(Mandatory = $false)]
        [string]$ProjectFile = "forge\Forge.csproj",
        
        [Parameter(Mandatory = $false)]
        [string]$ArtifactsDir = "artifacts",
        
        [Parameter(Mandatory = $false)]
        [string]$TempDir = ".nuke\temp"
    )
    
    # Resolve all paths to absolute
    $paths = @{
        ProjectRoot = $ProjectRoot
        ProjectFile = Join-Path $ProjectRoot $ProjectFile
        ArtifactsDir = Join-Path $ProjectRoot $ArtifactsDir
        TempDir = Join-Path $ProjectRoot $TempDir
    }
    
    # Validate critical paths exist
    if (-not (Test-Path $paths.ProjectFile)) {
        Write-Host "❌ Build Project Not Found: $($paths.ProjectFile)" -ForegroundColor Red
        
        exit 1
    }
    
    # Ensure artifacts directory exists
    if (-not (Test-Path $paths.ArtifactsDir)) {
        New-Item -ItemType Directory -Path $paths.ArtifactsDir -Force | Out-Null
        
        Write-Host "📁 Created Artifacts Directory: $($paths.ArtifactsDir)" -ForegroundColor Green
    }
    
    return $paths
}

function Invoke-BuildProject {
<#
.SYNOPSIS
    Execute a .NET build with development or production configuration.
.DESCRIPTION
    Performs either a fast development build (no restore) or a full production build
    with optimizations based on the specified mode.
.PARAMETER ProjectFile
    Path to the .NET project file to build.
.PARAMETER OutputDirectory
    Directory where build artifacts will be placed.
.PARAMETER IsDevelopment
    If true, performs a fast development build. If false, performs a production build.
.EXAMPLE
    Invoke-BuildProject -ProjectFile "src\MyApp.csproj" -OutputDirectory "artifacts"
.EXAMPLE
    Invoke-BuildProject -ProjectFile "src\MyApp.csproj" -OutputDirectory "artifacts" -IsDevelopment
#>
    param(
        [Parameter(Mandatory = $true)]
        [ValidateScript({Test-Path $_ -PathType Leaf})]
        [string]$ProjectFile,
        
        [Parameter(Mandatory = $true)]
        [string]$OutputDirectory,
        
        [Parameter(Mandatory = $false)]
        [switch]$IsDevelopment
    )
    
    $buildMode = if ($IsDevelopment) { "Development" } else { "Production" }
    
    if ($IsDevelopment) {
        Write-Host "⚡ Starting Development Build (Mode: $buildMode)..." -ForegroundColor Yellow
        Write-Host "   $ProjectFile -o $OutputDirectory" -ForegroundColor Yellow

        Invoke-SafeCommand {
            & $env:DOTNET_EXE build $ProjectFile -o $OutputDirectory -c Debug --no-restore /nodeReuse:false /p:UseSharedCompilation=false -nologo -clp:NoSummary --verbosity minimal | Out-Null
        }
        
        Write-Host "✅ Development Build Completed" -ForegroundColor Green
    }
    else {
        Write-Host "🚀 Starting Production Build (Mode: $buildMode)..." -ForegroundColor Yellow
        Write-Host "   $ProjectFile -o $OutputDirectory" -ForegroundColor Yellow

        Invoke-SafeCommand {
            & $env:DOTNET_EXE build $ProjectFile -o $OutputDirectory -c Release /nodeReuse:false /p:UseSharedCompilation=false -nologo -clp:NoSummary --verbosity quiet | Out-Null
        }
        
        Write-Host "✅ Production Build Completed" -ForegroundColor Green
    }
}

function Invoke-ForgeApplication {
<#
.SYNOPSIS
    Execute the Forge application with specified type and arguments.
.DESCRIPTION
    Runs the Forge.dll application with the provided build type and passes through
    any additional arguments to the application.
.PARAMETER ArtifactsDir
    Directory containing the built Forge.dll file.
.PARAMETER Type
    The build type to execute (e.g., 'docker', 'node', etc.).
.PARAMETER BuildArguments
    Additional arguments to pass to the Forge application.
.EXAMPLE
    Invoke-ForgeApplication -ArtifactsDir "artifacts" -Type "docker"
.EXAMPLE
    Invoke-ForgeApplication -ArtifactsDir "artifacts" -Type "node" -BuildArguments @("--verbose", "--force")
#>
    param(
        [Parameter(Mandatory = $true)]
        [ValidateScript({Test-Path $_ -PathType Container})]
        [string]$ArtifactsDir,
        
        [Parameter(Mandatory = $true)]
        [ValidateNotNullOrEmpty()]
        [string]$Type,
        
        [Parameter(Mandatory = $false)]
        [AllowEmptyCollection()]
        [array]$BuildArguments = @()
    )
    
    Write-Host "🔥 Executing Forge with Type: $Type" -ForegroundColor Magenta
    
    $forgeDll = Join-Path $ArtifactsDir 'Forge.dll'
    $forgeArgs = @("--no-logo", "--type", $Type)
    
    if ($BuildArguments -and $BuildArguments.Count -gt 0) {
        $forgeArgs += "--"
        $forgeArgs += $BuildArguments
    }
    
    Invoke-DotNetCommand -dllPath $forgeDll -arguments $forgeArgs
}

function Copy-Directory {
<#
.SYNOPSIS
    Recursively copy all files and subdirectories from a source directory to a destination directory.
.DESCRIPTION
    Copies all files and subdirectories from $sourceDir to $destinationDir, preserving the directory structure. If -Overwrite is specified, existing files will be overwritten; otherwise, existing files are skipped.
    Supports a .copy.ignore file in the source directory to exclude specific files from being copied.
.PARAMETER sourceDir
    The source directory to copy from.
.PARAMETER destinationDir
    The destination directory to copy to.
.PARAMETER Overwrite
    If specified, existing files in the destination will be overwritten. Otherwise, they are skipped.
.EXAMPLE
    Copy-Directory -sourceDir './template' -destinationDir './docs-ui' -Overwrite
.EXAMPLE
    Copy-Directory -sourceDir './template' -destinationDir './docs-ui'
.NOTES
    If a .copy.ignore file exists in the source directory, files matching the patterns in that file will be excluded from copying.
    Each line in .copy.ignore should contain a filename or pattern to ignore.
#>
    param(
        [Parameter(Mandatory = $true)]
        [ValidateNotNullOrEmpty()]
        [string]$SourceDir,
        
        [Parameter(Mandatory = $true)]
        [ValidateNotNullOrEmpty()]
        [string]$DestinationDir,

        [Parameter(Mandatory = $false)]
        [switch]$Overwrite
    )

    # Validate source directory exists
    if (-not (Test-Path $SourceDir -PathType Container)) {
        Write-Host "❌ Source Directory Not Found: $SourceDir" -ForegroundColor Red
        
        return
    }

    Write-Host ""
    $mode = if ($Overwrite) { "Overwrite" } else { "No Overwrite" }
    Write-Host "🔄 Copying: $SourceDir -> $DestinationDir ($mode)" -ForegroundColor Yellow

        # Load ignore patterns from .copy.ignore file if it exists
        $ignorePatterns = @()
        $ignoreFile = Join-Path $SourceDir ".copy.ignore"

        if (Test-Path $ignoreFile) {
            $ignorePatterns = Get-Content $ignoreFile | Where-Object { $_.Trim() -and -not $_.StartsWith('#') }
        
            if ($ignorePatterns.Count -gt 0) {
                Write-Host "📋 Loaded .copy.ignore with $($ignorePatterns.Count) Pattern(s)" -ForegroundColor Cyan
            }
        }

        try {
            Get-ChildItem -Path $sourceDir -Recurse | ForEach-Object {
                if (-not $_.PSIsContainer) {
                    $relativePath = $_.FullName.Substring($sourceDir.Length).TrimStart('\','/')
                    $fileName = $_.Name
                    
                    # Check if file should be ignored
                    $shouldIgnore = $false
                    foreach ($pattern in $ignorePatterns) {
                        if ($fileName -like $pattern -or $relativePath -like $pattern) {
                            $shouldIgnore = $true
                            break
                        }
                    }
                    
                    if ($shouldIgnore) {
                        Write-Host "  🚫 Ignored: $relativePath" -ForegroundColor DarkYellow
                        return
                    }
                    
                    $destPath = Join-Path $destinationDir $relativePath
                    $destDir = Split-Path $destPath -Parent

                    if (-not (Test-Path $destDir)) {
                        New-Item -ItemType Directory -Path $destDir -Force | Out-Null
                    }

                    if ($overwrite -or -not (Test-Path $destPath)) {
                        Copy-Item -Path $_.FullName -Destination $destPath -Force:$overwrite
                        
                        Write-Host "  ✓ Copied: $relativePath" -ForegroundColor Green
                    } else {
                        Write-Host "  ⏩ Skipped (Exists): $relativePath" -ForegroundColor Gray
                    }
                }
            }
        }
        catch {
            Write-Error ("  ✗ Failed to Copy {0}: {1}" -f $sourceDir, $_.Exception.Message)
        }

        Write-Host ""
}

function Get-PackageManager {
<#
.SYNOPSIS
    Detects the package manager used in the specified project directory.
.DESCRIPTION
    Checks for the presence of lock files specific to pnpm and yarn to determine the package manager. 
    If a "pnpm-lock.yaml" file is found, it returns "pnpm". If a "yarn.lock" file is found, it returns "yarn". 
    If neither file is present, it defaults to "npm".
.PARAMETER projectDir
    The root directory of the project to inspect. Defaults to current directory.
.EXAMPLE
    $pm = Get-PackageManager -projectDir "."
.EXAMPLE
    $pm = Get-PackageManager
#>
    param(
        [Parameter()]
        [ValidateNotNullOrEmpty()]
        [ValidateScript({Test-Path $_ -PathType Container})]
        [string]$ProjectDir = (Get-Location).Path
    )
    
    $pm = "npm"

    if (Test-Path (Join-Path $ProjectDir "pnpm-lock.yaml")) {
        $pm = "pnpm"
    }
    elseif (Test-Path (Join-Path $ProjectDir "yarn.lock")) {
        $pm = "yarn"
    }
    
    Write-Host "🔍 Detected Package Manager: $pm"
    
    return $pm
}

function Invoke-SafeCommand {
<#
.SYNOPSIS
    Execute a command with automatic error handling and exit code checking.
.DESCRIPTION
    Executes a scriptblock and automatically exits the script if the command fails.
    This ensures that build failures are caught immediately rather than continuing
    with potentially invalid state.
.PARAMETER Command
    The scriptblock to execute safely.
.EXAMPLE
    Invoke-SafeCommand { & dotnet build }
.EXAMPLE
    Invoke-SafeCommand { & git clone $repoUrl }
#>
    param(
        [Parameter(Mandatory = $true)]
        [scriptblock]$Command
    )
    
    try {
        & $Command

        if ($LASTEXITCODE -and $LASTEXITCODE -ne 0) {
            Write-Host "❌ Command Failed with Exit Code: $LASTEXITCODE" -ForegroundColor Red
            
            exit $LASTEXITCODE
        }
    }
    catch {
        Write-Host "❌ Command Failed with Exception: $($_.Exception.Message)" -ForegroundColor Red
        
        exit 1
    }
}

function Initialize-DotNetEnvironment {
<#
.SYNOPSIS
    Initialize .NET SDK environment, installing if necessary.
.DESCRIPTION
    Checks for existing .NET CLI installation and installs if needed.
    Supports version specification via global.json and fallback to channel-based installation.
    Configures .NET environment variables for optimal build performance.
.PARAMETER TempDirectory
    Directory for temporary files during .NET installation.
.PARAMETER GlobalJsonPath
    Path to global.json file for version specification. Defaults to "global.json" in current directory.
.PARAMETER InstallUrl
    URL for the .NET installation script. Defaults to official Microsoft installer.
.PARAMETER Channel
    .NET channel to install if no version is specified. Defaults to "STS" (Standard Term Support).
.EXAMPLE
    Initialize-DotNetEnvironment -TempDirectory "temp"
.EXAMPLE
    Initialize-DotNetEnvironment -TempDirectory "temp" -GlobalJsonPath "global.json"
#>
    param(
        [Parameter(Mandatory = $true)]
        [string]$TempDirectory,
        
        [Parameter(Mandatory = $false)]
        [string]$GlobalJsonPath = "global.json",
        
        [Parameter(Mandatory = $false)]
        [string]$InstallUrl = "https://dot.net/v1/dotnet-install.ps1",
        
        [Parameter(Mandatory = $false)]
        [string]$Channel = "STS"
    )
    
    # Configure .NET environment variables for optimal performance
    $env:DOTNET_CLI_TELEMETRY_OPTOUT = 1
    $env:DOTNET_NOLOGO = 1
    
    Write-Host "🔍 Checking .NET SDK Environment..." -ForegroundColor Cyan
    
    # Try to use existing global dotnet CLI
    if ($null -ne (Get-Command "dotnet" -ErrorAction SilentlyContinue)) {
        try {
            $version = & dotnet --version 2>$null

            if ($LASTEXITCODE -eq 0 -and $version) {
                $env:DOTNET_EXE = (Get-Command "dotnet").Path
            
                Write-Host "✅ Using Global .NET CLI: $version" -ForegroundColor Green
            
                return
            }
        }
        catch {
            Write-Host "⚠️ Global .NET CLI Check Failed, Will Install Locally" -ForegroundColor Yellow
        }
    }

    Write-Host "📥 Installing .NET SDK Locally..." -ForegroundColor Yellow

    # Ensure temp directory exists
    if (-not (Test-Path $TempDirectory)) {
        New-Item -ItemType Directory -Path $TempDirectory -Force | Out-Null
    }
    
    # Download install script
    $dotNetInstallFile = Join-Path $TempDirectory "dotnet-install.ps1"
    Write-Host "   Downloading Installer..." -ForegroundColor Gray

    try {
        [Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
        (New-Object System.Net.WebClient).DownloadFile($InstallUrl, $dotNetInstallFile)
    }
    catch {
        Write-Host "❌ Failed to Download .NET installer: $($_.Exception.Message)" -ForegroundColor Red
        exit 1
    }
    
    # Check for version specification in global.json
    $dotNetVersion = $null
    
    # Resolve GlobalJsonPath to absolute path if needed
    if (-not [System.IO.Path]::IsPathRooted($GlobalJsonPath)) {
        $GlobalJsonPath = Join-Path (Get-Location) $GlobalJsonPath
    }
    
    if (Test-Path $GlobalJsonPath) {
        Write-Host "   Checking global.json for Version..." -ForegroundColor Gray
        
        try {
            $globalJson = Get-Content $GlobalJsonPath | Out-String | ConvertFrom-Json
            
            if ($globalJson.PSObject.Properties["sdk"] -and $globalJson.sdk.PSObject.Properties["version"]) {
                $dotNetVersion = $globalJson.sdk.version
                Write-Host "   Found Specified Version: $dotNetVersion" -ForegroundColor Green
            }
        }
        catch {
            Write-Host "   Warning: Could not Parse global.json: $($_.Exception.Message)" -ForegroundColor Yellow
        }
    }
    
    # Install .NET SDK
    $dotNetDirectory = Join-Path $TempDirectory "dotnet-win"
    
    if ($dotNetVersion) {
        Write-Host "   Installing .NET SDK Version $dotNetVersion..." -ForegroundColor Gray
        Invoke-SafeCommand { & powershell $dotNetInstallFile -InstallDir $dotNetDirectory -Version $dotNetVersion -NoPath }
    } else {
        Write-Host "   Installing .NET SDK from Channel $Channel..." -ForegroundColor Gray
        Invoke-SafeCommand { & powershell $dotNetInstallFile -InstallDir $dotNetDirectory -Channel $Channel -NoPath }
    }
    
    # Configure environment
    $env:DOTNET_EXE = Join-Path $dotNetDirectory "dotnet.exe"
    $env:PATH = "$dotNetDirectory;$env:PATH"

    # Verify installation worked
    try {
        $installedVersion = & $env:DOTNET_EXE --version 2>$null
        if ($LASTEXITCODE -eq 0 -and $installedVersion) {
            Write-Host "✅ .NET SDK Installed Successfully: $installedVersion" -ForegroundColor Green
        } else {
            Write-Host "❌ .NET SDK Installation Verification Failed" -ForegroundColor Red
            exit 1
        }
    }
    catch {
        Write-Host "❌ .NET SDK Installation Verification Failed: $($_.Exception.Message)" -ForegroundColor Red
        exit 1
    }
}

function Invoke-ForgeBuild {
<#
.SYNOPSIS
    Executes a standard build workflow with initialization and Forge application execution.

.DESCRIPTION
    This function encapsulates the common build pattern used across docker-build, node-build, 
    and node-in-docker-build scripts. It handles script initialization, argument processing,
    and Forge application execution for one or more build types.

.PARAMETER ScriptName
    The display name for the build script (e.g., "Docker Build", "Node Build").

.PARAMETER BuildTypes
    Array of build types to execute. Each type will result in a separate Forge application call.
    Common values: "docker", "node".

.PARAMETER Arguments
    The arguments passed to the script, typically $args from the calling script.

.PARAMETER WorkingDir
    The working directory for the build. Defaults to current location.

.PARAMETER ArtifactsDir
    The directory where build artifacts will be placed. Defaults to "/nuke/forge".

.EXAMPLE
    # Single build type (docker-build pattern)
    Invoke-ForgeBuild -ScriptName "Docker Build" -BuildTypes @("docker") -Arguments $args
    
.EXAMPLE
    # Multiple build types (node-in-docker-build pattern)
    Invoke-ForgeBuild -ScriptName "Node-in-Docker Build" -BuildTypes @("node", "docker") -Arguments $args

.EXAMPLE
    # With custom directories
    Invoke-ForgeBuild -ScriptName "Custom Build" -BuildTypes @("node") -Arguments $args -WorkingDir "./src" -ArtifactsDir "./dist"
#>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)]
        [string]$BuildName,
        
        [Parameter(Mandatory = $true)]
        [string[]]$BuildTypes,
        
        [Parameter(Mandatory = $false)]
        [string[]]$Arguments = @(),
        
        [Parameter(Mandatory = $false)]
        [string]$WorkingDir = $(Get-Location).Path,
        
        [Parameter(Mandatory = $false)]
        [string]$ArtifactsDir = "/nuke/forge"
    )

    Write-Host "🚀 Starting $BuildName..." -ForegroundColor Green

    # Initialize the build script with common settings and load environment
    Initialize-BuildScript `
        -Name $BuildName `
        -Arguments $Arguments `
        -WorkingDir $WorkingDir

    # Add root argument for proper path handling
    $processedArgs = Add-RootArgument -Arguments $Arguments -RootPath $WorkingDir

    # Execute the Forge application for each specified build type
    foreach ($buildType in $BuildTypes) {
        Write-Host "🔨 Executing $buildType Build..." -ForegroundColor Cyan
        
        Invoke-ForgeApplication `
            -ArtifactsDir $ArtifactsDir `
            -Type $buildType `
            -BuildArguments $processedArgs
    }
    
    Write-Host "✅ $BuildName Completed Successfully!" -ForegroundColor Green
}

Export-ModuleMember -Function `
    Copy-Directory, `
    Initialize-BuildScript, `
    Invoke-Script, `
    Add-RootArgument, `
    Invoke-DotNetCommand, `
    Get-PackageManager, `
    Invoke-SafeCommand, `
    Initialize-DotNetEnvironment, `
    Initialize-BuildPaths, `
    Invoke-BuildProject, `
    Invoke-ForgeApplication, `
    Invoke-ForgeBuild