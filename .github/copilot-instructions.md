# GitHub Copilot Instructions for Docker-BuildAgent

## Project Overview

Docker-BuildAgent is a comprehensive CI/CD build system with a multi-build architecture built on .NET/Nuke. It supports containerized builds for Docker, Node.js, Angular, TypeScript, and .NET applications with cross-platform scripts and GitHub Actions integration.

## Core Architecture

### Multi-Build System (Forge Architecture)

The project uses a component-based build system with 5 specialized build types:

1. **Docker Build** (`forge/Docker/`) - Container image automation
2. **Node Build** (`forge/Node/`) - JavaScript/TypeScript application builds
3. **NodeInDocker Build** (`forge/NodeInDocker/`) - Combined Node + Container builds
4. **Forge Build** (`forge/Forge/`) - Changelog generation and release orchestration
5. **NodeTemplate Build** (integrated in `scripts/nuke/build.ps1`) - Template-based project generation

All build types are invoked via the unified `build` command: `build <type> [args]`

### Base Class Pattern

All builds inherit from `Base<TParams, TNotifications>` providing:
- Dependency injection with ServiceLocator pattern
- Consistent logging with Serilog 
- Parameter validation and configuration management
- Service injection for Git, GitHub, Docker, and Node.js operations
- Notification systems (Discord, etc.)

### Component Interfaces

Key interfaces for specialized functionality:
- `ICleanComponent` - Provides cleanup target for removing artifacts
- `IDockerComponent` - Docker operations and image management (build, push)
- `INodeComponent` - Node.js package manager detection and execution (build, copy artifacts)
- `IGitHubComponent` - GitHub release and Git tag management
- `INotifications` - Multi-channel notification support (Discord, etc.)

## Build Configuration System

### Configuration Files Structure (`.build/` directory)

```
.build/
├── .app.env.map         # Application environment variable mapping
├── .build.env.map       # Build environment variable mapping  
├── .build.copy          # Files/directories to copy to artifacts
└── .build.scripts       # Custom build commands to execute
```

### Environment Mapping Syntax

```bash
# .build.env.map example
RegistryToken=env:GITHUB_TOKEN
ImageTag=const:latest
NotificationsWebHookUrl=env:DISCORD_WEBHOOK
```

- `env:VARIABLE` - Read from environment variable
- `const:value` - Set constant value
- `env:VARIABLE,default:value` - Environment with fallback

### Build Scripts Convention

```bash
# .build.scripts example
npm ci
npm run build:prod
pwsh deploy.ps1
```

## Key Components & Services

### Common Services (`forge/Common/Services/`)

- **GitService** - Git operations, tagging, changelog generation
- **GitHubService** - GitHub API, releases, authentication  
- **DockerService** - Container builds, registry operations
- **NodeService** - Package manager detection (npm/pnpm/yarn), script execution

### Parameter Inheritance Hierarchy

```
ForgeParams (base parameters)
├── DockerParams (container-specific)
├── NodeParams (Node.js-specific) 
└── NodeInDockerParams (combined Docker + Node)
```

### Utilities (`forge/Common/Utilities/`)

