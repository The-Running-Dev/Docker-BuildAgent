# PSModule Specifications Archive

This file preserves PowerShell-module-related documentation snapshots from the pre-cleanup HEAD state.

## Source Snapshot: documentation/docs/powershell-module.md

---
id: powershell-module
title: "≡ƒöî PowerShell Module"
sidebar_position: 10
---

The PowerShell module provides a programmatic interface to the Build Agent functionality, making it easier to integrate into custom scripts and automation workflows.

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

## Build Invocation

The module uses a single command, `Invoke-Build`, which forwards a build type and an argument hashtable to the containerized build command. Parameter names are passed in camelCase and are converted to CLI kebab-case automatically.

```powershell
# Docker build with parameters
Invoke-Build `
    -type "docker" `
    -args @{
        imageName = "my-app"
        tag = "v1.0"
        createRegistry = $true
        dryRun = $true
    }

# Node.js build with parameters
Invoke-Build `
    -type "node" `
    -args @{
        packageManager = "pnpm"
        isProduction = $true
        artifactsDir = "./dist"
    }
```

## Updating Parameter Definitions

The module includes a parameter extraction script that scans C# parameter classes in the Forge build system and generates a `parameters.json` file used for optional validation:

```powershell
# Run from the module directory
./Update-ModuleParameters.ps1
```

This script:

1. Scans parameter definition files in `forge/Common/Parameters/`
2. Extracts parameter metadata including XML documentation
3. Handles inheritance to combine parameters from base classes
4. Generates a JSON file with complete parameter definitions
5. Enables optional validation in `Invoke-Build` via `-validateArgs`

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
Invoke-Build -type "docker" -args @{ createRegistry = $true }
```

## Benefits

- **Simplicity**: One command for all build types
- **Consistency**: Uniform argument handling across build targets
- **Validation**: Optional validation against `parameters.json`
- **Reusability**: Configure once, use consistently across scripts
- **Automation**: Easier integration with custom CI/CD scripts

## Limitations

- Requires manual execution of `Update-ModuleParameters.ps1` when new parameters are added
- Validation only runs when `-validateArgs` is specified
- PowerShell 5.1 or later required (included with Windows 10/11 or PowerShell Core)

---

## Source Snapshot: documentation/docs/architecture/powershell-helpers.md

---
id: powershell-helpers
title: "≡ƒº░ PowerShell Helpers"
sidebar_position: 5
---

The Build Agent provides several PowerShell helper modules that simplify build automation tasks and provide consistent behavior across different environments.

## nuke-helpers.psm1

The core PowerShell module that powers Build Agent automation scripts and provides standardized functions for common operations.

### Key Functions

| Function | Description |
|----------|-------------|
| `Copy-Directory` | Recursively copy directories with advanced pattern filtering and optional gitignore management |
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
- **Gitignore Management**: Optionally updates `.gitignore` when `-UpdateGitIgnore` is specified

#### Gitignore Management

When copying files with `-UpdateGitIgnore`, the function:

1. Creates `.gitignore` if it doesn't exist in the destination directory
2. Tracks all copied files
3. Adds entries to `.gitignore` (using forward slashes for cross-platform compatibility)
4. Avoids duplicate entries by checking existing patterns

This allows opt-in exclusion behavior for template-generated files while avoiding unexpected changes to repository tracking by default.

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

---

## Source Snapshot: documentation/docs/build-types.md

---
id: build-types
title: ≡ƒöº Build Types & Commands
sidebar_position: 2
---

The Build Agent provides a **unified `build` command** with different types. Each type is optimized for specific project types and use cases.

## Unified Build Command

All builds use the same command pattern:

```bash
build <type> [parameters]
```

Available types: `docker`, `node`, `node-in-docker`, `node-template`, `forge`

---

## ≡ƒÉ│ build docker

**Purpose**: Creates Docker images for your project artifacts with automatic tagging and registry push capabilities.

**What it does**:

- Builds Docker images from project artifacts (located in `ArtifactsDir`)
- Automatically detects or uses provided Dockerfile
- Supports [Docker templates](docker-templates) for common application types
- Tags images with version information from GitVersion
- Pushes to container registries when configured
- Creates git tags when building releases

**Usage**:

```bash
docker run \
  -v /var/run/docker.sock:/var/run/docker.sock \
  -v ./:/workspace \
  -it ghcr.io/the-running-dev/build-agent:latest \
  build docker
