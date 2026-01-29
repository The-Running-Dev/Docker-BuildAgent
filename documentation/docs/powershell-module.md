---
id: powershell-module
title: "🔌 PowerShell Module"
sidebar_position: 10
---

The new PowerShell module provides a programmatic interface to the Build Agent functionality, making it easier to integrate into custom scripts and automation workflows.

The new PowerShell module provides a programmatic interface to the Build Agent functionality, making it easier to integrate into custom scripts and automation workflows.

## Installation

```powershell
# Import directly from the repository (recommended method currently)
Import-Module ./scripts/powershell-module/Docker-BuildAgent.psm1
```

## Configuration

Before using the module, you need to configure it for your environment:

```powershell
Set-BuildAgentConfig `
    -DockerImage "ghcr.io/the-running-dev/build-agent:latest" `
    -DockerHost "tcp://host.docker.internal:2375" `
    -WorkspacePath "D:\Projects\YourProject" `
    -ArtifactsDir "./artifacts" `
    -Environment "development"
```

### Configuration Parameters

| Parameter | Description | Default |
|-----------|-------------|---------|
| `DockerImage` | The Build Agent image to use | ghcr.io/the-running-dev/build-agent:latest |
| `DockerHost` | Docker daemon endpoint | tcp://host.docker.internal:2375 |
| `WorkspacePath` | Local workspace path to mount in container | Current script location |
| `ArtifactsDir` | Output directory for build artifacts | artifacts |
| `Environment` | Build environment type | development |
| `AdditionalParameters` | Optional hashtable of extra parameters | {} |

## Dynamically Generated Functions

The module automatically generates strongly-typed PowerShell functions for each build type defined in the Forge build system. For example:

```powershell
# Docker build with parameters
Invoke-ForgeDocker `
    -ImageName "my-app" `
    -Tag "v1.0" `
    -CreateRegistry $true `
    -DryRun $true

# Node.js build with parameters
Invoke-ForgeNode `
    -PackageManager "pnpm" `
    -IsProduction $true `
    -ArtifactsDir "./dist"
```

## Updating Parameter Definitions

The module includes a parameter extraction script that scans C# parameter classes in the Forge build system and generates corresponding PowerShell function definitions:

```powershell
# Run from the module directory
./Update-ModuleParameters.ps1
```

This script:

1. Scans parameter definition files in `forge/Common/Parameters/`
2. Extracts parameter metadata including XML documentation
3. Handles inheritance to combine parameters from base classes
4. Generates a JSON file with complete parameter definitions
5. Creates strongly-typed PowerShell functions at runtime

## Migration from Shell Commands

If you're currently using the shell commands directly, here's how to migrate to the PowerShell module:

### Shell Command Style

```powershell
docker run --rm -it `
    -v ${PWD}:/workspace `
    -w /workspace `
    ghcr.io/the-running-dev/build-agent:latest `
    build docker --create-registry true
```

### Module Style

```powershell
# One-time configuration
Set-BuildAgentConfig `
    -DockerImage "ghcr.io/the-running-dev/build-agent:latest" `
    -DockerHost "tcp://host.docker.internal:2375" `
    -WorkspacePath $PWD

# Run the build (can be called multiple times with different parameters)
Invoke-ForgeDocker -CreateRegistry $true
```

## Benefits

- **IDE Integration**: Get parameter intellisense and tab completion in VS Code and other editors
- **Type Safety**: Parameter validation prevents common errors
- **Discoverability**: See available parameters and their descriptions
- **Reusability**: Configure once, use consistently across scripts
- **Automation**: Easier integration with custom CI/CD scripts

## Limitations

- Currently requires manual execution of `Update-ModuleParameters.ps1` when new parameters are added
- PowerShell 5.1 or later required (included with Windows 10/11 or PowerShell Core)
