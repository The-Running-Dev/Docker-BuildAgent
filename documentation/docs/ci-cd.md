---
id: ci-cd
title: "🔄 GitHub Actions"
sidebar_position: 8
---

## 🚀 Release Strategy

The Build Agent project uses a **controlled release strategy** to distinguish between development builds and official releases:

### Development Workflow

- **Push to main** → Triggers "Deploy" workflow → Builds and publishes Docker images (no GitHub releases)
- **Pull requests** → Triggers "CI" workflow → Validation and testing only

### Release Workflow

- **Manual releases**: Use "Create Release" workflow in GitHub Actions
- **Tag-based releases**: Push a version tag (e.g., `git tag v1.2.3 && git push origin v1.2.3`)

### Pre-releases

Tags with suffixes like `v1.0.0-beta.1` or `v1.0.0-rc.1` are automatically marked as pre-releases.

---

## 📦 Creating Official Releases

### Option 1: Manual Release (Recommended)

1. Go to **Actions** tab in your GitHub repository
2. Select **"Create Release"** workflow
3. Click **"Run workflow"**
4. Optionally specify:
   - Custom version (e.g., `v1.2.3`)
   - Mark as pre-release
   - Custom release notes
5. Click **"Run workflow"** button

### Option 2: Tag-Based Release

```bash
# Create and push a version tag
git tag v1.2.3
git push origin v1.2.3

# For pre-releases
git tag v1.0.0-beta.1
git push origin v1.0.0-beta.1
```

---

## 📦 Example Workflows

### Create Release Workflow

```yaml
name: Create Release
on:
  workflow_dispatch:
    inputs:
      version:
        description: 'Release version (optional, leave empty for auto-version)'
        required: false
        type: string
      prerelease:
        description: 'Mark as pre-release'
        required: false
        type: boolean
        default: false

permissions:
  packages: write
  contents: write

jobs:
  create-release:
    name: Create Release
    runs-on: ubuntu-latest
    
    steps:
      - name: Checkout Repository
        uses: actions/checkout@v4
        with:
          fetch-depth: 0

      - name: Setup Build Environment
        uses: ./.github/actions/common

      - name: Run Tests & Generate Coverage
        uses: ./.github/actions/tests

      - name: Build and Create Release
        run: |
          if [ -n "${{ github.event.inputs.version }}" ]; then
            nuke --type docker --create-github-release true --version "${{ github.event.inputs.version }}" --pre-release "${{ github.event.inputs.prerelease }}"
          else
            nuke --type docker --create-github-release true --pre-release "${{ github.event.inputs.prerelease }}"
          fi
        env:
          GITHUB_TOKEN: ${{ secrets.GITHUB_TOKEN }}
          RegistryToken: ${{ secrets.REGISTRY_TOKEN }}
```

### Deploy Workflow (Continuous Deployment)

```yaml
name: Deploy
on:
  push:
    branches:
      - main
    paths-ignore:
      - 'documentation/**'

permissions:
  packages: write
  contents: write

jobs:
  deploy:
    name: Build & Deploy
    runs-on: ubuntu-latest

    steps:
      - name: Checkout Repository
        uses: actions/checkout@v4
        with:
          fetch-depth: 0

      - name: Setup Build Environment
        uses: ./.github/actions/common

      - name: Run Tests & Generate Coverage
        uses: ./.github/actions/tests

      - name: Build and Publish to Registry
        run: nuke --type docker --create-github-release false
        env:
          GITHUB_TOKEN: ${{ secrets.GITHUB_TOKEN }}
          RegistryToken: ${{ secrets.REGISTRY_TOKEN }}
```

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

## � Changelog Generation

This workflow generates a changelog from Git commit history using the Forge build system. It can be configured to generate complete history or changes since a specific tag.

```yaml
name: Changelog-Generation
on:
  workflow_dispatch:
    inputs:
      changelog_source:
        description: 'Changelog source (all, tag name, or leave empty for since last tag)'
        required: false
        default: ''
        type: string
  push:
    branches:
      - main

jobs:
  Generate-Changelog:
    runs-on: ubuntu-latest
    container:
      image: ghcr.io/the-running-dev/build-agent:latest
    steps:
      - name: Checkout Repository
        uses: actions/checkout@v4
        with:
          fetch-depth: 0

      - name: Generate Changelog
        run: |
          if [ -n "${{ github.event.inputs.changelog_source }}" ]; then
            forge --target GenerateChangeLog --change-log-source "${{ github.event.inputs.changelog_source }}"
          else
            forge --target GenerateChangeLog
          fi
        env:
          GITHUB_TOKEN: ${{ secrets.GITHUB_TOKEN }}

      - name: Commit Changelog
        run: |
          git config --global user.name "github-actions[bot]"
          git config --global user.email "github-actions[bot]@users.noreply.github.com"
          git add CHANGELOG.md
          git diff --staged --quiet || git commit -m "Update changelog"
          git push
        env:
          GITHUB_TOKEN: ${{ secrets.GITHUB_TOKEN }}

## �🛠️ Custom Build

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
