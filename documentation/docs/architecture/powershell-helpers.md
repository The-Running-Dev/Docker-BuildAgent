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
|----------|-------------|
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

### Invoke-Build (Preferred)

For user automation, prefer the PowerShell module command `Invoke-Build`:

```powershell
Invoke-Build -type "docker" -args @{ createRegistry = $true; dryRun = $true }
```


## Docker-BuildAgent PowerShell Module (New)

A new PowerShell module that provides a programmable interface to the Build Agent's functionality.

### Installation

```powershell
# Install from PowerShell Gallery (coming soon)
Install-Module -Name Docker-BuildAgent

# Or import directly from the repository
Import-Module ./scripts/powershell-module/Docker-BuildAgent.psm1
```

### Configuration

```powershell
# Configure the module for your environment
Set-BuildAgentConfig `
    -DockerImage "ghcr.io/the-running-dev/build-agent:latest" `
    -DockerHost "tcp://host.docker.internal:2375" `
    -WorkspacePath "D:\Projects\YourProject" `
    -ArtifactsDir "./artifacts" `
    -Environment "development"
```

### Build Invocation

The module exposes a single `Invoke-Build` command that accepts a build type and a hashtable of parameters. It provides:

- Optional parameter validation
- Consistent Docker container execution
- Automatic workspace mounting

### Parameter Extraction

The module includes a parameter extraction script (`Update-ModuleParameters.ps1`) that:

1. Scans Forge parameter definition files (C# classes)
2. Extracts parameter metadata including name, type, and documentation
3. Handles inheritance to combine parameters from base classes
4. Generates a JSON file with complete parameter definitions
5. Enables optional validation in `Invoke-Build`

This ensures that the PowerShell module validates parameters against the current state of the Forge build system when requested.
