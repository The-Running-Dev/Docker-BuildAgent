---
id: development
title: "🛠️ Development"
sidebar_position: 8
---

This section explains how to set up Docker-BuildAgent and the Forge build orchestrator for local development and CI/CD pipelines.

## Prerequisites

- [Docker](https://www.docker.com/get-started) installed on your machine (for local builds)
- Access to [GitHub Container Registry (GHCR)](https://ghcr.io/)
- A valid GitHub Packages token with permissions to push to GHCR
- [.NET 8 SDK](https://dotnet.microsoft.com/en-us/download/dotnet/8.0) (for local .NET builds)

## Clone the Repository

```pwsh
git clone https://github.com/the-running-dev/Docker-BuildAgent
cd Docker-BuildAgent
```

## Build the Docker Image

```pwsh
docker build -t build-agent:latest .
```

## Run the Container

```pwsh
docker run -it build-agent:latest
```

## Using Forge for Multi-Project Builds

The main entry point for builds is via the provided scripts (e.g., `build.ps1`). You can specify the build type using the `-type` argument:

```pwsh
./build.ps1 -type docker   # For Docker builds
./build.ps1 -type node     # For Node.js builds
```

You can extend Forge to support new build types by adding logic to the Forge project.

## Directory Layout

```pwsh
/.build
├── .app.env.map         # Maps application env vars
├── .build.scripts       # List of commands (e.g. npm, ps1, bash)
├── .build.copy          # Files/folders to copy to artifacts/
├── .build.env.map       # Maps build env vars like DiscordWebHookUrl
/artifacts/              # Final build output ends up here
/Docs/                   # Docusaurus documentation
/Forge/                  # Shared NUKE build logic
├──/Docker/              # Docker-specific targets
├──/Node/                # Node.js-specific targets
./Dockerfile             # Containerize your build
./build.ps1              # Build entry point
```