```

**Common Parameters**:

- `--dry-run true` - Simulate build without pushing
- `--create-github-release true` - Create GitHub release
- `--force-push true` - Force push even in dry-run scenarios

---

## ≡ƒôª build node

**Purpose**: Builds Node.js applications with automatic package manager detection and script execution.

**What it does**:

- Auto-detects package manager (npm, pnpm, yarn) based on lock files
- Reads build scripts from `.build.scripts` file or uses conventions
- Executes custom build workflows
- Copies specified artifacts to output directory
- Supports TypeScript, Angular, React, Next.js, Express, and more

**Usage**:

```bash
docker run \
  -v ./:/workspace \
  -it ghcr.io/the-running-dev/build-agent:latest \
  build node
```

**Build Scripts Convention**:

If no `.build.scripts` file exists, defaults to:

```text
{detected-package-manager} install
{detected-package-manager} run build:prod
```

---

## ≡ƒöä build node-in-docker

**Purpose**: Combines Node.js build with Docker image creation in a comprehensive two-phase build pipeline.

**What it does**:

- **Phase 1**: Node.js Application Build
  - Auto-detects package manager (npm, pnpm, yarn)
  - Executes build scripts from `.build.scripts` or conventions
  - Generates production-ready artifacts
  - Copies built files to artifacts directory

- **Phase 2**: Docker Image Creation
  - Builds Docker image using specified Dockerfile
  - Tags image with version information
  - Optionally pushes to container registry
  - Creates Git tags and GitHub releases

**Build Target Execution Order**:

1. `Setup` - Initialize parameters and environment
2. `Clean` - Remove previous artifacts
3. `GenerateEnvironment` - Set up build environment
4. `BuildApplication` - Execute Node.js build process
5. `CopyToArtifacts` - Move built files to artifacts directory
6. `BuildDockerImage` - Create Docker container image
7. `PushToRegistry` - Push image to container registry
8. `PublishToGitHub` - Create GitHub release (includes Git tag creation)
9. `Build` - Final completion target

**Usage**:

```bash
# Basic usage
docker run \
  -v /var/run/docker.sock:/var/run/docker.sock \
  -v ./:/workspace \
  -it ghcr.io/the-running-dev/build-agent:latest \
  build node-in-docker

# With custom parameters
docker run \
  -v /var/run/docker.sock:/var/run/docker.sock \
  -v ./:/workspace \
  -it ghcr.io/the-running-dev/build-agent:latest \
  build node-in-docker \
  --artifacts-dir ./dist \
  --image-tag my-app:latest \
  --registry-url ghcr.io/myorg \
  --create-github-release true
```

**Key Parameters**:

### Node.js Build Parameters

- `--artifacts-dir` - Directory for build artifacts (default: 'artifacts')

### Docker Build Parameters

- `--templates-dir` - Directory containing Dockerfile templates (default: '/nuke/templates')
- `--docker-file` - Dockerfile to use for building (default: 'Dockerfile')
- `--image-tag` - Tag for the Docker image (default: 'container-app')
- `--registry-url` - Container registry URL for pushing images
- `--registry-user` - Registry username for authentication
- `--registry-token` - Registry token/password for authentication

### Release & Git Parameters

- `--create-github-release` - Create GitHub release (default: false)
- `--release-tag` - Tag for the release (default: 'v0.0.0')
- `--force-push` - Force push operations
- `--dry-run` - Simulate build without pushing

### Common Parameters

- `--notifications` - Enable Discord notifications
- `--notifications-web-hook-url` - Discord webhook URL
- `--verbosity` - Logging verbosity level (Quiet, Minimal, Normal, Verbose)

**Configuration Examples**:

```bash
# Production build with registry push
build node-in-docker \
  --artifacts-dir ./build \
  --image-tag myapp:v1.2.3 \
  --registry-url ghcr.io/myorg \
  --registry-user $GITHUB_ACTOR \
  --registry-token $GITHUB_TOKEN \
  --create-github-release true \
  --release-tag v1.2.3

# Development build (dry run)
build node-in-docker \
  --image-tag myapp:dev \
  --dry-run true \
  --verbosity Verbose

# Custom Dockerfile and artifacts location
build node-in-docker \
  --docker-file Dockerfile.prod \
  --artifacts-dir ./dist/app \
  --templates-dir ./docker-templates \
  --image-tag myapp:custom
