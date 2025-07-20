---
id: ci-cd
title: "🔄 GitHub Actions"
sidebar_position: 8
---

## 🐳 Docker Image

This workflow builds and pushes a Docker image using the Build Agent. It checks out your repository, runs the `docker-build` target, and passes required secrets for authentication.

```yaml
name: Docker-CI
on:
  workflow_dispatch:
  push:
    branches:
      - main

jobs:
  Docker-CI:
    runs-on: ubuntu-latest
    container:
      image: ghcr.io/the-running-dev/build-agent:latest
    steps:
      - name: Checkout Repository
        uses: actions/checkout@v4
        with:
          fetch-depth: 0

      - name: Docker CI
        run: docker-build
        env:
          GITHUB_TOKEN: ${{ secrets.GITHUB_TOKEN }}
          RegistryToken: ${{ secrets.GITHUBPACKAGESTOKEN }}
```

## 🟢 Node.js App

This workflow builds a Node.js application using the Build Agent. It checks out your repository, runs the `node-build` target, and passes required secrets for authentication.

```yaml
name: Node-CI
on:
  workflow_dispatch:
  push:
    branches:
      - main

jobs:
  Node-CI:
    runs-on: ubuntu-latest
    container:
      image: ghcr.io/the-running-dev/build-agent:latest
    steps:
      - name: Checkout Repository
        uses: actions/checkout@v4
        with:
          fetch-depth: 0

      - name: Node CI
        run: node-build
        env:
          GITHUB_TOKEN: ${{ secrets.GITHUB_TOKEN }}
          RegistryToken: ${{ secrets.GITHUBPACKAGESTOKEN }}
```

## 🟢 🐳 Node.js App in a Docker Image

```yaml
name: Node-in-Docker-CI
on:
  workflow_dispatch:
  push:
    branches:
      - main

jobs:
  Node-in-Docker-CI:
    runs-on: ubuntu-latest
    container:
      image: ghcr.io/the-running-dev/build-agent:latest
    steps:
      - name: Checkout Repository
        uses: actions/checkout@v4
        with:
          fetch-depth: 0

      - name: Node-in-Docker CI
        run: node-in-docker-build
        env:
          GITHUB_TOKEN: ${{ secrets.GITHUB_TOKEN }}
          RegistryToken: ${{ secrets.GITHUBPACKAGESTOKEN }}
```

## 🛠️ Custom Build

Because the build agent has all the tooling, you can run any Bash/PowerShell/NPM/Angular CLI scripts.

```yaml
name: Custom-CI
on:
  workflow_dispatch:
  push:
    branches:
      - main

jobs:
  Custom-CI:
    runs-on: ubuntu-latest
    container:
      image: ghcr.io/the-running-dev/build-agent:latest
    steps:
      - name: Checkout Repository
        uses: actions/checkout@v4
        with:
          fetch-depth: 0

      - name: Build and Push
        run: pwsh ./my-custom-build.ps1
        env:
          GIT_USER: Some-Value
          GIT_PASS: ${{ secrets.GITHUB_TOKEN }}
```