
<#
.SYNOPSIS
    Helper module for copying directory trees with optional overwrite.
.DESCRIPTION
    Provides the Copy-Directory function, which recursively copies all files and subdirectories from a source directory to a destination directory, preserving structure. You can choose to overwrite existing files or skip them.
.EXAMPLE
    Copy-Directory -sourceDir './template' -destinationDir './docs-ui' -overwrite
    # Copies all files from ./template to ./docs-ui, overwriting existing files.
.EXAMPLE
    Copy-Directory -sourceDir './template' -destinationDir './docs-ui'
    # Copies all files from ./template to ./docs-ui, skipping files that already exist.
#>

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

Export-ModuleMember `
    Copy-Directory