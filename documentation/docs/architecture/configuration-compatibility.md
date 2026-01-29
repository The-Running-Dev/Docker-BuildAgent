---
id: configuration-compatibility
title: ⚙️ Configuration & Compatibility
sidebar_position: 4
---

This guide covers configuration options, compatibility considerations, and environment-specific settings for the Docker Build Agent.

## PowerShell Compatibility

### Version Requirements

The Docker Build Agent supports multiple PowerShell versions:

- **PowerShell 5.1** (Windows PowerShell): Full compatibility
- **PowerShell 7+** (PowerShell Core): Full compatibility, cross-platform support
- **Minimum Version**: PowerShell 5.1 or later required

### ASCII Output Mode

For maximum compatibility, especially with PowerShell 5.1 and CI environments, all output uses ASCII characters instead of Unicode emojis.

#### ASCII Prefix Mapping

| Context | ASCII Prefix | Usage |
|---------|-------------|--------|
| Successful operations | `[OK]` | Build completions, successful tasks |
| Error conditions | `[ERROR]` | Build failures, critical issues |
| Warning messages | `[WARN]` | Non-critical issues, deprecations |
| Configuration setup | `[CONFIG]` | Environment setup, parameter configuration |
| File operations | `[COPY]` | File and directory operations |
| Build processes | `[BUILD]` | Compilation, image creation |
| Processing operations | `[PROCESS]` | Data processing, transformations |
| Package management | `[SETUP]` | Dependencies, installations |
| Detection/validation | `[DETECT]` / `[CHECK]` | Auto-detection, validation |
| Installation operations | `[INSTALL]` / `[CLONE]` | Downloads, git operations |
| Cleanup operations | `[CLEAN]` | Temporary file removal |
| Informational messages | `[INFO]` | Status updates, progress |
| Skipped operations | `[SKIP]` | Conditional skips |
| Push operations | `[PUSH]` | Registry pushes, deployments |
| Git tagging | `[TAG]` | Version tagging |

### Execution Policy

Ensure PowerShell execution policy allows script execution:

```powershell
# Check current execution policy
Get-ExecutionPolicy

# Set execution policy (if needed)
Set-ExecutionPolicy -ExecutionPolicy RemoteSigned -Scope CurrentUser
```

## Environment Configuration

### Required Environment Variables

These environment variables are essential for full functionality:

```bash
# GitHub Integration
GITHUB_TOKEN=ghp_xxxxxxxxxxxxxxxxxxxx
GITHUB_ACTOR=your-username

# Container Registry
REGISTRY_URL=ghcr.io
REGISTRY_USER=your-username
REGISTRY_TOKEN=ghp_xxxxxxxxxxxxxxxxxxxx

# Optional: Notifications
NOTIFICATIONS_WEBHOOK_URL=https://discord.com/api/webhooks/...
```

### Optional Environment Variables

```bash
# Build Configuration
VERBOSITY=Normal                    # Quiet, Minimal, Normal, Verbose
DRY_RUN=false                      # true/false
FORCE_PUSH=false                   # true/false
NOTIFICATIONS=true                 # true/false

# Directory Paths
ARTIFACTS_DIR=artifacts            # Relative or absolute path
TEMPLATES_DIR=/nuke/templates      # Docker template directory
ROOT_DIRECTORY=/workspace          # Project root directory

# Version Control
REPOSITORY_URL=https://github.com/owner/repo
CHANGELOG_FROM=start               # start, last-tag, specific-tag

# Build Specific
IMAGE_TAG=latest                   # Docker image tag
DOCKER_FILE=Dockerfile             # Dockerfile name
CREATE_GITHUB_RELEASE=false        # true/false for releases
```

### Environment File Configuration

You can also use `.env` files for configuration:

```bash
# .env file example
GITHUB_TOKEN=your_token_here
DISCORD_WEBHOOK_URL=https://discord.com/api/webhooks/...
DRY_RUN=false
```

## ASCII Output Compatibility

For enhanced PowerShell 5.1+ compatibility across different platforms, the project uses ASCII alternatives instead of emoji characters in console output.

### Console Output Mapping

All emoji characters have been replaced with ASCII alternatives in square brackets:

| Original Emoji | ASCII Replacement | Usage Context |
|---------------|-------------------|---------------|
| ✅ | `[OK]` | Successful operations and completions |
| ❌ | `[ERROR]` | Error conditions and failures |
| ⚠️ | `[WARN]` | Warning messages and non-critical issues |
| 🔧 | `[CONFIG]` | Configuration and environment setup |
| 📁 | `[COPY]` | File and directory copying operations |
| 🚀 | `[BUILD]` / `[RELEASE]` | Build operations / GitHub releases |
| 🔄 | `[PROCESS]` | Processing operations (changelog generation) |
| 📦 | `[SETUP]` | Package management and setup |
| 🔍 | `[SEARCH]` | Search and discovery operations |
| 🎯 | `[TARGET]` | Target-specific operations |
| 🔨 | `[ACTION]` | Build actions and operations |
| 📊 | `[INFO]` | Information display and reporting |

