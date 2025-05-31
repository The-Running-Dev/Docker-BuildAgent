# Docker-BuildAgent

![Build Status](https://img.shields.io/github/actions/workflow/status/the-running-dev/Docker-BuildAgent/build-and-push.yml?branch=main)
![License](https://img.shields.io/github/license/the-running-dev/Docker-BuildAgent)

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
    - [Build and Push to GitHub Container Registry](#build-and-push-to-github-container-registry)
    - [GitHub Actions CI/CD](#github-actions-cicd)
      - [Workflow Steps](#workflow-steps)
      - [Tagging Support](#tagging-support)
  - [Environment Variables](#environment-variables)
  - [Customization](#customization)
  - [Troubleshooting](#troubleshooting)
  - [Image Details](#image-details)
  - [Nuke Build Automation](#nuke-build-automation)
    - [Common Nuke Targets](#common-nuke-targets)
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
  - [License](#license)
  - [Contact](#contact)
  - [Quick Start](#quick-start)
  - [FAQ / Common Issues](#faq--common-issues)
  - [Changelog](#changelog)

---

## Overview

Docker-BuildAgent is a pre-configured Docker image and build environment designed for CI/CD pipelines. It supports JavaScript/TypeScript, Angular, .NET, and PowerShell development, and is ready for use in GitHub Actions or other CI/CD systems. The project includes a robust Dockerfile, build scripts for multiple platforms, and a sample GitHub Actions workflow for automated builds, linting, and security scanning.

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
- **Security best practices** (npm cache cleaning, Trivy scanning)
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
build/                    # .NET build project (Nuke, custom build logic)
vscode.code-workspace     # VS Code workspace settings
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

   ```sh
   git clone <repo-url>
   cd Docker-BuildAgent
   ```

2. **Build the Docker image:**

   ```sh
   docker build -t build-agent:latest .
   ```

3. **Run the container:**

   ```sh
   docker run -it build-agent:latest
   ```

### Build and Push to GitHub Container Registry

You can use the provided `build.sh`, `build.ps1`, or `build.cmd` script to build and push the image to GHCR. The scripts support custom tags (e.g., version numbers) as well as the default `latest` tag.

1. **Set the required environment variable:**
   - `GitHubPackagesToken`: Your GitHub Packages token (with write:packages scope)

2. **Run the build script:**

   ```sh
   # For the latest tag (default)
   export GitHubPackagesToken=YOUR_TOKEN
   chmod +x build.sh
   ./build.sh

   # For a custom tag (e.g., v1.2.3)
   ./build.sh v1.2.3
   ```
   Or on Windows:

   ```powershell
   $env:GitHubPackagesToken="YOUR_TOKEN"
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
- Runs the NUKE target BuildAndPush

#### Tagging Support

- On branch pushes, the image is tagged as `latest`.
- On tag pushes (e.g., `v1.2.3`), the image is tagged accordingly (e.g., `ghcr.io/the-running-dev/build-agent:v1.2.3`).

## Environment Variables

- `GitHubPackagesToken`: GitHub Packages token used for authenticating with GHCR (required for pushing images)

## Customization

- Modify the `Dockerfile` to add or remove tools as needed for your build environment.
- To change the base image, edit the `FROM` line in the `Dockerfile`.
- To install additional global npm packages, add them to the `npm install -g` command in the `Dockerfile`.
- Add or update PowerShell/.NET tools as needed using `dotnet tool install --global <tool>`.
- Use the `build/` directory for advanced .NET build automation with Nuke.

## Troubleshooting

- **Docker login/authentication errors:**
  - Ensure your `GitHubPackagesToken` is valid and has the correct permissions (`write:packages`).
  - Use GitHub Actions secrets for sensitive values.
- **Permission denied on build.sh:**
  - Run `chmod +x build.sh` before executing the script.
- **Image push fails:**
  - Check your network connection and GHCR access rights.
- **Lint or security scan failures:**
  - Review the output from shellcheck or Trivy for actionable issues.
- **Trivy finds secrets in npm cache:**
  - The Dockerfile now cleans npm cache after global installs to prevent this issue.
- **.NET build issues:**
  - Ensure you have the .NET 8 SDK installed locally if running .NET builds outside the container.

## Image Details

- **Base Image:** `mcr.microsoft.com/devcontainers/javascript-node:latest`
- **Installed Tools:**
  - Node.js, npm, Angular CLI, TypeScript, Docker, PowerShell, .NET 8 SDK, Git, GitVersion
- **Default Shell:** PowerShell (`pwsh`)
- **Default Working Directory:** `/workspace`
- **How to update tool versions:** Edit the `Dockerfile` to specify desired versions.
- **Health and Security:** The workflow includes linting and vulnerability scanning for best practices. Npm cache is cleaned to avoid leaking secrets.
- **Build Automation:** The `build/` directory contains a .NET (Nuke) build project for advanced automation.

## Nuke Build Automation

This project uses [Nuke](https://nuke.build/) for advanced .NET build automation. The `build/` directory contains a Nuke build project (`Build.csproj`, `Build.cs`, etc.) that defines custom build logic for CI/CD scenarios.

### Common Nuke Targets

- `BuildAndPush` – Builds and pushes Docker images (used in CI/CD)

### Passing Parameters to Nuke

You can pass parameters to Nuke using the `--parameter value` syntax. For example:

```pwsh
./build.sh --target BuildAndPush --configuration Release
```

### Environment Variables for Nuke

- `GitHubPackagesToken` – Required for pushing images to GHCR
- Any other secrets or tokens used in your build logic should be set as environment variables or GitHub Actions secrets

### Running Nuke Locally

1. **Restore .NET tools and dependencies:**

   ```pwsh
   dotnet tool restore
   dotnet restore build/Build.csproj
   ```

2. **Run a Nuke target (e.g., BuildAndPush):**

   ```pwsh
   ./build.ps1 BuildAndPush
   # or
   ./build.sh BuildAndPush
   ```

3. **List all available Nuke targets:**

   ```pwsh
   ./build.ps1 --help
   ```

### Nuke in CI/CD

- The GitHub Actions workflow can invoke Nuke targets as part of the build process. You can customize the workflow to run specific targets (e.g., `BuildAndPush`, `Test`, `Pack`).
- The Nuke build scripts (`build.sh`, `build.ps1`, `build.cmd`) are cross-platform entry points for running the build pipeline.

### Using Predefined Nuke Scripts in Your Projects

- The `nuke` directory contains scripts and configuration for running Nuke builds for your container projects, enabling reproducible and isolated build environments.
- To use this features, you can mount your source code and invoke the Nuke build script inside a Docker container, or adapt the provided scripts for your CI/CD system.

#### Example: Run Nuke Build in Your Container Project

```pwsh
docker run --rm -it `
    -v "./:/workspace" `
    -w "/workspace" `
    ghcr.io/the-running-dev/build-agent:latest pwsh -Command "container-ci"
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
          GitHubPackagesToken: ${{ secrets.GITHUBPACKAGESTOKEN }}
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

Please see [CONTRIBUTING.md](CONTRIBUTING.md) for guidelines on how to contribute, report issues, or submit pull requests. PRs and issues are welcome!

## License

[MIT](LICENSE)

## Contact

For questions, issues, or support, please open an [issue](https://github.com/the-running-dev/Docker-BuildAgent/issues) or contact the maintainer at [ben@subzerodev.com](mailto:ben@subzerodev.com).

## Quick Start

To quickly build and run the Docker-BuildAgent locally:

```pwsh
# Clone the repository
git clone <repo-url>
cd Docker-BuildAgent

# Build the Docker image
docker build -t build-agent:latest .

# Run the container
docker run -it build-agent:latest
```

To build and push to GitHub Container Registry (GHCR):

```pwsh
$env:GitHubPackagesToken="YOUR_TOKEN"
chmod +x build.sh
./build.sh v1.2.3  # Replace v1.2.3 with your desired tag
```

## FAQ / Common Issues

- **Q: I get a permission denied error on build.sh**
  - A: Run `chmod +x build.sh` before executing the script.
- **Q: Trivy reports a secret in npm cache**
  - A: The Dockerfile now cleans npm cache after global installs to prevent this issue. If you see this, ensure you are using the latest image.
- **Q: .NET build fails outside the container**
  - A: Make sure you have the .NET 8 SDK installed locally.
- **Q: How do I pass secrets to Nuke or Docker builds?**
  - A: Use environment variables or GitHub Actions secrets. Never hardcode secrets in scripts or Dockerfiles.

## Changelog

See [Releases](https://github.com/the-running-dev/Docker-BuildAgent/releases) for a history of changes and updates.