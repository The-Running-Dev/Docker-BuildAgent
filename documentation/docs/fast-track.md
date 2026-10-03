---
id: fast-track
title: 🚀 Fast Track
sidebar_position: 1
---

Canonical contract (build command): [design/20-contract.md](https://github.com/The-Running-Dev/Docker-BuildAgent/blob/main/design/20-contract.md)

## Choose a build

| You have | Run | You get |
|---|---|---|
| A project with a `Dockerfile`, or a type that has a [template](docker-templates) | `build docker` | An image, pushed in CI (and released with `--create-github-release true`) |
| A Node.js application that is not containerized | `build node` | The built application in the artifacts directory |
| A Node.js application that ships as an image | `build node-in-docker` | The Node build, then the image |
| A documentation site | `build node-template` | The site built from a template repository |
| A Git history | `build forge` | `CHANGELOG.md` |

## What each build needs

| Build | Docker host | Your project provides | To push or release in CI |
|---|---|---|---|
| `build docker` | Yes: mount `docker.sock` or set `DOCKER_HOST` | A `Dockerfile`, or none when a template matches | `RegistryToken` and `GITHUB_TOKEN` |
| `build node` | No | A `build:prod` npm script, or `.build/.build.scripts` | Not applicable |
| `build node-in-docker` | Yes | The same as `build node` | `RegistryToken` and `GITHUB_TOKEN` |
| `build node-template` | No | A documentation directory; the template comes from a repository | Not applicable |
| `build forge` | No | The full Git history (`fetch-depth: 0` in GitHub Actions) and a `.build/` directory (the build stops if it is missing) | Not applicable |

Every build mounts the project at `/workspace`.

## Quick Start Examples

The Build Agent uses a unified `build` command with different types. Here are the most common scenarios to get you started quickly:

> 💡 **Need help choosing?** Check out our comprehensive [Build Types Reference](build-types) for detailed comparisons, parameters, and decision guidance.

### 🐳 Docker Image Build

Creates a Docker image from your project; the project directory is the Docker build context.

1. Map your project directory (`./`) to `/workspace`
2. Expose the Docker host to the container, either through docker.sock volume bind (on Linux) or DOCKER_HOST environment variable.
3. Optional: provide a Dockerfile in your project directory, or use a [Docker Template](docker-templates) automatically.
4. Execute `build docker`

```bash
# Expose Docker host with volume bind
docker run \
     -v /var/run/docker.sock:/var/run/docker.sock \
     -v ./:/workspace \
     -it ghcr.io/the-running-dev/build-agent:latest \
     build docker
```

```pwsh
# Expose Docker host with environment variable
& docker run `
     -e DOCKER_HOST=tcp://host.docker.internal:2375 `
     -v ./:/workspace `
     -it ghcr.io/the-running-dev/build-agent:latest `
     build docker
```

This will run the `Docker` forge with all its [targets](targets#docker) and default [parameters](parameters#docker), and build your Docker image.

### 🟢 Node.js Application Build

1. Map your project directory (`./`) to `/workspace`
2. Define a `build:prod` npm script inside your `package.json`
3. Execute `build node`

```pwsh
& docker run `
    -v ./:/workspace `
    -it ghcr.io/the-running-dev/build-agent:latest `
    build node
```

This will run the `Node` forge with all its [targets](targets#node) and default [parameters](parameters#node), and build your Node application.

By default, the `Node` build target runs 3 scripts, using the package manager it detects (`npm` below):

1. `rm -rf node_modules`
2. `npm install`
3. `npm run build:prod`

You can customize this by specifying your own `.build.scripts`, see [customization](customization).

### 🟢 🐳 Node.js + Docker Combined Build

1. Map your project directory (`./`) to `/workspace`
2. Expose the Docker host to the container, either through docker.sock volume bind (on Linux) or DOCKER_HOST environment variable.
3. Define a `build:prod` npm script inside your `package.json`
4. Execute `build node-in-docker`

```pwsh
& docker run `
    -e DOCKER_HOST=tcp://host.docker.internal:2375 `
    -v ./:/workspace `
    -it ghcr.io/the-running-dev/build-agent:latest `
    build node-in-docker
```

This will run the `Node` forge with all its [targets](targets#node) and default [parameters](parameters#node), and build your Node application. And after that, it will run the `Docker` forge with all its [targets](targets#docker) and default [parameters](parameters#docker), and build your Docker image.

### 📝 Changelog Generation

1. Map your project directory (`./`) to `/workspace`
2. Execute `build forge` with the changelog options

```pwsh
# Generate changelog since last tag (default)
& docker run `
    -v ./:/workspace `
    -it ghcr.io/the-running-dev/build-agent:latest `
    build forge --target GenerateChangeLog

# Generate complete commit history
& docker run `
    -v ./:/workspace `
    -it ghcr.io/the-running-dev/build-agent:latest `
    build forge --change-log-source all
```

This will generate a formatted changelog from Git commit history and save it to `CHANGELOG.md`. The changelog uses the format `yyyy.MM.dd` for dates and groups commits by date in descending order.

## 📚 Learn More

These examples show the most common use cases. For complete information about all build types, parameters, and advanced scenarios:

- **[Build Types Reference](build-types)** - Comprehensive guide to all 5 build commands
- **[Parameters](parameters)** - Detailed parameter documentation
- **[Customization](customization)** - Advanced configuration options