### Implementation Example

Instead of:

```text
✅ Build completed successfully
🚀 Deploying to production
⚠️ Warning: Missing configuration
```

The system outputs:

```text
[OK] Build completed successfully
[BUILD] Deploying to production
[WARN] Warning: Missing configuration
```

This ensures consistent output across different terminal environments and PowerShell versions.

Create a `.env` file in your project root for local development:

```bash
# .env file (automatically loaded by build scripts)
GITHUB_TOKEN=your_token_here
REGISTRY_URL=ghcr.io
REGISTRY_USER=your_username
REGISTRY_TOKEN=your_token_here
VERBOSITY=Verbose
DRY_RUN=true
```

### Build Environment Maps

Use `.build.env.map` for more complex environment variable mapping:

```text
# .build.env.map
CreateGitHubRelease=const:true
ImageTag=env:BUILD_NUMBER,default:latest
RegistryUrl=env:CONTAINER_REGISTRY,default:ghcr.io
Verbosity=env:BUILD_VERBOSITY,default:Normal
```

## Docker Configuration

### Container Registry Setup

#### GitHub Container Registry (GHCR)

```bash
# Login to GHCR
echo $GITHUB_TOKEN | docker login ghcr.io -u $GITHUB_ACTOR --password-stdin

# Configure for builds
export REGISTRY_URL=ghcr.io
export REGISTRY_USER=$GITHUB_ACTOR
export REGISTRY_TOKEN=$GITHUB_TOKEN
```

#### Azure Container Registry (ACR)

```bash
# Login to ACR
az acr login --name myregistry

# Configure for builds
export REGISTRY_URL=myregistry.azurecr.io
export REGISTRY_USER=myregistry
export REGISTRY_TOKEN=$(az acr credential show -n myregistry --query "passwords[0].value" -o tsv)
```

#### Docker Hub

```bash
# Login to Docker Hub
docker login -u $DOCKER_USERNAME -p $DOCKER_TOKEN

# Configure for builds
export REGISTRY_URL=docker.io
export REGISTRY_USER=$DOCKER_USERNAME
export REGISTRY_TOKEN=$DOCKER_TOKEN
```

### Docker Engine Configuration

#### Docker Socket Access

For Docker-in-Docker scenarios:

```bash
# Linux/macOS
docker run \
  -v /var/run/docker.sock:/var/run/docker.sock \
  -v ./:/workspace \
  -it ghcr.io/the-running-dev/build-agent:latest \
  build docker

# Windows (Docker Desktop)
docker run \
  -v //var/run/docker.sock:/var/run/docker.sock \
  -v ${PWD}:/workspace \
  -it ghcr.io/the-running-dev/build-agent:latest \
  build docker
```

#### Docker Daemon Configuration

For custom Docker daemon settings, configure `/etc/docker/daemon.json`:

```json
{
  "insecure-registries": ["localhost:5000"],
  "registry-mirrors": ["https://mirror.gcr.io"],
  "log-driver": "json-file",
  "log-opts": {
    "max-size": "10m",
    "max-file": "3"
  }
}
```

## Node.js Configuration

### Package Manager Detection

The build system automatically detects package managers:

```text
Priority Order:
1. pnpm-lock.yaml → pnpm
2. yarn.lock → yarn  
3. package-lock.json → npm
4. Default → npm
```

### Custom Build Scripts

Create `.build.scripts` file to override default build commands:

```text
# .build.scripts
npm ci
npm run lint
npm run test
npm run build:prod
```

### Node.js Version Management

For projects requiring specific Node.js versions:

```json
// package.json
{
  "engines": {
    "node": ">=18.0.0",
    "npm": ">=8.0.0"
  }
}
```

## Git Configuration

### GitVersion Configuration

Configure semantic versioning with `GitVersion.yml`:

```yaml
# GitVersion.yml
mode: Mainline
branches:
  main:
    tag: ''
  develop:
    tag: 'beta'
  feature:
    tag: 'alpha'
ignore:
  sha: []
merge-message-formats: {}
```

### Git Credentials

#### Personal Access Token

```bash
# Configure Git with PAT
git config --global user.name "Your Name"
git config --global user.email "your.email@example.com"
git config --global credential.helper store

# Store credentials
echo "https://${GITHUB_TOKEN}@github.com" > ~/.git-credentials
```

#### SSH Key Authentication

```bash
# Generate SSH key
ssh-keygen -t ed25519 -C "your.email@example.com"

# Add to SSH agent
eval "$(ssh-agent -s)"
ssh-add ~/.ssh/id_ed25519

# Add public key to GitHub
cat ~/.ssh/id_ed25519.pub
```

