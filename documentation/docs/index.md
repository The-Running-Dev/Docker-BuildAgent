---
id: index
title: Introduction
sidebar_position: 0
---

Docker-BuildAgent is a build image built on [NUKE](https://nuke.build/). It standardizes and simplifies the CI/CD process for:

- Docker image builds, with tagging and a registry push
- Node.js applications (Express, Angular, React and others)
- Build artifacts and change logs

## Features

- Reusable build logic as NUKE targets
- Build and application environment files generated from pipeline secrets
- Automatic detection of the Node.js package manager
- Discord notifications
- GitHub releases, with a controlled release strategy
- Orchestration by convention, or through `.build/.build.scripts` and `.build/.build.copy`
- A dry-run mode for safe testing
- Change log generation from the Git history, with customizable formatting
- The Forge build system, which provides five build types (`docker`, `node`, `node-in-docker`, `node-template`, `forge`)
- An optional [project configuration file](project-configuration.md), a [PowerShell module](powershell-module.md) and an [update and rollback tool](update-tool.md)

The tools in the image (Node.js, the Angular CLI, PowerShell, the .NET SDKs, Git and GitVersion) are listed in [What the image contains](https://github.com/The-Running-Dev/Docker-BuildAgent/blob/main/README.md#what-the-image-contains).

## Fast Track

To get started, follow the [Fast Track guide](fast-track.md). The [Build Types reference](build-types.md) describes every build command, and [Customization](customization.md) covers advanced configuration.

## Architecture and Development

For developers and advanced users:

- [Dependency Injection](architecture/dependency-injection.md): the service architecture and how to test it
- [Development Guide](architecture/development-guide.md): setup and the contribution workflow
- [Configuration and Compatibility](architecture/configuration-compatibility.md): environment setup and platform compatibility