```

**Project Structure Requirements**:

```text
your-project/
Γö£ΓöÇΓöÇ package.json              # Node.js project configuration
Γö£ΓöÇΓöÇ .build.scripts            # Optional: Custom build commands
Γö£ΓöÇΓöÇ Dockerfile                # Docker image definition
Γö£ΓöÇΓöÇ set-environment.ps1       # Optional: Environment setup
ΓööΓöÇΓöÇ artifacts/                # Default output directory
    ΓööΓöÇΓöÇ (built files)
```

**Environment Variables**:

The build process respects these environment variables:

- `GITHUB_TOKEN` - For GitHub release creation
- `REGISTRY_USER` - Container registry username
- `REGISTRY_TOKEN` - Container registry authentication
- `DISCORD_WEBHOOK_URL` - For build notifications

**Use Cases**:

- **Frontend Applications**: Angular, React, Vue.js with Nginx serving
- **Node.js APIs**: Express, Fastify, NestJS applications
- **Full-Stack Apps**: Next.js, Nuxt.js applications
- **Static Sites**: Gatsby, Hugo with Node.js build pipeline
- **Microservices**: Node.js services requiring containerization

---

## ≡ƒôÜ build node-template

**Purpose**: Builds documentation sites using templates (primarily Docusaurus) with smart file merging.

**What it does**:

- Clones a documentation template repository
- Copies template files to your project, preserving existing files
- Auto-detects package manager (npm, pnpm, yarn)
- Installs dependencies and builds the documentation
- Supports production and development builds

**Usage**:

```bash
docker run \
  -v ./:/workspace \
  -it ghcr.io/the-running-dev/build-agent:latest \
  build node-template -AppDir documentation
```

**Key Parameters**:

- `-AppDir` - Target directory for documentation (default: 'documentation')
- `-PackageManager` - Force specific package manager (npm/pnpm/yarn)
- `-SkipInstall` - Skip npm install step
- `-IsProduction` - Build for production using build:prod script
- `-NodeTemplateRepositoryUrl` - Custom template repository URL

**Examples**:

```bash
# Basic usage with auto-detection
build node-template

# Custom directory with specific package manager
build node-template -AppDir docs-ui -PackageManager pnpm

# Skip install and build for development
build node-template -SkipInstall -IsProduction:$false

# Use custom template repository
build node-template -NodeTemplateRepositoryUrl https://github.com/my-org/custom-template.git
```

---

## ≡ƒô¥ build forge

**Purpose**: Provides build orchestration and changelog generation from Git history with advanced formatting options.

**What it does**:

- Generates formatted changelogs from Git commit history
- Supports multiple changelog sources (all commits, since last tag, or specific tag)
- Customizable date formatting (default: yyyy.MM.dd)
- Automatically prepends new changelog content to existing files
- Provides build orchestration for complex multi-stage processes

**Usage**:

```bash
# Generate changelog since last tag
docker run \
  -v ./:/workspace \
  -it ghcr.io/the-running-dev/build-agent:latest \
  build forge --target GenerateChangeLog

# Generate complete history
docker run \
  -v ./:/workspace \
  -it ghcr.io/the-running-dev/build-agent:latest \
  build forge --change-log-source all

# Generate changelog since specific tag
docker run \
  -v ./:/workspace \
  -it ghcr.io/the-running-dev/build-agent:latest \
  build forge --change-log-source v1.0.0
```

**Key Parameters**:

- `--change-log-source` - Source for changelog generation:
  - `null/empty` - Since last Git tag (default)
  - `all` - Complete commit history
  - `specific-tag` - Since specified tag (e.g., "v1.0.0")
- `--target` - Build target to execute (GenerateChangeLog, Build)

**Build Targets**:

1. `Setup` - Initialize parameters and environment
2. `GenerateChangeLog` - Create and save changelog to CHANGELOG.md
3. `Build` - Complete build process (depends on GenerateChangeLog)

**Output Format**:

The generated changelog uses this format with customizable date formatting:

```markdown
## Since v1.4.0 (2025.08.04)

### 2025.08.04

- Update build script to include 'forge' as a build type option
- Refactor NodeService to handle different shell commands
- Update documentation directory path

### 2025.08.03

