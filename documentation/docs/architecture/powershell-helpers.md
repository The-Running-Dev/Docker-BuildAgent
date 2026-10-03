---
id: powershell-helpers
title: PowerShell Helpers
sidebar_position: 5
---

Docker-BuildAgent provides a PowerShell helper module, `nuke-helpers.psm1`, that simplifies build automation tasks and provides consistent behavior across different environments.

## nuke-helpers.psm1

The core PowerShell module that powers Docker-BuildAgent automation scripts and provides standardized functions for common operations.

### Key Functions

| Function | Description |
| -------- | ----------- |
| `Copy-Directory` | Recursively copy directories with advanced pattern filtering and gitignore management |
| `Invoke-Script` | Execute PowerShell scripts conditionally with standardized messaging |
| `Invoke-DotNetBuild` | Execute .NET builds with environment-specific configurations |
| `Initialize-Build` | Set up build paths and validate project structure |
| `Get-PackageManager` | Auto-detect Node.js package manager based on lock files |
| `Invoke-SafeCommand` | Execute commands with comprehensive error handling |
| `Add-RootArgument` | Add the project root directory to a list of build arguments |
| `Invoke-DotNetCommand` | Run a `dotnet` command and fail on a non-zero exit code |
| `Initialize-DotNetEnvironment` | Verify the .NET SDK before a build |
| `Invoke-Forge` | Run the Forge build system for a build type, after running `set-environment.ps1` if the project has one |

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
- **Gitignore Management**: Updates `.gitignore` with copied files when `-UpdateGitIgnore` is passed

#### Gitignore Management

When `-UpdateGitIgnore` is passed (it is off by default, and `build.ps1` does not pass it), the function:

1. Creates `.gitignore` if it doesn't exist in the destination directory
2. Tracks all copied files
3. Adds entries to `.gitignore` (using forward slashes for cross-platform compatibility)
4. Avoids duplicate entries by checking existing patterns

This ensures that template files and generated code don't accidentally get committed to version control.

### Build Invocation

For user automation, use the unified build command through the root scripts or the container image:

```powershell
./build.ps1 -type docker --dry-run true
```

Canonical contract (build command): [design/20-contract.md](https://github.com/The-Running-Dev/Docker-BuildAgent/blob/main/design/20-contract.md)
