- [📚 Documentation](#-documentation)
  - [Key Documentation Pages](#key-documentation-pages)
- [📊 Project Status](#-project-status)
- [Overview](#overview)
- [Features](#features)
- [Project Structure](#project-structure)
- [Prerequisites](#prerequisites)
- [Usage](#usage)
  - [Building and Running Locally](#building-and-running-locally)
  - [Build and Push to Container Registry](#build-and-push-to-container-registry)
  - [GitHub Actions CI/CD](#github-actions-cicd)
    - [Release Strategy](#release-strategy)
    - [Workflow Steps](#workflow-steps)
    - [Tagging Support](#tagging-support)
- [Environment Variables](#environment-variables)
- [Customization](#customization)
- [Image Details](#image-details)
- [Example: Build Commands](#example-build-commands)
- [Example GitHub Action: Run Nuke Build in Your Container Project](#example-github-action-run-nuke-build-in-your-container-project)
- [Related Resources](#related-resources)
- [Contributing](#contributing)

## 📚 Documentation

This repository includes a full documentation site at [build-agent.subzerodev.com](https://build-agent.subzerodev.com), covering all usage, customization, parameters, targets, advanced topics, and troubleshooting for the Build Agent and Forge system.

**Start here:** [Docs Site Home](https://build-agent.subzerodev.com)

### Key Documentation Pages

- [🚀 Fast Track / Usage Guide](https://build-agent.subzerodev.com/docs/usage)
- [📝 Customization Options](https://build-agent.subzerodev.com/docs/customization)
- [⚙️ Parameters & Settings](https://build-agent.subzerodev.com/docs/parameters)
- [🎯 Build Targets](https://build-agent.subzerodev.com/docs/targets)
- [🐳 Docker Templates](https://build-agent.subzerodev.com/docs/docker-templates)
- [🔄 CI/CD Examples](https://build-agent.subzerodev.com/docs/ci-cd)
- [⚡ Advanced Usage](https://build-agent.subzerodev.com/docs/advanced)
- [🛠️ Development Setup](https://build-agent.subzerodev.com/docs/development)
- [❓ Troubleshooting & FAQ](https://build-agent.subzerodev.com/docs/troubleshooting)

For the most up-to-date and detailed information, always refer to the documentation site. The rest of this README provides a high-level summary.

## 📊 Project Status

[![CI](https://github.com/the-running-dev/Docker-BuildAgent/actions/workflows/ci.yml/badge.svg?branch=main)](https://github.com/the-running-dev/Docker-BuildAgent/actions/workflows/ci.yml)
[![Deploy](https://github.com/the-running-dev/Docker-BuildAgent/actions/workflows/release.yml/badge.svg?branch=main)](https://github.com/the-running-dev/Docker-BuildAgent/actions/workflows/release.yml)
[![Version](https://img.shields.io/github/v/release/the-running-dev/Docker-BuildAgent?logo=semver&logoColor=white&label=Version)](https://github.com/the-running-dev/Docker-BuildAgent/releases/latest)
[![License](https://img.shields.io/badge/License-MIT-blue?logo=opensourceinitiative&logoColor=white)](https://github.com/the-running-dev/Docker-BuildAgent/blob/main/LICENSE)
[![Docs](https://img.shields.io/badge/Docs-Live-blue?logo=gitbook&logoColor=white)](https://build-agent.subzerodev.com)

> 📋 For comprehensive project metrics including build health, security scans, community stats, and development activity, visit the [**📊 Project Status Dashboard**](https://build-agent.subzerodev.com/docs/project-status) in our documentation.

## Overview

Docker-BuildAgent is a pre-configured Docker image and build environment designed for CI/CD pipelines. It supports JavaScript/TypeScript, NodeJS, Angular, .NET, and PowerShell development, and is ready for use in GitHub Actions or other CI/CD systems. The project includes a robust Dockerfile, build scripts for multiple platforms, and a sample GitHub Actions workflow for automated builds.

## Features

- **Node.js** and **npm** for JavaScript/TypeScript development
- **Angular CLI** and **TypeScript** for Angular projects
- **Docker** for containerized builds and deployments
- **PowerShell** for advanced scripting
- **.NET 8 SDK** for .NET builds and tools
- **Git** for source control
- **GitVersion** for semantic versioning in CI/CD
- **Nuke Build** support for advanced .NET build automation
- **Forge Build System** with multiple specialized build types:
  - **Docker builds** with automated image creation and registry push
  - **Node.js builds** with package manager detection and custom scripts
  - **Combined Node+Docker builds** for full-stack applications
  - **Changelog generation** with Git integration and customizable formatting
- **Cross-platform build scripts** (`build.sh`, `build.ps1`, `build.cmd`)
- **Ready-to-use in CI/CD pipelines** (e.g., GitHub Actions)

## Project Structure

```text
Dockerfile                # Main Docker image definition
README.md                 # Project documentation
build.sh                  # Bash build/push script (Linux/macOS/CI)
build.ps1                 # PowerShell build script (Windows)
GitVersion.yml            # GitVersion configuration for semantic versioning
forge/                    # Forge build system with multiple specialized builds:
  ├── Common/             # Shared services, utilities, and base classes
  ├── Docker/             # Docker image build automation
  ├── Node/               # Node.js application builds
  ├── NodeInDocker/       # Combined Node.js + Docker builds
  └── Forge/              # Changelog generation and build orchestration
.github/workflows/        # GitHub Actions workflow(s)
```

## Prerequisites

- [Docker](https://www.docker.com/get-started) installed on your machine (for local builds)
- Access to [GitHub Container Registry (GHCR)](https://ghcr.io/)
- A valid GitHub Packages token with permissions to push to GHCR
- [.NET 8 SDK](https://dotnet.microsoft.com/en-us/download/dotnet/8.0) (for local .NET builds)

## Usage

### Building and Running Locally

1. **Clone the repository:**

   ```pwsh
   git clone https://github.com/the-running-dev/Docker-BuildAgent
   cd Docker-BuildAgent
   ```

2. **Build the Docker image:**

   ```pwsh
   docker build -t build-agent:latest .
   ```

3. **Run the container:**

   ```pwsh
   docker run -it build-agent:latest
   ```

### Build and Push to Container Registry

You can use the provided `build.sh`, `build.ps1` script to build and push the image to GHCR. The scripts support custom tags (e.g., version numbers) as well as the default `latest` tag.

1. **Set the required environment variable:**
   - `PackagesToken`: Your packages token (with write:packages scope)

2. **Run the build script:**

   ```sh
   # For the latest tag (default)
   export REGISTRY_TOKEN=YOUR_TOKEN
   chmod +x build.sh
   ./build.sh

   # For a custom tag (e.g., v1.2.3)
   ./build.sh v1.2.3
   ```
   Or on Windows:

   ```powershell
   $env:RegistryToken="YOUR_TOKEN"
   ./build.ps1 v1.2.3
   ```

### GitHub Actions CI/CD

The repository includes multiple GitHub Actions workflows for different purposes:

- **CI Workflow** (`.github/workflows/ci.yml`): Runs on pull requests and feature branches for validation
- **Deploy Workflow** (`.github/workflows/release.yml`): Builds and publishes Docker images on every main branch push
- **Create Release Workflow** (`.github/workflows/create-release.yml`): Manual workflow to create official releases
- **Tag Release Workflow** (`.github/workflows/tag-release.yml`): Creates releases when version tags are pushed

#### Release Strategy

The project uses a **controlled release strategy** to distinguish between development builds and official releases:

**For Development:**

- Every push to `main` → Triggers "Deploy" workflow → Builds and publishes Docker images (no GitHub releases)
- Pull requests → Triggers "CI" workflow → Validation only

**For Official Releases:**

- **Manual releases**: Use "Create Release" workflow in GitHub Actions
- **Tag-based releases**: Push a version tag (e.g., `git tag v1.2.3 && git push origin v1.2.3`)

#### Workflow Steps

- Checks out the repository
- Sets up Docker Buildx
- Sets up .NET SDK
- Installs GitVersion
- Installs NUKE
- Runs the NUKE target Build

#### Tagging Support

- **Development builds**: On main branch pushes, Docker images are tagged with version numbers from GitVersion
- **Release builds**: On manual release creation or tag pushes, GitHub releases are created with proper changelog and artifacts
- **Pre-releases**: Tags with suffixes like `v1.0.0-beta.1` or `v1.0.0-rc.1` are marked as pre-releases

## Environment Variables

- `PackagesToken`: Packages token used for authenticating with your repository (Ex. GHCR, required for pushing images)

## Customization

- Modify the `Dockerfile` to add or remove tools as needed for your build environment.
- To change the base image, edit the `FROM` line in the `Dockerfile`.
- To install additional global npm packages, add them to the `npm install -g` command in the `Dockerfile`.
- Add or update PowerShell/.NET tools as needed using `dotnet tool install --global <tool>`.
- Use the `Forge/` directory for advanced .NET build automation with Nuke.
- **Docker Templates:** Store custom Dockerfile templates in your repository at `.github/templates/` or `templates/` directory. The build agent will automatically discover and use them when no Dockerfile exists in your project. See [Docker Templates documentation](https://build-agent.subzerodev.com/docs/docker-templates) for details.

## Image Details

- **Base Image:** `mcr.microsoft.com/devcontainers/javascript-node:latest`
- **Installed Tools:**
  - Node.js, npm, Angular CLI, TypeScript, Docker, PowerShell, .NET 8 SDK, Git, GitVersion, Nuke
- **Default Shell:** PowerShell (`pwsh`)
- **Default Working Directory:** `/workspace`
- **How to update tool versions:** Edit the `Dockerfile` to specify desired versions.
- **Build Automation:** The `forge/` directory contains a comprehensive .NET (Nuke) build system with multiple specialized builds for different project types. Each build provides specific commands like `docker-build`, `node-build`, `node-in-docker-build`, and `forge` for changelog generation.
- **Changelog Generation:** Built-in changelog generation with customizable date formatting (yyyy.MM.dd), tag-based filtering, and automatic prepending to existing changelogs.

## Example: Build Commands

The build agent provides specialized commands for different project types:

```pwsh
# Build a Docker image from your project
docker run --rm -it \
    -v "${PWD}:/workspace" \
    -v /var/run/docker.sock:/var/run/docker.sock \
    ghcr.io/the-running-dev/build-agent:latest \
    docker-build

# Build a Node.js application  
docker run --rm -it \
    -v "${PWD}:/workspace" \
    ghcr.io/the-running-dev/build-agent:latest \
    node-build

# Build Node.js app and create Docker image
docker run --rm -it \
    -v "${PWD}:/workspace" \
    -v /var/run/docker.sock:/var/run/docker.sock \
    ghcr.io/the-running-dev/build-agent:latest \
    node-in-docker-build

# Generate changelog from Git history
docker run --rm -it \
    -v "${PWD}:/workspace" \
    ghcr.io/the-running-dev/build-agent:latest \
    forge --target GenerateChangeLog
```

## Example GitHub Action: Run Nuke Build in Your Container Project

```yaml
name: Container-CI

on:
  workflow_dispatch:
  push:
    branches:
      - main

permissions:
  packages: write
  contents: write

jobs:
  Container-CI:
    runs-on: ubuntu-latest
    container:
      image: ghcr.io/the-running-dev/build-agent:latest

    steps:
      - name: Checkout Repository
        uses: actions/checkout@v3
        with:
          fetch-depth: 0

      - name: Container CI
        run: docker-build
        env:
          GITHUB_TOKEN: ${{ secrets.GITHUB_TOKEN }}
          RegistryToken: ${{ secrets.REGISTRY_TOKEN }}
```

## Related Resources

- [Docker Documentation](https://docs.docker.com/)
- [GitHub Actions Documentation](https://docs.github.com/en/actions)
- [GitHub Container Registry](https://docs.github.com/en/packages/working-with-a-github-packages-registry/working-with-the-container-registry)
- [Node.js](https://nodejs.org/)
- [Angular CLI](https://angular.io/cli)
- [PowerShell](https://docs.microsoft.com/powershell/)
- [.NET](https://dotnet.microsoft.com/)
- [GitVersion](https://gitversion.net/)
- [Nuke Build](https://nuke.build/)

## Contributing

For questions, issues, or support, please open an [issue](https://github.com/the-running-dev/Docker-BuildAgent/issues).
