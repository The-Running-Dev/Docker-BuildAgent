---
id: build-types
title: 🔧 Build Types & Commands
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

## 🐳 build docker

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

## 📦 build node

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

## 🔄 build node-in-docker

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
├── package.json              # Node.js project configuration
├── .build.scripts            # Optional: Custom build commands
├── Dockerfile                # Docker image definition
├── set-environment.ps1       # Optional: Environment setup
└── artifacts/                # Default output directory
    └── (built files)
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

## 📚 build node-template

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

## 📝 build forge

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

## 🧰 PowerShell Module

**Purpose**: Provides a programmable PowerShell interface for integrating Build Agent functionality into custom scripts and workflows.

**What it does**:

- Automatically generates strongly-typed functions for all build types
- Provides consistent Docker container execution pattern
- Handles parameter validation and conversion
- Supports IDE intellisense and tab completion
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
# Call dynamically generated functions for each build type
Invoke-ForgeDocker -CreateRegistry $true -DryRun $true
Invoke-ForgeNode -PackageManager "pnpm" -IsProduction $true
```

**Parameter Extraction**:

The module includes a parameter extraction script that generates function definitions from C# parameter files:

```powershell
./Update-ModuleParameters.ps1
```

---

## 🔧 Common Features

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

## 🎯 Choosing the Right Build Type

| Project Type | Command | Use Case |
|-------------|---------|----------|
| Pure Docker projects | `build docker` | Existing Dockerfile, containerizing artifacts |
| Node.js apps (no container) | `build node` | Build and test Node.js applications |
| Node.js apps (with container) | `build node-in-docker` | Complete CI/CD pipeline with registry push |
| Documentation sites | `build node-template` | Docusaurus, GitBook, static sites |
| Changelog generation | `build forge` | Git history-based changelog creation |
| Build orchestration | `build forge` | Complex multi-stage build processes |

---

## 🔗 Related Documentation

- [Parameters Reference](parameters) - Detailed parameter documentation
- [Docker Templates](docker-templates) - Available Dockerfile templates
- [Customization](customization) - Custom build scripts and configuration
- [CI/CD Examples](ci-cd) - GitHub Actions integration examples
