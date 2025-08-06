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
5. **NodeTemplate Build** (`scripts/nuke/node-template-build.ps1`) - Template-based project generation

### Base Class Pattern

All builds inherit from `Base<TParams, TNotifications>` providing:
- Dependency injection with ServiceLocator pattern
- Consistent logging with Serilog 
- Parameter validation and configuration management
- Service injection for Git, GitHub, Docker, and Node.js operations
- Notification systems (Discord, etc.)

### Component Interfaces

Key interfaces for specialized functionality:
- `IDockerComponent` - Docker operations and image management
- `INodeComponent` - Node.js package manager detection and execution
- `INotifications` - Multi-channel notification support

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

```bash
docker run -v ./:/workspace -it ghcr.io/the-running-dev/build-agent:latest docker-build
docker run -v ./:/workspace -it ghcr.io/the-running-dev/build-agent:latest node-build
```

### Local PowerShell Scripts

```powershell
.\build.ps1 -type docker -isProd
.\build.ps1 -type node --artifacts-dir ./dist
.\build.ps1 -type forge --change-log-source all
```

### Direct NUKE Execution

```bash
dotnet run --project forge/Docker/Docker.csproj -- --root /workspace
dotnet run --project forge/Node/Node.csproj -- --artifacts-dir artifacts
```

## PowerShell Automation (`scripts/nuke/`)

### Core Helper Module (`nuke-helpers.psm1`)

Key functions for build automation:
- `Invoke-Forge` - Execute multi-build workflows
- `Copy-Directory` - Recursive copying with ignore patterns
- `Initialize-Build` - Environment setup and validation
- `Invoke-DotNetBuild` - .NET compilation management
- `Get-PackageManager` - Auto-detect npm/pnpm/yarn from lock files

### Build Script Architecture

- `docker-build.ps1` - Container build automation
- `node-build.ps1` - Node.js application builds
- `forge-build.ps1` - Release and changelog generation
- `node-template-build.ps1` - Template-based project creation

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
# Container builds
docker-build --image-tag myapp:v1.0.0 --dry-run
node-build --artifacts-dir ./dist
node-in-docker-build --skip-docker-build

# Local builds  
.\build.ps1 -type docker -isProd
.\build.ps1 -type node --verbosity Verbose

# Direct NUKE execution
dotnet forge/artifacts/Docker.dll --root /workspace --dry-run
dotnet forge/artifacts/Node.dll --artifacts-dir artifacts --verbosity Quiet
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
