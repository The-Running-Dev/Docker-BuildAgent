# Docker-BuildAgent

![Build Status](https://img.shields.io/github/actions/workflow/status/the-running-dev/Docker-BuildAgent/build-and-push.yml?branch=main)
![License](https://img.shields.io/github/license/the-running-dev/Docker-BuildAgent)

---

## Table of Contents

- [Docker-BuildAgent](#docker-buildagent)
  - [Table of Contents](#table-of-contents)
  - [Overview](#overview)
  - [Features](#features)
  - [Prerequisites](#prerequisites)
  - [Usage](#usage)
    - [Building and Running Locally](#building-and-running-locally)
    - [Build and Push to GitHub Container Registry](#build-and-push-to-github-container-registry)
    - [GitHub Actions CI/CD](#github-actions-cicd)
      - [Workflow Steps](#workflow-steps)
  - [Environment Variables](#environment-variables)
  - [Customization](#customization)
  - [Troubleshooting](#troubleshooting)
  - [Image Details](#image-details)
  - [Security Notes](#security-notes)
  - [Related Resources](#related-resources)
  - [Contributing](#contributing)
  - [License](#license)
  - [Contact](#contact)

---

## Overview

Docker-BuildAgent is a pre-configured Docker image designed to serve as a build agent for CI/CD pipelines. It comes with Node.js, Angular CLI, TypeScript, Docker, and PowerShell pre-installed, making it ideal for building, testing, and deploying JavaScript/TypeScript and Angular applications in automated environments.

## Features

- **Node.js** and **npm** for JavaScript/TypeScript development
- **Angular CLI** and **TypeScript** for Angular projects
- **Docker** for containerized builds and deployments
- **PowerShell** for advanced scripting
- Ready-to-use in CI/CD pipelines (e.g., GitHub Actions)

## Prerequisites

- [Docker](https://www.docker.com/get-started) installed on your machine (for local builds)
- Access to [GitHub Container Registry (GHCR)](https://ghcr.io/)
- A valid GitHub Packages token with permissions to push to GHCR

## Usage

### Building and Running Locally

1. **Clone the repository:**

   ```sh
   git clone <repo-url>
   cd Docker-BuildAgent
   ```

2. **Build the Docker image:**

   ```sh
   docker build -t build-agent .
   ```

3. **Run the container:**

   ```sh
   docker run -it build-agent
   ```

### Build and Push to GitHub Container Registry

You can use the provided `build.sh` script to build and push the image to GHCR.

1. **Set the required environment variable:**
   - `gitHubPackagesToken`: Your GitHub Packages token (with write:packages scope)

2. **Run the build script:**

   ```sh
   export gitHubPackagesToken=YOUR_TOKEN
   chmod +x build.sh
   ./build.sh
   ```

### GitHub Actions CI/CD

The `.github/workflows/build-and-push.yml` workflow automates building and pushing the Docker image on every push to the `main` branch or via manual dispatch. It requires the `GITHUBPACKAGESTOKEN` secret to be set in your repository.

#### Workflow Steps

- Checks out the repository
- Sets up Docker Buildx
- Grants execute permission to `build.sh`
- Runs `build.sh` to build and push the image to GHCR

## Environment Variables

- `gitHubPackagesToken`: GitHub Packages token used for authenticating with GHCR (required for pushing images)

## Customization

- Modify the `Dockerfile` to add or remove tools as needed for your build environment.
- Update `build.sh` for custom tagging or registry targets.
- To change the base image, edit the `FROM` line in the `Dockerfile`.
- To install additional global npm packages, add them to the `npm install -g` command in the `Dockerfile`.

## Troubleshooting

- **Docker login/authentication errors:**
  - Ensure your `gitHubPackagesToken` is valid and has the correct permissions (`write:packages`).
  - Use GitHub Actions secrets for sensitive values.
- **Permission denied on build.sh:**
  - Run `chmod +x build.sh` before executing the script.
- **Image push fails:**
  - Check your network connection and GHCR access rights.

## Image Details

- **Base Image:** `mcr.microsoft.com/devcontainers/javascript-node:latest`
- **Installed Tools:**
  - Node.js, npm, Angular CLI, TypeScript, Docker, PowerShell
- **Default Shell:** PowerShell (`pwsh`)
- **Default Working Directory:** `/workspace`
- **How to update tool versions:** Edit the `Dockerfile` to specify desired versions.

## Security Notes

- **Never expose your GitHub token in logs or public repositories.**
- Always use GitHub Actions secrets or environment variables for sensitive data.
- Review Dockerfile and scripts for any hardcoded credentials before sharing.

## Related Resources

- [Docker Documentation](https://docs.docker.com/)
- [GitHub Actions Documentation](https://docs.github.com/en/actions)
- [GitHub Container Registry](https://docs.github.com/en/packages/working-with-a-github-packages-registry/working-with-the-container-registry)
- [Node.js](https://nodejs.org/)
- [Angular CLI](https://angular.io/cli)
- [PowerShell](https://docs.microsoft.com/powershell/)

## Contributing

Contributions are welcome! Please open issues or submit pull requests for improvements or bug fixes.

## License

[MIT](LICENSE)

## Contact

For questions, issues, or support, please open an [issue](https://github.com/the-running-dev/Docker-BuildAgent/issues) or contact the maintainer at [ben@subzerodev.com](mailto:ben@subzerodev.com).
