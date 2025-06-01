# Docker-BuildAgent

![Build Status](https://github.com/the-running-dev/Docker-BuildAgent/actions/workflows/ci.yml/badge.svg?branch=main)

---

## Table of Contents

- [Docker-BuildAgent](#docker-buildagent)
  - [Table of Contents](#table-of-contents)
  - [Overview](#overview)
  - [Features](#features)
  - [Project Structure](#project-structure)
  - [Prerequisites](#prerequisites)
  - [Usage](#usage)
    - [Building and Running Locally](#building-and-running-locally)
    - [Build and Push to Container Registry](#build-and-push-to-container-registry)
    - [GitHub Actions CI/CD](#github-actions-cicd)
      - [Workflow Steps](#workflow-steps)
      - [Tagging Support](#tagging-support)
  - [Environment Variables](#environment-variables)
  - [Customization](#customization)
  - [Troubleshooting](#troubleshooting)
  - [Image Details](#image-details)
  - [Nuke Build Automation](#nuke-build-automation)
    - [Docker Nuke Targets](#docker-nuke-targets)
    - [Passing Parameters to Nuke](#passing-parameters-to-nuke)
    - [Environment Variables for Nuke](#environment-variables-for-nuke)
    - [Running Nuke Locally](#running-nuke-locally)
    - [Nuke in CI/CD](#nuke-in-cicd)
    - [Using Predefined Nuke Scripts in Your Projects](#using-predefined-nuke-scripts-in-your-projects)
      - [Example: Run Nuke Build in Your Container Project](#example-run-nuke-build-in-your-container-project)
      - [Example GitHub Action: Run Nuke Build in Your Container Project](#example-github-action-run-nuke-build-in-your-container-project)
  - [Security Notes](#security-notes)
  - [Related Resources](#related-resources)
  - [Contributing](#contributing)
  - [FAQ / Common Issues](#faq--common-issues)

---

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
- **Cross-platform build scripts** (`build.sh`, `build.ps1`, `build.cmd`)
- **Ready-to-use in CI/CD pipelines** (e.g., GitHub Actions)

## Project Structure

```text
Dockerfile                # Main Docker image definition
README.md                 # Project documentation
build.sh                  # Bash build/push script (Linux/macOS/CI)
build.ps1                 # PowerShell build script (Windows)
build.cmd                 # Batch build script (Windows)
Build.sln                 # .NET solution for build automation
GitVersion.yml            # GitVersion configuration for semantic versioning
docker/                   # .NET build project (Nuke, custom build logic)
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

You can use the provided `build.sh`, `build.ps1`, or `build.cmd` script to build and push the image to GHCR. The scripts support custom tags (e.g., version numbers) as well as the default `latest` tag.

1. **Set the required environment variable:**
   - `PackagesToken`: Your packages token (with write:packages scope)

2. **Run the build script:**

   ```sh
   # For the latest tag (default)
   export PackagesToken=YOUR_TOKEN
   chmod +x build.sh
   ./build.sh

   # For a custom tag (e.g., v1.2.3)
   ./build.sh v1.2.3
   ```
   Or on Windows:

   ```powershell
   $env:PackagesToken="YOUR_TOKEN"
   ./build.ps1 v1.2.3
   ```

### GitHub Actions CI/CD

The `.github/workflows/ci.yml` workflow automates building, linting, scanning, and pushing the Docker image on every push to the `main` branch, or when a new tag is pushed, or via manual dispatch. It requires the `GITHUBPACKAGESTOKEN` secret to be set in your repository.

#### Workflow Steps

- Checks out the repository
- Sets up Docker Buildx
- Sets up .NET SDK
- Installs GitVersion
- Installs NUKE
- Runs the NUKE target ContainerCI

#### Tagging Support

- On branch pushes, the image is tagged as `latest`.
- On tag pushes (e.g., `v1.2.3`), the image is tagged accordingly (e.g., `ghcr.io/the-running-dev/build-agent:v1.2.3`).

## Environment Variables

- `PackagesToken`: Packages token used for authenticating with your repository (Ex. GHCR, required for pushing images)

## Customization

- Modify the `Dockerfile` to add or remove tools as needed for your build environment.
- To change the base image, edit the `FROM` line in the `Dockerfile`.
- To install additional global npm packages, add them to the `npm install -g` command in the `Dockerfile`.
- Add or update PowerShell/.NET tools as needed using `dotnet tool install --global <tool>`.
- Use the `docker/` directory for advanced .NET build automation with Nuke.

## Troubleshooting

- **Docker login/authentication errors:**
  - Ensure your `PackagesToken` is valid and has the correct permissions (`write:packages`).
  - Use GitHub Actions secrets for sensitive values.
- **Permission denied on build.sh:**
  - Run `chmod +x build.sh` before executing the script.
- **Image push fails:**
  - Check your network connection and GHCR access rights.
- **.NET build issues:**
  - Ensure you have the .NET 8 SDK installed locally if running .NET builds outside the container.
- **CI tool access issues:**
  - Ensure the CI environment has access to the required tools and permissions. The workflow sets up tools in the `/root/.dotnet/tools` directory and updates the PATH.

## Image Details

- **Base Image:** `mcr.microsoft.com/devcontainers/javascript-node:latest`
- **Installed Tools:**
  - Node.js, npm, Angular CLI, TypeScript, Docker, PowerShell, .NET 8 SDK, Git, GitVersion, Nuke
- **Default Shell:** PowerShell (`pwsh`)
- **Default Working Directory:** `/workspace`
- **How to update tool versions:** Edit the `Dockerfile` to specify desired versions.
- **Build Automation:** The `docker/` directory contains a .NET (Nuke) build project for advanced automation. The `nuke/docker-ci` script enables containerized builds.

## Nuke Build Automation

This project uses [Nuke](https://nuke.build/) for advanced .NET build automation. The `docker/` directory contains a Nuke build project (`Docker.csproj`, `Docker.cs`, etc.) that defines custom build logic for CI/CD scenarios, including Docker image builds, versioning, and publishing.

### Docker Nuke Targets

- `ContainerCI` – Main entry for CI builds; depends on `Publish` and marks CI completion
- `GetVersion` – Runs GitVersion, writes resolved version to file
- `Clean` – Removes version file and cleans up artifacts
- `ValidateInputs` – Ensures required parameters are set, depends on `GetVersion`
- `BuildContainer` – Builds and tags Docker images, depends on `PrintInfo`
- `Publish` – Final publish step, depends on `Push`
- `Push` – Logs in and pushes Docker images, depends on `Tag`
- `Tag` – Creates and pushes a Git tag for the version, depends on `BuildContainer`
- `PrintInfo` – Prints build and environment info, depends on `ValidateInputs`

### Passing Parameters to Nuke

You can pass parameters to Nuke using the `--parameter value` syntax. For example:

```pwsh
./build.sh --target ContainerCI --configuration Release --docker-tag v1.2.3
```

### Environment Variables for Nuke

- `Repository` – Docker image repository (e.g., ghcr.io/owner/image)
- `RepositoryUsername` – Username for the Docker registry (e.g., GitHub username)
- `RepositoryToken` – Token or password for the Docker registry (e.g., GitHub Packages token)
- `ImageTag` – Tag for the Docker image (e.g., latest, v1.2.3)
- `Dockerfile` – Path to the Dockerfile (default: Dockerfile)
- `DryRun` – Set to true to skip push and tag steps (optional)
- `ForceCiBehavior` – Set to true to force push/tag even during local builds (optional)
- Any other secrets or tokens used in your build logic should be set as environment variables or GitHub Actions secrets

### Running Nuke Locally

1. **Restore .NET tools and dependencies:**

   ```pwsh
   dotnet tool restore
   dotnet restore docker/Docker.csproj
   ```

2. **Run a Nuke target (e.g., BuildAndPush):**

   ```pwsh
   ./build.ps1 ContainerCI
   ```
   
   ```sh
   ./build.sh ContainerCI
   ```

3. **List all available Nuke targets and parameters:**

   ```pwsh
   ./build.ps1 --help
   ```

### Nuke in CI/CD

- The GitHub Actions workflow (`.github/workflows/ci.yml`) installs .NET tools, sets up the environment, and runs Nuke targets as part of the build process.
- The workflow combines tool installation, symlink creation, and PATH setup for reliable tool access.
- You can customize the workflow to run specific targets (e.g., `ContainerCI`, `Publish`, `Tag`, etc.).
- The Nuke build scripts (`build.sh`, `build.ps1`, `build.cmd`) are cross-platform entry points for running the build pipeline.

### Using Predefined Nuke Scripts in Your Projects

- The `nuke` directory contains scripts and configuration for running Nuke builds for your container projects, enabling reproducible and isolated build environments.
- The `nuke/docker-ci` script can be used to run builds inside a container, ensuring consistency across environments.
- To use this feature, mount your source code and invoke the Nuke build script inside a Docker container, or adapt the provided scripts for your CI/CD system.

#### Example: Run Nuke Build in Your Container Project

```pwsh
# Run from the project root, mounting the workspace
# (adjust the path as needed for your environment)
docker run --rm -it \
    -v "${PWD}:/workspace" \
    -w "/workspace" \
    ghcr.io/the-running-dev/build-agent:latest pwsh -Command "nuke --target ContainerCI"
    # or through a predefined command
    # -Command "container-ci"
```

#### Example GitHub Action: Run Nuke Build in Your Container Project

```yaml
jobs:
  Container:
    runs-on: ubuntu-latest
    container:
      image: ghcr.io/the-running-dev/build-agent:latest
    steps:
      - name: Checkout Repository
        uses: actions/checkout@v3

      - name: Container CI
        run: container-ci
        env:
          PackagesToken: ${{ secrets.GITHUBPACKAGESTOKEN }}
```

## Security Notes

- **Never expose your GitHub token in logs or public repositories.**
- Always use GitHub Actions secrets or environment variables for sensitive data.
- Review Dockerfile and scripts for any hardcoded credentials before sharing.
- The build process removes npm cache to prevent accidental exposure of secrets or private keys.

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


## FAQ / Common Issues

- **Q: I get a permission denied error on build.sh**
  - A: Run `chmod +x build.sh` before executing the script.
- **Q: .NET build fails outside the container**
  - A: Make sure you have the .NET 8 SDK installed locally.
- **Q: How do I pass secrets to Nuke or Docker builds?**
  - A: Use environment variables or GitHub Actions secrets. Never hardcode secrets in scripts or Dockerfiles.
- **Q: GitVersion or Nuke not found in CI?**
  - A: The workflow now installs .NET tools globally, creates a symlink for GitVersion, and adds `/root/.dotnet/tools` to the PATH for reliable access.