---
id: fast-track
title: 🚀 Fast Track
sidebar_position: 1
---

## Quick Start Examples

The Build Agent supports 5 different build types. Here are the most common scenarios to get you started quickly:

> 💡 **Need help choosing?** Check out our comprehensive [Build Types Reference](build-types) for detailed comparisons, parameters, and decision guidance.

### 🐳 Docker Image Build

Creates a Docker image for your project artifacts (from the default `ArtifactsDir`).

1. Map your project directory (`./`) to `/workspace`
2. Expose the Docker host to the container, either through docker.sock volume bind (on Linux) or DOCKER_HOST environment variable.
3. Optional: provide a Dockerfile in your project directory, or use a [Docker Template](docker-templates) automatically.
4. Execute `docker-build`

```bash
# Expose Docker host with volume bind
docker run `
     -v /var/run/docker.sock:/var/run/docker.sock \
     -v ./:/workspace \
     -it ghcr.io/the-running-dev/build-agent:latest \
     docker-build
```

```pwsh
# Expose Docker host with environment variable
& docker run `
     -e DOCKER_HOST=tcp://host.docker.internal:2375 `
     -v ./:/workspace `
     -it ghcr.io/the-running-dev/build-agent:latest `
     docker-build
```

This will run the `Docker` forge with all it's [targets](targets#-docker-targets) and default [parameters](parameters#-docker), and build your Docker image.

### 🟢 Node.js Application Build

1. Map your project directory (`./`) to `/workspace`
2. Define a `build:prod` npm script inside your `package.json`
3. Execute `node-build`
 
```pwsh
& docker run `
    -v ./:/workspace `
    -it ghcr.io/the-running-dev/build-agent:latest `
    node-build
```

This will run the `Node` forge with all it's [targets](targets#-node-targets) and default [parameters](parameters#-node), and build your Node application.

By default, the `Node` build target runs 2 scripts:

1. npm install
2. npm run build:prod

You can customize this by specifying your own `.build.scripts`, see [customization](customization).

### 🟢 🐳 Node.js + Docker Combined Build

1. Map your project directory (`./`) to `/workspace`
2. Expose the Docker host to the container, either through docker.sock volume bind (on Linux) or DOCKER_HOST environment variable.
3. Define a `build:prod` npm script inside your `package.json`
4. Execute `node-in-docker-build`

```pwsh
& docker run `
    -e DOCKER_HOST=tcp://host.docker.internal:2375 `
    -v ./:/workspace
    -it ghcr.io/the-running-dev/build-agent:latest `
    node-in-docker-build
```

This will run the `Node` forge with all it's [targets](targets#-node-targets) and default [parameters](parameters#-node), and build your Node application. And after that, it will run the `Docker` forge with all it's [targets](targets#-docker-targets) and default [parameters](parameters#-docker), and build your Docker image.

### 📝 Changelog Generation

1. Map your project directory (`./`) to `/workspace`
2. Execute `forge` with the `GenerateChangeLog` target

```pwsh
# Generate changelog since last tag (default)
& docker run `
    -v ./:/workspace `
    -it ghcr.io/the-running-dev/build-agent:latest `
    forge --target GenerateChangeLog

# Generate complete commit history
& docker run `
    -v ./:/workspace `
    -it ghcr.io/the-running-dev/build-agent:latest `
    forge --target GenerateChangeLog --change-log-source all
```

This will generate a formatted changelog from Git commit history and save it to `CHANGELOG.md`. The changelog uses the format `yyyy.MM.dd` for dates and groups commits by date in descending order.

## 📚 Learn More

These examples show the most common use cases. For complete information about all build types, parameters, and advanced scenarios:

- **[Build Types Reference](build-types)** - Comprehensive guide to all 5 build commands
- **[Parameters](parameters)** - Detailed parameter documentation  
- **[Customization](customization)** - Advanced configuration options