- Work in Progress
```

**Configuration**:

The changelog formatter supports these options:

- **Date Format**: `yyyy.MM.dd` (default), customizable via ChangeLogFormatOptions
- **Include Hash**: Option to include commit hashes in output
- **Include Author**: Option to include commit author names
- **Grouping**: Commits grouped by date in descending order (latest first)

---

## ≡ƒº░ PowerShell Module

**Purpose**: Provides a programmable PowerShell interface for integrating Build Agent functionality into custom scripts and workflows.

**What it does**:

- Provides a single `Invoke-Build` command for all build types
- Provides consistent Docker container execution pattern
- Handles parameter validation and conversion
- Maintains consistent environment configuration

**Installation**:

```powershell
# Import module from repository
Import-Module ./scripts/powershell-module/Docker-BuildAgent.psm1

# Configure for your environment
Set-BuildAgentConfig `
    -DockerImage "ghcr.io/the-running-dev/build-agent:latest" `
    -DockerHost "tcp://host.docker.internal:2375" `
    -WorkspacePath "D:\Projects\YourProject" `
    -Environment "development"
```

**Usage Example**:

```powershell
# Use Invoke-Build for each build type
Invoke-Build -type "docker" -args @{ createRegistry = $true; dryRun = $true }
Invoke-Build -type "node" -args @{ packageManager = "pnpm"; isProduction = $true }
```

**Parameter Extraction**:

The module includes a parameter extraction script that generates a `parameters.json` definition file from C# parameter files:

```powershell
./Update-ModuleParameters.ps1
```

---

## ≡ƒöº Common Features

All build commands share these common capabilities:

### Environment Setup

- Automatically loads `set-environment.ps1` if present in project root
- Supports GitVersion for semantic versioning
- Reads configuration from project files

### Parameter Support

- `--root` - Specify project root directory (auto-added)
- `--dry-run` - Simulate operations without side effects
- `--force-push` - Override safety checks for pushing

### Logging & Output

- Colored console output with status prefixes
- Detailed progress information
- Error handling with meaningful messages

### Integration

- GitHub release creation
- Discord notifications
- Container registry push
- Git tag creation

---

## ≡ƒÄ» Choosing the Right Build Type

| Project Type | Command | Use Case |
|-------------|---------|----------|
| Pure Docker projects | `build docker` | Existing Dockerfile, containerizing artifacts |
| Node.js apps (no container) | `build node` | Build and test Node.js applications |
| Node.js apps (with container) | `build node-in-docker` | Complete CI/CD pipeline with registry push |
| Documentation sites | `build node-template` | Docusaurus, GitBook, static sites |
| Changelog generation | `build forge` | Git history-based changelog creation |
| Build orchestration | `build forge` | Complex multi-stage build processes |

---

## ≡ƒöù Related Documentation

- [Parameters Reference](parameters) - Detailed parameter documentation
- [Docker Templates](docker-templates) - Available Dockerfile templates
- [Customization](customization) - Custom build scripts and configuration
- [CI/CD Examples](ci-cd) - GitHub Actions integration examples

---

## Source Snapshot: documentation/docs/troubleshooting.md

---
id: troubleshooting
title: "Γ¥ô Troubleshooting & FAQ"
sidebar_position: 11
---

This section covers common issues, troubleshooting steps, and frequently asked questions for Docker-BuildAgent and the Forge build orchestrator.

## Troubleshooting

- **Docker login/authentication errors:**
  - Ensure your `RegistryToken` is valid and has the correct permissions (`write:packages`).
  - Use GitHub Actions secrets for sensitive values.
- **Image push fails:**
  - Check your network connection and GHCR access rights.
- **.NET build issues:**
  - Ensure you have the .NET 8 SDK installed locally if running .NET builds outside the container.
- **CI tool access issues:**
  - Ensure the CI environment has access to the required tools and permissions. The workflow sets up tools in the `/root/.dotnet/tools` directory and updates the PATH.
- **Forge build type errors:**
  - Make sure you specify the correct `-type` argument (e.g., `docker`, `node`, `forge`).
- **Copy-Directory .gitignore issues:**
  - `.gitignore` updates are opt-in and only occur when `-UpdateGitIgnore` is specified.
  - If the .gitignore feature isn't working properly, check that the destination directory is writable.
  - Verify that copied files have relative paths that can be properly converted to forward slashes.
  - If you do not want `.gitignore` updates, omit `-UpdateGitIgnore`.
- **PowerShell module parameter detection issues:**
  - Run `Update-ModuleParameters.ps1` if you've updated parameter definitions in C# code.
  - Ensure XML documentation comments exist on parameter properties for proper help text.
- **Changelog generation issues:**
  - Ensure your Git repository has commit history and proper tag structure.
  - Check that the repository has at least one tag if using the default (since last tag) option.
  - Verify write permissions to the project directory for CHANGELOG.md creation.
- **Date formatting problems:**
  - The changelog formatter uses `yyyy.MM.dd` format by default; custom formats require code changes.
  - Ensure commit dates are properly parsed from Git history.

## FAQ

- **Q: I get a permission denied error on build.sh or build.ps1**
  - A: Run `chmod +x build.sh` (Linux/macOS) or ensure PowerShell script permissions (Windows).
- **Q: .NET build fails outside the container**
  - A: Make sure you have the .NET 8 SDK installed locally.
- **Q: How do I pass secrets to Forge, Nuke, or Docker builds?**
  - A: Use environment variables or GitHub Actions secrets. Never hardcode secrets in scripts or Dockerfiles.
- **Q: GitVersion or Nuke not found in CI?**
  - A: The workflow installs .NET tools globally, creates a symlink for GitVersion, and adds `/root/.dotnet/tools` to the PATH for reliable access.
- **Q: How do I run a build for a specific project type?**
  - A: Use the `-type` argument with the build script, e.g., `./build.ps1 -type docker`, `./build.ps1 -type node`, or `./build.ps1 -type forge`.
- **Q: How do I use the PowerShell module instead of shell commands?**
  - A: Import the module with `Import-Module ./scripts/powershell-module/Docker-BuildAgent.psm1`, configure it with `Set-BuildAgentConfig`, and use `Invoke-Build` with a `-type` and `-args` hashtable.
- **Q: Why is my changelog empty or not generating correctly?**
  - A: Ensure your Git repository has commits and tags. Use `--change-log-source all` to generate complete history, or verify the last tag exists with `git tag -l`.
- **Q: Can I customize the changelog date format?**
  - A: The default format is `yyyy.MM.dd`. Customization requires modifying the `ChangeLogFormatOptions` class in the Forge source code.
- **Q: What happens when you run `ContainerCI` or the `container-ci` command?**
  - A: The `ContainerCI` target runs the full build, versioning, tagging, and publishing pipeline in order:
    1. Clean
    2. GetVersion
    3. ValidateInputs
    4. PrintInfo
    5. BuildContainer
    6. Tag
    7. Push
    8. Publish
    9. ContainerCI

Each target depends on the previous one, ensuring the full pipeline is executed correctly.

For more help, see the project README or open an issue on GitHub.

---

## Source Snapshot: documentation/docs/architecture/development-guide.md

---
id: development-guide
title: ≡ƒ¢á∩╕Å Development Guide
sidebar_position: 3
---

Complete guide for setting up, developing, and contributing to the Docker Build Agent project.

## Prerequisites

### Required Software

- **Docker**: [Docker Desktop](https://www.docker.com/get-started) installed and running (for local builds)
- **.NET 8 SDK**: [Download .NET 8](https://dotnet.microsoft.com/en-us/download/dotnet/8.0) (for local .NET builds)
- **PowerShell**: PowerShell 5.1+ (Windows) or PowerShell Core 7+ (Cross-platform)
- **Git**: Git client for version control
- **Node.js**: Node.js 18+ (for documentation and Node.js builds)

### Optional Development Tools

- **Visual Studio 2022** or **Visual Studio Code** with C# extension
- **JetBrains Rider** for .NET development
- **GitHub CLI** for enhanced GitHub integration

### Access Requirements

- **GitHub Account**: For repository access and container registry
- **GitHub Container Registry (GHCR)**: Access to ghcr.io
- **Personal Access Token**: GitHub token with packages:write permissions to push to GHCR

## Environment Setup

### 1. Clone the Repository

```bash
git clone https://github.com/The-Running-Dev/Docker-BuildAgent.git
cd Docker-BuildAgent
```

### 2. Build the Docker Image (Quick Start)

For immediate testing, you can build the container image:

```bash
# Build the build-agent container
docker build -t build-agent:latest .

