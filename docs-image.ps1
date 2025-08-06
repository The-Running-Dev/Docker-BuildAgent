<#
.SYNOPSIS
    Dynamically builds and executes a Docker command to run the Docusaurus development server.
.DESCRIPTION
    This script inspects the official 'docs-template' Docker image to discover its default file structure.
    It then compares this structure with the local directory files.

    For every file that exists both locally and in the image's '/template' directory, a Docker volume
    mount (-v) is dynamically created. This ensures that local customizations are reflected inside the
    container, while any missing local files are sourced from the image itself.

    The script also maps port 3000 for local access and creates an anonymous volume for 'node_modules'
    to prevent local dependencies from overwriting the container's own modules.
.EXAMPLE
    ./docs-image.ps1
    # This command inspects the image, generates volume mounts for all matching local files,
    # and starts the Docusaurus development server, accessible at http://localhost:3000.
#>
[CmdletBinding()]
param()

# --- Configuration ---
$imageName = "ghcr.io/the-running-dev/docs-template:latest"
$imageTemplatePath = "/template"
$localPath = $PSScriptRoot

# --- Function Definition ---

function Get-ImageFileSystem {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)]
        [string]$ImageName,

        [Parameter(Mandatory = $true)]
        [string]$PathTemplate
    )

    Write-Host "[INFO] Inspecting image '$ImageName' for files in '$PathTemplate'..."
    try {
        # Run a temporary container to list all files recursively, overriding the default entrypoint.
        # Exclude the node_modules directory from the find command for efficiency.
        $fileList = docker run --rm --entrypoint "" $ImageName find $PathTemplate -path "$PathTemplate/node_modules" -prune -o -type f -print
        
        # Process the raw file list into clean, relative paths.
        $relativePaths = $fileList | ForEach-Object {
            $_.Substring($PathTemplate.Length).TrimStart('/')
        }

        Write-Host "[OK] Found $($relativePaths.Count) files in the image." -ForegroundColor Green
        
        return $relativePaths
    }
    catch {
        Write-Error "Failed to inspect image '$ImageName'. Please ensure Docker is running and the image is pulled."

        return $null
    }
}

# --- Main Script Logic ---

# 1. Get the list of all files from the '/template' directory in the Docker image.
$imageFiles = Get-ImageFileSystem -ImageName $imageName -PathTemplate $imageTemplatePath

if (-not $imageFiles) {
    Write-Error "Could not retrieve file list from image. Aborting."
    exit 1
}

# 2. Dynamically generate volume mount arguments.
$volumeMounts = @()
Write-Host "[INFO] Comparing local files with image files to generate volume mounts..."

foreach ($file in $imageFiles) {
    $localFilePath = Join-Path $localPath $file

    if (Test-Path $localFilePath) {
        # If a local file exists that matches one in the image, create a volume mount for it.
        $absoluteLocalPath = (Resolve-Path $localFilePath).Path
        $containerPath = "$imageTemplatePath/$file"
        
        # Add the -v flag and its value as separate arguments for robustness.
        $volumeMounts += "-v"
        $volumeMounts += "$absoluteLocalPath`:$containerPath"

        Write-Host "  [+] Mounting local file: $file"
    }
}

# 3. Construct the final Docker command.
$dockerArgs = @(
    "run",
    "--rm",
    "-p", "3000:3000", # Separate the flag from the value
    "-it"
)
$dockerArgs += $volumeMounts
$dockerArgs += "-v", "./docs:/template/docs" # Mount the local docs directory
$dockerArgs += "-v", "./src/pages:/template/src/pages" # Mount the local pages directory
$dockerArgs += "-v", "./src/navbarLinks.ts:/template/src/navbarLinks.ts"
$dockerArgs += "-v", "/template/node_modules" # Anonymous volume to protect node_modules
$dockerArgs += $imageName

# 4. Execute the command.
Write-Host "[INFO] Starting container with dynamically generated mounts..." -ForegroundColor Cyan
& docker $dockerArgs