## Notification Configuration

### Discord Notifications

Configure Discord webhook notifications:

```bash
# Set Discord webhook URL
export NOTIFICATIONS_WEBHOOK_URL="https://discord.com/api/webhooks/YOUR_WEBHOOK_ID/YOUR_WEBHOOK_TOKEN"

# Enable notifications
export NOTIFICATIONS=true
export FORCE_NOTIFICATIONS=false  # Only send on failures by default
```

### Custom Notification Services

Extend notification system by implementing `INotifications`:

```csharp
public class SlackNotifications : INotifications
{
    public async Task SendSuccessAsync(string message)
    {
        // Implement Slack notification
    }
    
    public async Task SendFailureAsync(string message)
    {
        // Implement Slack notification
    }
}
```

## Platform-Specific Configuration

### Windows Configuration

```powershell
# PowerShell execution policy
Set-ExecutionPolicy RemoteSigned -Scope CurrentUser

# Windows-specific paths
$env:ARTIFACTS_DIR = ".\artifacts"
$env:TEMPLATES_DIR = ".\templates"

# Docker Desktop configuration
$env:DOCKER_HOST = "tcp://localhost:2375"  # If using TCP
```

### Linux Configuration

```bash
# Docker group membership
sudo usermod -aG docker $USER
newgrp docker

# Linux-specific paths
export ARTIFACTS_DIR="./artifacts"
export TEMPLATES_DIR="./templates"

# SELinux considerations (if applicable)
sudo setsebool -P container_manage_cgroup on
```

### macOS Configuration

```bash
# Docker Desktop for Mac
# Uses Docker socket at /var/run/docker.sock by default

# Homebrew Node.js management
brew install node@18
brew link node@18

# macOS-specific paths
export ARTIFACTS_DIR="./artifacts"
export TEMPLATES_DIR="./templates"
```

## CI/CD Platform Configuration

### GitHub Actions

```yaml
# .github/workflows/build.yml
env:
  GITHUB_TOKEN: ${{ secrets.GITHUB_TOKEN }}
  REGISTRY_TOKEN: ${{ secrets.REGISTRY_TOKEN }}
  NOTIFICATIONS_WEBHOOK_URL: ${{ secrets.DISCORD_WEBHOOK }}
  VERBOSITY: Normal
  DRY_RUN: false
```

### Azure DevOps

```yaml
# azure-pipelines.yml
variables:
  GITHUB_TOKEN: $(github-token)
  REGISTRY_URL: $(container-registry-url)
  REGISTRY_TOKEN: $(container-registry-token)
  VERBOSITY: Normal
```

### GitLab CI

```yaml
# .gitlab-ci.yml
variables:
  GITHUB_TOKEN: $GITHUB_TOKEN
  REGISTRY_URL: registry.gitlab.com
  REGISTRY_TOKEN: $CI_REGISTRY_PASSWORD
  VERBOSITY: Normal
```

## Troubleshooting Configuration

### Common Configuration Issues

#### PowerShell Execution Policy

```powershell
# Error: "execution of scripts is disabled on this system"
Set-ExecutionPolicy -ExecutionPolicy RemoteSigned -Scope CurrentUser
```

#### Docker Permission Issues

```bash
# Error: "permission denied while trying to connect to Docker daemon"
sudo usermod -aG docker $USER
newgrp docker
```

#### Registry Authentication

```bash
# Error: "unauthorized: authentication required"
echo $REGISTRY_TOKEN | docker login $REGISTRY_URL -u $REGISTRY_USER --password-stdin
```

#### Environment Variable Issues

```bash
# Debug environment variables
env | grep -E "(GITHUB|REGISTRY|DOCKER)"

# Check specific variables
echo "GITHUB_TOKEN: ${GITHUB_TOKEN}"
echo "REGISTRY_URL: ${REGISTRY_URL}"
```

### Debugging Configuration

#### Verbose Logging

```bash
# Enable verbose logging
export VERBOSITY=Verbose

# Run with debugging
./build.ps1 -type docker --verbosity Verbose --dry-run true
```

#### Configuration Validation

```powershell
# Validate PowerShell environment
$PSVersionTable
Get-ExecutionPolicy

# Validate Docker environment
docker version
docker info

# Validate .NET environment
dotnet --version
dotnet --list-sdks
```

#### Test Configuration

```bash
# Test with minimal configuration
docker run \
  -v ./:/workspace \
  -e VERBOSITY=Verbose \
  -e DRY_RUN=true \
  -it ghcr.io/the-running-dev/build-agent:latest \
  build node
```

This configuration guide ensures compatibility across different environments and platforms while providing comprehensive setup instructions for all supported scenarios.