# Test the container
docker run -it build-agent:latest
```

### 3. Set Environment Variables

Create a `.env` file in the project root (this file is gitignored):

```bash
# GitHub Configuration
GITHUB_TOKEN=your_github_personal_access_token
REGISTRY_TOKEN=your_github_personal_access_token
GITHUB_ACTOR=your_github_username

# Registry Configuration
REGISTRY_URL=ghcr.io
REGISTRY_USER=your_github_username

# Optional: Discord Notifications
NOTIFICATIONS_WEBHOOK_URL=your_discord_webhook_url

# Optional: Development Settings
VERBOSITY=Verbose
DRY_RUN=false
```

### 4. Build the Solution

```bash
# Build all projects
dotnet build forge/Forge.sln

# Or build specific projects
dotnet build forge/Docker/Docker.csproj
dotnet build forge/Node/Node.csproj
dotnet build forge/NodeInDocker/NodeInDocker.csproj
```

### 5. Run Tests

```bash
# Run all tests
dotnet test forge/Forge.sln

# Run specific test project
dotnet test forge/Common.Tests/Common.Tests.csproj
```

## Development Workflow

### Local Development

#### Using the Local Build Script

```powershell
# Basic Docker build
.\build.ps1 -type docker

# Node.js build with production flag
.\build.ps1 -type node -isProd

