---
id: build-types
title: 🔧 Build Types & Commands
sidebar_position: 2
---

The Build Agent provides several specialized build commands (executables) available in the container. Each command is optimized for specific project types and use cases.

## 🐳 docker-build

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
  docker-build
```

**Common Parameters**:

- `--dry-run true` - Simulate build without pushing
- `--create-github-release true` - Create GitHub release
- `--force-push true` - Force push even in dry-run scenarios

---

## 📦 node-build

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
  node-build
```

**Build Scripts Convention**:

If no `.build.scripts` file exists, defaults to:

```text
{detected-package-manager} install
{detected-package-manager} run build:prod
```

---

## 🔄 node-in-docker-build

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
7. `CreateGitTag` - Tag the release in Git
8. `PushToRegistry` - Push image to container registry
9. `PublishToGitHub` - Create GitHub release
10. `Build` - Final completion target

**Usage**:

```bash
# Basic usage
docker run \
  -v /var/run/docker.sock:/var/run/docker.sock \
  -v ./:/workspace \
  -it ghcr.io/the-running-dev/build-agent:latest \
  node-in-docker-build

# With custom parameters
docker run \
  -v /var/run/docker.sock:/var/run/docker.sock \
  -v ./:/workspace \
  -it ghcr.io/the-running-dev/build-agent:latest \
  node-in-docker-build \
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
node-in-docker-build \
  --artifacts-dir ./build \
  --image-tag myapp:v1.2.3 \
  --registry-url ghcr.io/myorg \
  --registry-user $GITHUB_ACTOR \
  --registry-token $GITHUB_TOKEN \
  --create-github-release true \
  --release-tag v1.2.3

# Development build (dry run)
node-in-docker-build \
  --image-tag myapp:dev \
  --dry-run true \
  --verbosity Verbose

# Custom Dockerfile and artifacts location
node-in-docker-build \
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

## 📚 node-template-build

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
  node-template-build -appDir documentation
```

**Key Parameters**:

- `-appDir` - Target directory for documentation (default: 'documentation')
- `-packageManager` - Force specific package manager (npm/pnpm/yarn)
- `-skipInstall` - Skip npm install step
- `-isProduction` - Build for production using build:prod script
- `-nodeTemplateRepositoryUrl` - Custom template repository URL

**Examples**:

```bash
# Basic usage with auto-detection
node-template-build

# Custom directory with specific package manager
node-template-build -appDir docs-ui -packageManager pnpm

# Skip install and build for development
node-template-build -skipInstall -isProduction:$false

# Use custom template repository
node-template-build -nodeTemplateRepositoryUrl https://github.com/my-org/custom-template.git
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

- Colored console output with emojis
- Detailed progress information
- Error handling with meaningful messages

### Integration

- GitHub release creation
- Discord notifications
- Container registry push
- Git tag creation

---

## 🎯 Choosing the Right Build Type

| Project Type | Recommended Command | Use Case |
|-------------|-------------------|----------|
| Pure Docker projects | `docker-build` | Existing Dockerfile, containerizing artifacts |
| Node.js apps (no container) | `node-build` | Build and test Node.js applications |
| Node.js apps (with container) | `node-in-docker-build` | Complete CI/CD pipeline with registry push |
| Documentation sites | `node-template-build` | Docusaurus, GitBook, static sites |

---

## 🔗 Related Documentation

- [Parameters Reference](parameters) - Detailed parameter documentation
- [Docker Templates](docker-templates) - Available Dockerfile templates
- [Customization](customization) - Custom build scripts and configuration
- [CI/CD Examples](ci-cd) - GitHub Actions integration examples