- **Files.cs** - Environment file generation, configuration parsing
- **Common.cs** - Version detection, path utilities
- **Extensions/** - File system and process extensions

## Build Execution Patterns

### Container-Based Execution (Recommended)

All builds use the unified `build` command with type as first argument:

```bash
# Docker build with automatic image tagging and registry push
docker run -v ./:/workspace -it ghcr.io/the-running-dev/build-agent:latest build docker --registry-url ghcr.io --registry-user username

# Node.js build with artifact output
docker run -v ./:/workspace -it ghcr.io/the-running-dev/build-agent:latest build node --artifacts-dir ./dist

# Node + Docker combined build
docker run -v ./:/workspace -it ghcr.io/the-running-dev/build-agent:latest build node-in-docker

# Node template build for documentation sites
docker run -v ./:/workspace -it ghcr.io/the-running-dev/build-agent:latest build node-template -AppDir documentation

# Forge: Changelog and release management
docker run -v ./:/workspace -it ghcr.io/the-running-dev/build-agent:latest build forge --change-log-source all
```

### Using PowerShell Module

```powershell
# Import and configure the module
Import-Module .\scripts\powershell-module\Docker-BuildAgent.psm1
Set-BuildAgentConfig `
    -DockerImage "ghcr.io/the-running-dev/build-agent:latest" `
    -WorkspacePath $PWD `
    -ArtifactsDir "./artifacts"

# Execute builds via Invoke-Build
Invoke-Build -type "docker" -args @{ imageName = "myapp"; tag = "v1.0"; registryUrl = "ghcr.io" }
Invoke-Build -type "node" -args @{ packageManager = "pnpm"; artifactsDir = "./dist" }
Invoke-Build -type "node-in-docker" -args @{}
Invoke-Build -type "forge" -args @{ changeLogSource = "all" }
```

### Local PowerShell Scripts (via unified build.ps1)

```powershell
# Using the unified build script directly
.\scripts\nuke\build.ps1 docker -ImageName myapp -Tag v1.0
.\scripts\nuke\build.ps1 node -ArtifactsDir ./dist
.\scripts\nuke\build.ps1 node-in-docker -SkipDockerBuild $false
.\scripts\nuke\build.ps1 forge -ChangeLogSource all
.\scripts\nuke\build.ps1 node-template -AppDir documentation
```

### Direct NUKE Execution

```bash
# Build Docker images
dotnet run --project forge/Docker/Docker.csproj -- --registry-url ghcr.io --registry-user username

# Build Node.js applications
dotnet run --project forge/Node/Node.csproj -- --artifacts-dir artifacts

# Combined Node + Docker
dotnet run --project forge/NodeInDocker/NodeInDocker.csproj -- --skip-docker-build false

# Changelog and release
dotnet run --project forge/Forge/Forge.csproj -- --change-log-source all
```

## PowerShell Automation (`scripts/`)

### PowerShell Module (`scripts/powershell-module/`)

New programmatic interface providing:
- **Docker-BuildAgent.psm1** - Main module with configuration and `Invoke-Build`
- **Docker-BuildAgent.psd1** - Module manifest for proper PowerShell import
- **Update-ModuleParameters.ps1** - Script to sync module functions with C# parameters

Module commands:
- `Set-BuildAgentConfig` - Configure module for your environment (DockerImage, WorkspacePath, etc.)
- `Invoke-Build` - Execute build types with a parameter hashtable

### Core Helper Module (`scripts/nuke/nuke-helpers.psm1`)

Key functions for build automation:
- `Invoke-Forge` - Execute multi-build workflows
- `Copy-Directory` - Recursive copying with .gitignore management and exclude patterns
- `Initialize-Build` - Environment setup and validation
- `Invoke-DotNetBuild` - .NET compilation management
- `Get-PackageManager` - Auto-detect npm/pnpm/yarn from lock files
- `Invoke-SafeCommand` - Execute commands with comprehensive error handling

### Build Script Architecture

Consolidated into a single unified entry point:

- `scripts/nuke/build.ps1` - Unified build script handling all build types:
  - `build docker` - Container build automation
  - `build node` - Node.js application builds
  - `build node-in-docker` - Combined Node + Docker builds
  - `build forge` - Release and changelog generation
  - `build node-template` - Template-based project creation
- `scripts/nuke/nuke-helpers.psm1` - Shared helper functions

## Development Workflow

### Environment Setup

1. **Prerequisites**: .NET 8 SDK, Docker, Node.js, PowerShell 5.1+
2. **Environment Variables**: Create `.env` file with GitHub tokens, registry credentials
3. **Solution Build**: `dotnet build forge/Forge.sln`
4. **Test Execution**: `dotnet test forge/Common.Tests/`

### Build Target Dependencies

Standard build pipeline flow:
```
Setup → Clean → GenerateEnvironment → BuildApplication → CopyToArtifacts → Build
```

### Package Manager Detection Logic

Auto-detection based on lock files:
1. `pnpm-lock.yaml` → pnpm
2. `yarn.lock` → yarn  
3. Default → npm

## GitHub Actions Integration

### Workflow Structure (`.github/workflows/`)

- **CI Workflow** - Build validation, testing, multi-platform execution
- **Release Workflow** - Automated releases with semantic versioning
- **Documentation Workflow** - Docusaurus site deployment

### Environment Variables for CI/CD

```yaml
env:
  GITHUB_TOKEN: ${{ secrets.GITHUB_TOKEN }}
  REGISTRY_TOKEN: ${{ secrets.GITHUB_TOKEN }}
  NOTIFICATIONS_WEBHOOK_URL: ${{ secrets.DISCORD_WEBHOOK }}
  VERBOSITY: Normal
  DRY_RUN: false
```

## Documentation System (`documentation/`)

### Docusaurus Configuration

- **Theme System** - CSS variable-based theming with dynamic switching
- **Pre-build Process** - Automated markdown copying from project root
- **Navigation Generation** - Dynamic navbar from markdown files

### Build Process

```bash
npm run build:prod     # Production build with optimizations
npm run build:dev      # Development build with hot reload
```

## Testing Strategy

### Unit Testing (`forge/Common.Tests/`)

- **Component Testing** - Build class validation and pipeline testing
- **Service Testing** - Mock-based testing for external dependencies
- **Configuration Testing** - Environment mapping and parameter validation

### Integration Testing

- Container-based build validation
- Cross-platform script execution testing
- GitHub Actions workflow validation

## Security Considerations

### Token Management

- Use environment variables for all sensitive tokens
- Implement token masking in build outputs via `StripForDisplay` patterns
- Support for GitHub Personal Access Tokens with minimal required permissions

### Container Security

- Non-root user execution in containers
- Minimal base image with only required tools
- Volume mounting for workspace isolation

## Performance Optimizations

### Build Caching

- .NET build artifact caching
- Node.js dependency caching with package manager detection
- Docker layer caching for container builds

### Parallel Execution

- Multi-target builds with dependency management
- Concurrent service operations where safe
- Background process support for long-running tasks

## Common Patterns & Best Practices

### Error Handling

- `Invoke-SafeCommand` for PowerShell operations with automatic error handling
- Comprehensive logging with structured output (`[OK]`, `[ERROR]`, `[WARN]` prefixes)
- Service-based error propagation with detailed context

### Configuration Management

- Convention over configuration with sensible defaults
- Environment-specific overrides via mapping files
- Validation at build initialization with early failure

### Cross-Platform Compatibility

- PowerShell Core support for cross-platform scripting
- ASCII output prefixes for PowerShell 5.1 compatibility
- Path handling abstractions for Windows/Linux compatibility

## Quick Reference

### Essential Commands

```bash
# Container builds using unified build command (recommended)
docker run -v ./:/workspace -it ghcr.io/the-running-dev/build-agent:latest build docker --registry-url ghcr.io
docker run -v ./:/workspace -it ghcr.io/the-running-dev/build-agent:latest build node --artifacts-dir ./dist
docker run -v ./:/workspace -it ghcr.io/the-running-dev/build-agent:latest build node-in-docker
docker run -v ./:/workspace -it ghcr.io/the-running-dev/build-agent:latest build node-template -AppDir docs
docker run -v ./:/workspace -it ghcr.io/the-running-dev/build-agent:latest build forge --change-log-source all

# PowerShell module
Import-Module .\scripts\powershell-module\Docker-BuildAgent.psm1
Set-BuildAgentConfig -DockerImage ghcr.io/the-running-dev/build-agent:latest -WorkspacePath $PWD
Invoke-Build -type "docker" -args @{ imageName = "myapp" }
Invoke-Build -type "node" -args @{ packageManager = "pnpm" }

# Direct NUKE execution
dotnet run --project forge/Docker/Docker.csproj -- --registry-url ghcr.io
dotnet run --project forge/Node/Node.csproj -- --artifacts-dir artifacts
dotnet run --project forge/NodeInDocker/NodeInDocker.csproj
dotnet run --project forge/Forge/Forge.csproj -- --change-log-source all
```

### Environment Variables

```bash
# Required
GITHUB_TOKEN=ghp_xxxxxxxxxxxx
REGISTRY_TOKEN=ghp_xxxxxxxxxxxx

# Optional  
DISCORD_WEBHOOK_URL=https://discord.com/api/webhooks/...
VERBOSITY=Normal
DRY_RUN=false
FORCE_PUSH=false
```

### Build Configuration Files

```bash
# .build.scripts
npm ci
npm run build:prod
pwsh deploy.ps1

# .build.copy
dist/
package.json
README.md

# .build.env.map
ImageTag=env:BUILD_NUMBER,default:latest
CreateRelease=const:true
```