# Combined Node + Docker build with parameters
.\build.ps1 -type node-in-docker --dry-run true --verbosity Verbose
```

#### Direct Project Execution

```bash
# Run Docker build directly
dotnet run --project forge/Docker/Docker.csproj -- Build --dry-run true

# Run Node build with specific parameters
dotnet run --project forge/Node/Node.csproj -- Build --artifacts-dir ./dist

# Run NodeInDocker build
dotnet run --project forge/NodeInDocker/NodeInDocker.csproj -- Build --create-github-release false
```

### Container-Based Development

#### Building the Build Agent Container

```bash
# Build the container image
docker build -t build-agent:dev .

# Build with specific tag
docker build -t build-agent:latest .
```

#### Testing Container Locally

```bash
# Test Docker build
docker run \
  -v /var/run/docker.sock:/var/run/docker.sock \
  -v ./:/workspace \
  -e GITHUB_TOKEN=$GITHUB_TOKEN \
  -it build-agent:dev \
  build docker --dry-run true

# Test Node build
docker run \
  -v ./:/workspace \
  -it build-agent:dev \
  build node --artifacts-dir ./test-output
```

## ≡ƒôü Project Structure

The project follows a modular architecture with shared components:

```text
Docker-BuildAgent/
Γö£ΓöÇΓöÇ Common/                     # Shared utilities and interfaces
Γöé   Γö£ΓöÇΓöÇ Interfaces/            # Service interfaces (IGitService, IDockerService, etc.)
Γöé   Γö£ΓöÇΓöÇ Services/              # Service implementations
Γöé   ΓööΓöÇΓöÇ Models/                # Data models and DTOs
Γö£ΓöÇΓöÇ Docker/                    # Docker build type implementation
Γö£ΓöÇΓöÇ Node/                      # Node.js build type implementation
Γö£ΓöÇΓöÇ forge/                     # Forge multi-project builds
Γöé   Γö£ΓöÇΓöÇ NodeInDocker/         # Node.js in Docker container
Γöé   ΓööΓöÇΓöÇ NodeTemplate/         # Node.js template project
Γö£ΓöÇΓöÇ templates/                 # Project templates
Γö£ΓöÇΓöÇ scripts/                   # Build and deployment scripts
Γö£ΓöÇΓöÇ artifacts/                 # Build output directory
ΓööΓöÇΓöÇ documentation/             # Docusaurus documentation site
```

### Core Components

- **Common Project**: Contains shared services, interfaces, and utilities used across all build types
- **Build Types**: Independent implementations (Docker, Node) with specific functionality
- **Forge Projects**: Multi-project builds that combine functionality from multiple build types
- **Templates**: Reusable project templates for quick project initialization

### Solution Organization

```text
Docker-BuildAgent/
Γö£ΓöÇΓöÇ forge/                      # Main solution directory
Γöé   Γö£ΓöÇΓöÇ Forge.sln              # Main solution file
Γöé   Γö£ΓöÇΓöÇ Common/                # Shared utilities and services
Γöé   Γöé   Γö£ΓöÇΓöÇ Services/          # Service interfaces and implementations
Γöé   Γöé   Γö£ΓöÇΓöÇ Parameters/        # Base parameter classes
Γöé   Γöé   Γö£ΓöÇΓöÇ DependencyInjection/  # DI container setup
Γöé   Γöé   ΓööΓöÇΓöÇ Extensions/        # Extension methods
Γöé   Γö£ΓöÇΓöÇ Docker/                # Docker build project
Γöé   Γö£ΓöÇΓöÇ Node/                  # Node.js build project
Γöé   Γö£ΓöÇΓöÇ NodeInDocker/          # Combined Node+Docker build project
Γöé   Γö£ΓöÇΓöÇ NodeTemplate/          # Template-based documentation build
Γöé   ΓööΓöÇΓöÇ Common.Tests/          # Unit tests
Γö£ΓöÇΓöÇ scripts/                   # PowerShell build scripts
Γö£ΓöÇΓöÇ documentation/             # Docusaurus documentation site
Γö£ΓöÇΓöÇ templates/                 # Dockerfile templates
Γö£ΓöÇΓöÇ .github/                   # GitHub Actions workflows
Γö£ΓöÇΓöÇ build.ps1                  # Main build script
ΓööΓöÇΓöÇ Dockerfile                 # Build agent container definition
```

### Key Files

- **`build.ps1`**: Main entry point for local builds
- **`Dockerfile`**: Defines the build agent container
- **`forge/Forge.sln`**: Main .NET solution
- **`scripts/nuke/nuke-helpers.psm1`**: PowerShell helper functions
- **`.github/workflows/`**: CI/CD pipeline definitions

### Build Configuration Files

The project uses several configuration files to control build behavior:

```text
/.build
Γö£ΓöÇΓöÇ .app.env.map         # Maps application env vars
Γö£ΓöÇΓöÇ .build.scripts       # List of commands (e.g. npm, ps1, bash)
Γö£ΓöÇΓöÇ .build.copy          # Files/folders to copy to artifacts/
Γö£ΓöÇΓöÇ .build.env.map       # Maps build env vars like DiscordWebHookUrl
/artifacts/              # Final build output ends up here
/documentation/          # Docusaurus documentation
/forge/                  # Shared NUKE build logic
Γö£ΓöÇΓöÇ/Docker/              # Docker-specific targets
Γö£ΓöÇΓöÇ/Node/                # Node.js-specific targets
./Dockerfile             # Containerize your build
./build.ps1              # Build entry point
```

## ≡ƒöº Forge Multi-Project Builds

The Forge solution provides a unified build system that combines multiple build types into specialized implementations:

### Available Forge Projects

1. **NodeInDocker**: Combines Node.js and Docker build capabilities
2. **NodeTemplate**: Template-based project generation with Node.js support

### Building Forge Projects

```bash
# Build all Forge projects
dotnet build forge/Forge.sln

