---
id: powershell-helpers
title: "🧰 PowerShell Helpers"
sidebar_position: 5
---

The Build Agent provides several PowerShell helper modules that simplify build automation tasks and provide consistent behavior across different environments.

## nuke-helpers.psm1

The core PowerShell module that powers Build Agent automation scripts and provides standardized functions for common operations.

### Key Functions

| Function | Description |
| -------- | ----------- |
| `Copy-Directory` | Recursively copy directories with advanced pattern filtering and gitignore management |
| `Invoke-Script` | Execute PowerShell scripts conditionally with standardized messaging |
| `Invoke-DotNetBuild` | Execute .NET builds with environment-specific configurations |
| `Initialize-Build` | Set up build paths and validate project structure |
| `Get-PackageManager` | Auto-detect Node.js package manager based on lock files |
| `Invoke-SafeCommand` | Execute commands with comprehensive error handling |

### Copy-Directory

A powerful directory copying function with several advanced features:

```powershell
Copy-Directory -SourceDir './template' -DestinationDir './docs-ui' -Overwrite
```

**Features:**

- **Selective File Copying**: Using `.copy.ignore` files to exclude specific patterns
- **Preservation Mode**: Can skip existing files to preserve customizations
- **Automatic Directory Creation**: Creates destination directory structure as needed
- **Detailed Logging**: Shows which files are copied, skipped, or ignored
- **Gitignore Management**: Automatically updates `.gitignore` with copied files

#### Gitignore Management

When copying files, the function now automatically:

1. Creates `.gitignore` if it doesn't exist in the destination directory
2. Tracks all copied files
3. Adds entries to `.gitignore` (using forward slashes for cross-platform compatibility)
4. Avoids duplicate entries by checking existing patterns

This ensures that template files and generated code don't accidentally get committed to version control.

### Build Invocation

For user automation, use the unified build command through the root scripts or the container image:

```powershell
./build.ps1 -type docker -create-registry true -dry-run true
```
