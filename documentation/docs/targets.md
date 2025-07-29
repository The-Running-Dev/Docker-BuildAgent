---
id: targets
title: 🎯 Targets
sidebar_position: 5
---

This page describes the main build targets available in the Forge system for Docker and Node.js projects.

---

## 🛠️ Forge (Generic) Targets

- 🏗️ **Build**: Runs the main build process for the selected project type (Docker, Node, etc.), depending on your command-line arguments.
- 📝 **GenerateChangeLog**: Generates or updates the project changelog based on recent commits.

### 🗺️ Target Execution Order

This diagram shows the order in which the Forge (Generic) targets are executed:

```mermaid
flowchart TD
    Setup --> GenerateChangeLog --> Build
```

---

## 🐳 Docker Targets

- 🏗️ **Build**: Builds the Docker image, orchestrating all Docker-related steps.
- 🚀 **PublishToGitHub**: Creates a GitHub release for the built Docker image (if enabled and configured).
- 📤 **PushToRegistry**: Pushes the built Docker image(s) to the configured container registry.
- 🖼️ **BuildDockerImage**: Runs the actual Docker build command to produce the image.

### 🗺️ Target Execution Order

This diagram shows the order in which the Docker build targets are executed:

```mermaid
flowchart TD
    Setup --> BuildDockerImage --> PushToRegistry --> PublishToGitHub --> Build
```

---

## 🟩 Node Targets

- 🏗️ **Build**: Runs the full Node.js build pipeline, including environment generation, application build, and artifact copying.
- 📦 **CopyToArtifacts**: Copies the built Node.js application and related files to the artifacts directory.
- 🛠️ **BuildApplication**: Executes the Node.js build process (e.g., `npm run build`).
- 🌱 **GenerateEnvironment**: Generates the environment file from your mapping configuration, ensuring all required variables are set.
- 🧹 **Clean**: Cleans the artifacts directory and prepares the workspace for a fresh build.

### 🗺️ Target Execution Order

This diagram shows the order in which the Node.js build targets are executed:

```mermaid
flowchart TD
    Setup --> Clean --> GenerateEnvironment --> BuildApplication --> CopyToArtifacts --> Build
```

---

## 🟩🐳 NodeInDocker Targets

The NodeInDocker build combines Node.js application building with Docker image creation:

- 🏗️ **Build**: Executes the complete pipeline from Node.js build to Docker image push and GitHub release.
- 🚀 **PublishToGitHub**: Creates a GitHub release for the built Docker image (if enabled and configured).
- 📤 **PushToRegistry**: Pushes the built Docker image(s) to the configured container registry.
- 🖼️ **BuildDockerImage**: Builds the Docker image from the Node.js artifacts.
- 📦 **CopyToArtifacts**: Copies the built Node.js application to the artifacts directory.
- 🛠️ **BuildApplication**: Executes the Node.js build process.
- 🌱 **GenerateEnvironment**: Generates the environment file from your mapping configuration.
- 🧹 **Clean**: Cleans the artifacts directory and prepares the workspace for a fresh build.

### 🗺️ Target Execution Order

This diagram shows the order in which the NodeInDocker build targets are executed:

```mermaid
flowchart TD
    Setup --> Clean --> GenerateEnvironment --> BuildApplication --> CopyToArtifacts --> BuildDockerImage --> PushToRegistry --> PublishToGitHub --> Build
```