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

**Purpose**: Combines Node.js build with Docker image creation in a single command.

**What it does**:

- Executes `node-build` first to build the Node.js application
- Then executes `docker-build` to create a Docker image
- Perfect for Node.js applications that need to be containerized
- Maintains the same parameters and options as both individual commands

**Usage**:

```bash
docker run \
  -v /var/run/docker.sock:/var/run/docker.sock \
  -v ./:/workspace \
  -it ghcr.io/the-running-dev/build-agent:latest \
  node-in-docker-build
```

**Use Cases**:

- Angular applications with Nginx
- React applications with static serving
- Node.js APIs with Express
- Next.js applications

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
| Node.js apps (with container) | `node-in-docker-build` | Full-stack apps, APIs, SPAs |
| Documentation sites | `node-template-build` | Docusaurus, GitBook, static sites |

---

## 🔗 Related Documentation

- [Parameters Reference](parameters) - Detailed parameter documentation
- [Docker Templates](docker-templates) - Available Dockerfile templates
- [Customization](customization) - Custom build scripts and configuration
- [CI/CD Examples](ci-cd) - GitHub Actions integration examples
