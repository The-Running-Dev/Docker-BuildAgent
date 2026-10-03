---
id: advanced
title: Advanced
sidebar_position: 9
---

You can run any tool in the image for a reproducible environment, and you can run the `build` command with
selected targets. A `build docker` run builds an image, so it needs the host's Docker socket mounted into the
container; without it the Docker CLI inside the image has no daemon to talk to and the build fails.

```bash
docker run --rm -it \
    -v "${PWD}:/workspace" \
    -v /var/run/docker.sock:/var/run/docker.sock \
    -w "/workspace" \
    ghcr.io/the-running-dev/build-agent:latest build docker
```

## Tools Available in Docker-BuildAgent

The image comes with these tools:

- Node.js and npm
- Angular CLI (`ng`)
- TypeScript (`tsc`) and `tsx`
- .NET SDKs 8, 9 and 10 (`dotnet`)
- Docker CLI (`docker`), with the buildx and compose plugins
- PowerShell (`pwsh`)
- Git
- GitVersion
- Nuke global tool (`nuke`)
- angular-cli-ghpages

Run a tool by giving its command in place of `build`:

```bash
docker run --rm -it \
    -v "${PWD}:/workspace" \
    -w "/workspace" \
    ghcr.io/the-running-dev/build-agent:latest <your command>
```

### Example: Run Angular CLI

```bash
docker run --rm -it -v "${PWD}:/workspace" ghcr.io/the-running-dev/build-agent:latest \
    pwsh -Command "ng build --configuration production"
```

### Example: Use GitVersion to get the semantic version

```bash
docker run --rm -it -v "${PWD}:/workspace" ghcr.io/the-running-dev/build-agent:latest \
    pwsh -Command "gitversion /output json"
```

### Example: Run part of a build

Pass `--target` to run one target and the targets before it. This builds the image and stops before the push:

```bash
docker run --rm -it \
    -v /var/run/docker.sock:/var/run/docker.sock \
    -v "${PWD}:/workspace" \
    ghcr.io/the-running-dev/build-agent:latest \
    build docker --target BuildDockerImage
```

The targets of each build type are in [Targets](./targets.md).

### Example: Build and push a Docker image yourself

```bash
docker run --rm -it \
    -v /var/run/docker.sock:/var/run/docker.sock \
    -v "${PWD}:/workspace" \
    ghcr.io/the-running-dev/build-agent:latest \
    pwsh -Command "docker build -t my-app:latest . && docker push my-app:latest"
```

### Example: Compile TypeScript

```bash
docker run --rm -it -v "${PWD}:/workspace" ghcr.io/the-running-dev/build-agent:latest \
    pwsh -Command "tsc src/index.ts --outDir dist"
```

Canonical contract (build command): [design/20-contract.md](https://github.com/The-Running-Dev/Docker-BuildAgent/blob/main/design/20-contract.md)
