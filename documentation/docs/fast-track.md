---
id: fast-track
title: Fast Track
sidebar_position: 1
---

## Prerequisites

- **Docker**, running. Every build runs in the image. [What each build needs](build-types#what-each-build-needs) says which builds also need the Docker host, and which project files and tokens each one uses.

## Choose a build

[Choose a build](build-types#choose-a-build) lists the five build types and the project each one suits. The examples below cover the common ones.

## Quick Start Examples

Docker-BuildAgent has one `build` command with several types. These are the most common scenarios:

Not sure which type to use? See [Build Types](build-types) for the comparison, the parameters and the details.

### Docker Image Build

Creates a Docker image from your project; the project directory is the Docker build context.

1. Map your project directory (`./`) to `/workspace`
2. Expose the Docker host to the container, either through a `docker.sock` volume bind (on Linux) or the `DOCKER_HOST` environment variable.
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

This runs the `docker` build type with all its [targets](targets#docker) and default [parameters](parameters#docker), and builds your Docker image.

### Node.js Application Build

1. Map your project directory (`./`) to `/workspace`
2. Define a `build:prod` npm script inside your `package.json`
3. Execute `build node`

```pwsh
& docker run `
    -v ./:/workspace `
    -it ghcr.io/the-running-dev/build-agent:latest `
    build node
```

This runs the `node` build type with all its [targets](targets#node) and default [parameters](parameters#node), and builds your Node application.

By default, the `node` build runs three scripts, using the package manager it detects (`npm` below):

1. `rm -rf node_modules`
2. `npm install`
3. `npm run build:prod`

To change this, provide your own `.build/.build.scripts`; see [customization](customization).

### Node.js + Docker Combined Build

1. Map your project directory (`./`) to `/workspace`
2. Expose the Docker host to the container, either through a `docker.sock` volume bind (on Linux) or the `DOCKER_HOST` environment variable.
3. Define a `build:prod` npm script inside your `package.json`
4. Execute `build node-in-docker`

```pwsh
& docker run `
    -e DOCKER_HOST=tcp://host.docker.internal:2375 `
    -v ./:/workspace `
    -it ghcr.io/the-running-dev/build-agent:latest `
    build node-in-docker
```

This runs the `node` build type with all its [targets](targets#node) and default [parameters](parameters#node), and builds your Node application. It then runs the `docker` build type with all its [targets](targets#docker) and default [parameters](parameters#docker), and builds your Docker image.

### Changelog Generation

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

This generates a formatted changelog from the Git commit history and saves it to `CHANGELOG.md`. Dates use the format `yyyy.MM.dd`, and commits are grouped by date, newest first.

## Learn More

These examples show the common cases. For every build type, parameter and advanced scenario:

- [Build Types](build-types): all five build types
- [Parameters](parameters): every parameter
- [Customization](customization): advanced configuration

Canonical contract (build command): [design/20-contract.md](https://github.com/The-Running-Dev/Docker-BuildAgent/blob/main/design/20-contract.md)
