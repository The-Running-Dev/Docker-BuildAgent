---
id: usage
title: 🚀 Fast Track
sidebar_position: 1
---

### 🐳 Docker Image

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

### 🟢 Node.js App

1. Map your project directory (`./`) to `/workspace`
2. Define a `build:prod` npm script inside your `package.json`
3. Execute `node-build`
 
```pwsh
& docker run `
    -v ./:/workspace `
    -it ghcr.io/the-running-dev/build-agent:latest `
    node-build
```

This will run the `Node` forge with all it's [targets](targets#-node-targets) and default [parameters](parameters#-nodejs), and build your Node application.

By default, the `Node` build target runs 2 scripts:

1. npm install
2. npm run build:prod

You can customize this by specifying your own `.build.scripts`, see [customization](customization).

### 🟢 🐳 Node.js App, Inside a Docker Image

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

This will run the `Node` forge with all it's [targets](targets#-node-targets) and default [parameters](parameters#-nodejs), and build your Node application. And after that, it will run the `Docker` forge with all it's [targets](targets#-docker-targets) and default [parameters](parameters#-docker), and build your Docker image.