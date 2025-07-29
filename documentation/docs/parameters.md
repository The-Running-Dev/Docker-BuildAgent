---
id: parameters
title: ⚙️ Parameters
sidebar_position: 3
---

import Tabs from '@theme/Tabs';
import TabItem from '@theme/TabItem';

This document lists all available settings for Forge, Docker, and Node.js build processes. All parameters can be passed to the build system as **NUKE parameters** (command-line arguments, e.g. `--param value`). These can also be set via environment variables or in the `.build.env.map` configuration file, depending on your CI/CD setup.

---

## 🛠️ Forge (Common Settings)

These are the base parameters available to all builds. **NodeParams** and **DockerParams** both inherit from ForgeParams, so all settings below are available to Node and Docker builds as well.

<Tabs>
<TabItem value="env" label="Environment" default>
| Variable                   | Description                                       | Default        |
|----------------------------|---------------------------------------------------|----------------|
| `RepositoryUrl`            | The URL of the source code repository.            |                |
| `Notifications`            | Enable/disable notifications (bool).              | false          |
| `ForceNotifications`       | Force notifications even if not required (bool).  | false          |
| `NotificationsWebHookUrl`  | Webhook URL for sending notifications.            |                |
| `ForcePush`                | Force push actions (bool).                        | false          |
| `DryRun`                   | Run the build in dry-run mode (bool).             | false          |
| `Verbosity`                | Verbosity level for build output (enum).          | Normal         |
</TabItem>
<TabItem value="nuke" label="NUKE Parameters">
| Variable                         | Description                                       | Default        |
|----------------------------------|---------------------------------------------------|----------------|
| `--repository-url`               | The URL of the source code repository.            |                |
| `--notifications`                | Enable/disable notifications (bool).              | false          |
| `--force-notifications`          | Force notifications even if not required (bool).  | false          |
| `--notifications-web-hook-url`   | Webhook URL for sending notifications.            |                |
| `--force-push`                   | Force push actions (bool).                        | false          |
| `--dry-run`                      | Run the build in dry-run mode (bool).             | false          |
| `--verbosity`                    | Verbosity level for build output (enum).          | Normal         |
</TabItem>
</Tabs>

---

## 🐳 Docker

**Inherits all settings from Forge.**

<Tabs>
<TabItem value="env" label="Environment" default>
| Variable              | Description                                             | Default        |
|-----------------------|---------------------------------------------------------|----------------|
| `TemplatesDir`        | Directory path for Dockerfile templates.                | /nuke/templates|
| `Dockerfile`          | Path to the Dockerfile.                                 | Dockerfile     |
| `ImageTag`            | Tag for the container image.                            | container-app  |
| `RegistryUrl`         | Registry URL for pushing the Docker image.              |                |
| `RegistryUser`        | Registry user name.                                     |                |
| `RegistryToken`       | Registry authentication token.                          |                |
| `CreateGitHubRelease` | Whether to create a GitHub release after build (bool).  | false          |
</TabItem>
<TabItem value="nuke" label="NUKE Parameters">
| Parameter                 | Description                                             | Default        |
|---------------------------|---------------------------------------------------------|----------------|
| `--templates-dir`         | Directory path for Dockerfile templates.                | /nuke/templates|
| `--docker-file`           | Path to the Dockerfile.                                 | Dockerfile     |
| `--image-tag`             | Tag for the container image.                            | container-app  |
| `--registry-url`          | Registry URL for pushing the Docker image.              |                |
| `--registry-user`         | Registry user name.                                     |                |
| `--registry-token`        | Registry authentication token.                          |                |
| `--create-github-release` | Whether to create a GitHub release after build (bool).  | false          |
</TabItem>
</Tabs>

---

## 🔄 NodeInDocker

**Inherits all settings from both Docker and Node.js (and therefore Forge).**

The NodeInDocker build type combines parameters from both Docker and Node.js builds, giving you access to all configuration options from both parent types. This enables comprehensive control over the two-phase build process.

<Tabs>
<TabItem value="env" label="Environment" default>
| Variable              | Description                                             | Default        | Source |
|-----------------------|---------------------------------------------------------|----------------|--------|
| `ArtifactsDir`        | Directory path for storing Node.js build artifacts.     | artifacts      | Node   |
| `TemplatesDir`        | Directory path for Dockerfile templates.                | /nuke/templates| Docker |
| `Dockerfile`          | Path to the Dockerfile.                                 | Dockerfile     | Docker |
| `ImageTag`            | Tag for the container image.                            | container-app  | Docker |
| `RegistryUrl`         | Registry URL for pushing the Docker image.              |                | Docker |
| `RegistryUser`        | Registry user name.                                     |                | Docker |
| `RegistryToken`       | Registry authentication token.                          |                | Docker |
| `CreateGitHubRelease` | Whether to create a GitHub release after build (bool).  | false          | Docker |
</TabItem>
<TabItem value="nuke" label="NUKE Parameters">
| Parameter                 | Description                                             | Default        | Source |
|---------------------------|---------------------------------------------------------|----------------|--------|
| `--artifacts-dir`         | Directory path for storing Node.js build artifacts.     | artifacts      | Node   |
| `--templates-dir`         | Directory path for Dockerfile templates.                | /nuke/templates| Docker |
| `--docker-file`           | Path to the Dockerfile.                                 | Dockerfile     | Docker |
| `--image-tag`             | Tag for the container image.                            | container-app  | Docker |
| `--registry-url`          | Registry URL for pushing the Docker image.              |                | Docker |
| `--registry-user`         | Registry user name.                                     |                | Docker |
| `--registry-token`        | Registry authentication token.                          |                | Docker |
| `--create-github-release` | Whether to create a GitHub release after build (bool).  | false          | Docker |
</TabItem>
</Tabs>

**Note**: NodeInDocker also inherits all Forge (common) parameters shown above, including notifications, verbosity, dry-run mode, and force-push options.

---


## 🟩 Node.js

**Inherits all settings from Forge.**

<Tabs>
<TabItem value="env" label="Environment" default>
| Variable         | Description                                         | Default     |
|------------------|-----------------------------------------------------|-------------|
| `ArtifactsDir`   | Directory path for storing build artifacts.         | artifacts   |
</TabItem>
<TabItem value="nuke" label="NUKE Parameters">
| Parameter         | Description                                         | Default     |
|-------------------|-----------------------------------------------------|-------------|
| `--artifacts-dir` | Directory path for storing build artifacts.         | artifacts   |
</TabItem>
</Tabs>

## Example

Tell the build process to create a GitHub release (`false` by default):

Create `.build.env.map`, with:

```env
CreateGitHubRelease=const:true
```

Use a NUKE parameter:

```pwsh
& docker run `
     -e DOCKER_HOST=tcp://host.docker.internal:2375 `
     -v ./:/workspace `
     -it ghcr.io/the-running-dev/build-agent:latest `
     docker-build --create-github-release true
```