# Build specific project
dotnet build forge/NodeInDocker/NodeInDocker.csproj
```

## Adding New Features

### 1. Creating a New Build Type

Follow the multi-build architecture to add new build types:

```csharp
// 1. Create new project directory
forge/MyNewBuild/
Γö£ΓöÇΓöÇ MyNewBuild.cs
Γö£ΓöÇΓöÇ MyNewBuild.csproj
ΓööΓöÇΓöÇ Parameters/
    ΓööΓöÇΓöÇ MyNewBuildParams.cs

// 2. Implement the build class
public class MyNewBuild : Base<MyNewBuildParams, DiscordNotifications>
{
    public Target Setup => _ => _
        .Executes(() => {
            Logger.Information("Setting up MyNewBuild");
        });

    public Target Build => _ => _
        .DependsOn(Setup)
        .Executes(() => {
            Logger.Information("Executing MyNewBuild");
        });
}

// 3. Add to solution
dotnet sln forge/Forge.sln add forge/MyNewBuild/MyNewBuild.csproj

// 4. Update Dockerfile to include new executable
```

### 2. Adding New Services

Extend the dependency injection system:

```csharp
// 1. Define service interface
public interface IMyNewService
{
    Task<bool> DoSomethingAsync(string parameter);
}

// 2. Implement service
public class MyNewService : IMyNewService
{
    private readonly ILogger<MyNewService> _logger;
    
    public MyNewService(ILogger<MyNewService> logger)
    {
        _logger = logger;
    }
    
    public async Task<bool> DoSomethingAsync(string parameter)
    {
        _logger.LogInformation($"Doing something with {parameter}");
        // Implementation
        return true;
    }
}

// 3. Register in ServiceCollectionExtensions
services.AddTransient<IMyNewService, MyNewService>();

// 4. Use in build classes
public class MyBuild : Base<MyParams, MyNotifications>
{
    private readonly IMyNewService _myNewService;
    
    public MyBuild()
    {
        _myNewService = ServiceLocator.GetRequiredService<IMyNewService>();
    }
}
```

### 3. Adding New Parameters

Extend the parameter system:

```csharp
// 1. Create parameter class
public class MyNewBuildParams : ForgeParams
{
    [Parameter("Description of my parameter")]
    public string MyParameter { get; set; } = "default-value";
    
