
<#
.SYNOPSIS
    Helper module for copying directory trees with optional overwrite and common build operations.
.DESCRIPTION
    Provides functions for directory copying, build script initialization, and common build operations.
.EXAMPLE
    Copy-Directory -sourceDir './template' -destinationDir './docs-ui' -overwrite
    # Copies all files from ./template to ./docs-ui, overwriting existing files.
.EXAMPLE
    Initialize-BuildScript -scriptName "Docker Build"
    # Sets up common build script initialization
#>

function Initialize-BuildScript {
<#
.SYNOPSIS
    Initialize common build script settings and display startup information.
.PARAMETER scriptName
    The name of the script being executed (for display purposes).
.PARAMETER workingDir
    The working directory (defaults to current directory).
.PARAMETER arguments
    The arguments passed to the script.
.EXAMPLE
    Initialize-BuildScript -scriptName "Docker Build" -arguments $args
#>
    param(
        [string]$scriptName,
        [string]$workingDir = (Convert-Path .),
        [array]$arguments = @()
    )
    
    $script:ErrorActionPreference = "Stop"
    
    Write-Host "🚀 Running $scriptName`: $workingDir"
    Write-Host "🧾 Arguments: $arguments"
    
    return $workingDir
}

function Set-BuildEnvironment {
<#
.SYNOPSIS
    Load environment variables from set-environment.ps1 if it exists.
.PARAMETER projectDir
    The directory to look for set-environment.ps1 in.
.EXAMPLE
    Set-BuildEnvironment -projectDir $workingDir
#>
    param(
        [string]$projectDir
    )
    
    $envScript = Join-Path $projectDir "set-environment.ps1"

    if (Test-Path $envScript) {
        Write-Host "🔧 Setting Environment Variables..."
        . $envScript
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
        [array]$arguments,
        [string]$rootPath
    )
    
    if ($arguments -notcontains '--root') {
        $arguments += '--root'
        $arguments += $rootPath
    }
    
    return $arguments
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
        [string]$dllPath,
        [array]$arguments
    )
    
    & dotnet $dllPath @arguments

    if ($LASTEXITCODE -ne 0) {
        Write-Host "❌ Build Failed: $LASTEXITCODE"
        
        exit $LASTEXITCODE
    }
}

function Copy-Directory {
<#
.SYNOPSIS
    Recursively copy all files and subdirectories from a source directory to a destination directory.
.DESCRIPTION
    Copies all files and subdirectories from $sourceDir to $destinationDir, preserving the directory structure. If -Overwrite is specified, existing files will be overwritten; otherwise, existing files are skipped.
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
#>
    param(
        [string]$sourceDir,
        [string]$destinationDir,
        [switch]$overwrite
    )

    if (Test-Path $sourceDir -PathType Container) {
        $mode = if ($overwrite) { "Overwrite" } else { "No Overwrite" }
        Write-Host "Processing: $sourceDir -> $destinationDir ($mode)" -ForegroundColor Yellow

        try {
            Get-ChildItem -Path $sourceDir -Recurse | ForEach-Object {
                if (-not $_.PSIsContainer) {
                    $relativePath = $_.FullName.Substring($sourceDir.Length).TrimStart('\','/')
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
    else {
        Write-Host "$sourceDir not Found...Skipping." -ForegroundColor Yellow
        Write-Host ""
    }
}

<#
.SYNOPSIS
    Helper module for Node.js operations including package manager detection.
.DESCRIPTION
    Provides functions for common Node.js build operations like detecting package managers.
#>

function Get-PackageManager {
<#
.SYNOPSIS
    Detects the package manager used in the specified project directory.
.DESCRIPTION
    Checks for the presence of lock files specific to pnpm and yarn to determine the package manager. 
    If a "pnpm-lock.yaml" file is found, it returns "pnpm". If a "yarn.lock" file is found, it returns "yarn". 
    If neither file is present, it defaults to "npm".
.PARAMETER ProjectDirectory
    The root directory of the project to inspect. Defaults to current directory.
.EXAMPLE
    $pm = Get-PackageManager -projectDir "."
.EXAMPLE
    $pm = Get-PackageManager
#>
    param(
        [string]$projectDir = (Get-Location).Path
    )
    
    $pm = "npm"
    
    if (Test-Path (Join-Path $projectDir "pnpm-lock.yaml")) {
        $pm = "pnpm"
    }
    elseif (Test-Path (Join-Path $projectDir "yarn.lock")) {
        $pm = "yarn"
    }
    
    Write-Host "🔍 Detected Package Manager: $pm"
    
    return $pm
}

Export-ModuleMember -Function `
    Copy-Directory, `
    Initialize-BuildScript, `
    Set-BuildEnvironment, `
    Add-RootArgument, `
    Invoke-DotNetCommand, `
    Get-PackageManager