    [Parameter("Another parameter with validation")]
    public int MyNumberParameter { get; set; } = 42;
}

// 2. Use in build class
public class MyNewBuild : Base<MyNewBuildParams, DiscordNotifications>
{
    public Target Build => _ => _
        .Executes(() => {
            Logger.Information($"Using parameter: {Parameters.MyParameter}");
        });
}
```

## Testing

### Unit Testing

```csharp
[Test]
public async Task TestBuildProcess()
{
    // Arrange
    var services = new ServiceCollection();
    services.AddTransient<IGitService, MockGitService>();
    services.AddTransient<ILogger<MyBuild>, MockLogger<MyBuild>>();
    
    var serviceProvider = services.BuildServiceProvider();
    ServiceLocator.Initialize(serviceProvider);
    
    var build = new MyBuild();
    
    // Act
    var result = await build.ExecuteAsync();
    
    // Assert
    Assert.That(result, Is.True);
}
```

### Integration Testing

```bash
# Test with real container
docker run \
  -v /var/run/docker.sock:/var/run/docker.sock \
  -v ./test-project:/workspace \
  -e GITHUB_TOKEN=$GITHUB_TOKEN \
  -it build-agent:dev \
  build node-in-docker --dry-run true
```

### Manual Testing

```bash
# Test different build types
.\build.ps1 -type docker --dry-run true
.\build.ps1 -type node --artifacts-dir ./test-output
.\build.ps1 -type node-in-docker --verbosity Verbose

# Test with different parameters
.\build.ps1 -type docker --image-tag test:latest --registry-url localhost:5000
```

## Debugging

### Local Debugging

1. **Visual Studio**: Set startup project to the build type you want to debug
2. **VS Code**: Use the provided launch configurations
3. **Command Line**: Use `dotnet run` with `--` separator for arguments

```bash
# Debug with specific arguments
dotnet run --project forge/Docker/Docker.csproj -- Build --verbosity Verbose --dry-run true
```

### Container Debugging

```bash
# Run container interactively
docker run -it --entrypoint /bin/bash build-agent:dev

# Check installed tools
which docker
which node
which dotnet

# Test build commands manually
build docker --help
build node --help
```

### Common Issues

#### .NET Build Errors

```bash
# Clean and rebuild
dotnet clean forge/Forge.sln
dotnet build forge/Forge.sln

# Restore packages
dotnet restore forge/Forge.sln
```

#### Docker Issues

```bash
# Check Docker daemon
docker version
docker info

# Check container logs
docker logs container-id

# Debug container
docker run -it --entrypoint /bin/bash build-agent:dev
```

#### PowerShell Module Issues

```powershell
# Reload the module
Remove-Module nuke-helpers -Force
Import-Module ./scripts/nuke/nuke-helpers.psm1 -Force

# Check module functions
Get-Command -Module nuke-helpers
```

## Contributing

### 1. Development Process

1. **Fork** the repository
2. **Create** a feature branch
3. **Make** your changes
4. **Test** thoroughly
5. **Submit** a pull request

### 2. Commit Guidelines

Follow conventional commit format:

```text
feat: add new build type for Python projects
fix: resolve Docker image tagging issue
docs: update multi-build architecture guide
test: add unit tests for GitService
```

### 3. Pull Request Process

1. **Update documentation** if needed
2. **Add tests** for new functionality
3. **Ensure all CI checks pass**
4. **Request review** from maintainers

### 4. Code Standards

- **Follow C# coding conventions**
- **Use dependency injection** for external dependencies
- **Add XML documentation** for public APIs
- **Include unit tests** for new features
- **Update relevant documentation**

## CI/CD Pipeline

### GitHub Actions Workflows

- **`ci.yml`**: Runs on pull requests and feature branches
- **`release.yml`**: Deploys to production on main branch
- **`docs.yml`**: Updates documentation site

### Local CI Testing

```bash
# Run the same commands as CI
dotnet build forge/Forge.sln
dotnet test forge/Forge.sln
docker build -t build-agent:test .
```

### Release Process

1. **Merge** to main branch
2. **Automatic** GitHub Actions build
3. **Container** pushed to GHCR
4. **GitHub release** created
5. **Documentation** updated

This development guide provides everything needed to contribute effectively to the Docker Build Agent project, from initial setup through advanced development scenarios.

---

