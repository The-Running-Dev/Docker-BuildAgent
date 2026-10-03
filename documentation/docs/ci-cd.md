---
id: ci-cd
title: "🔄 GitHub Actions"
sidebar_position: 8
---

Canonical contract (build command): [design/20-contract.md](https://github.com/The-Running-Dev/Docker-BuildAgent/blob/main/design/20-contract.md)

## This repository's workflows

Docker-BuildAgent builds, tests, publishes and documents itself with seven GitHub Actions
workflows in `.github/workflows`. Each file's header comment describes it; this table is the
overview.

| Workflow (`name:`) | File | Runs on | Publishes |
|---|---|---|---|
| **CI** | `ci.yml` | Pull requests and manual runs | Nothing |
| **Build** | `build.yml` | Pushes to `main` (except changes only under `documentation/**` or `.github/**`) and manual runs | `latest` image, on a push only |
| **Release** | `release.yml` | Manual runs | Versioned image, `latest` and a GitHub release |
| **Release-from-Tag** | `release-tag.yml` | Pushes of a `v*` tag | Versioned image, `latest` and a GitHub release |
| **Docs** | `docs.yml` | Pushes to `main` that change `documentation/**`, `docs-template` or `scripts/setup-docs-submodule.ps1`; a repository dispatch of type `Update-Documentation`; manual runs | The documentation site, to GitHub Pages |
| **Module Tests** | `module-tests.yml` | Called by CI, Release and Release-from-Tag; it has no trigger of its own | Nothing |
| **Claude Code Review** | `claude-code-review.yml` | Pull requests when opened, updated, marked ready for review or reopened | Nothing; posts review comments on the pull request |

CI does not run on pushes to feature branches: open a pull request, or run it manually.

### CI

CI has three jobs:

- **Module Tests** calls the Module Tests workflow.
- **Docs Check** runs `dotnet run --project forge/DocsCheck -c Release -- .`. It fails the pull
  request when a document names a path, command, parameter, build type or template-discovery
  location that the repository does not have, or breaks the canonical-contract rules.
  It then runs `pwsh scripts/sync-site-home.ps1 -Check`, which fails the pull request when the
  site home page (`documentation/src/pages/index.md`) no longer matches `README.md`, and
  `pwsh scripts/Update-ParameterDocs.ps1 -Check`, which fails it when the tables in
  [Parameters](parameters.md) no longer match the `*Params` classes.
- **Build & Validate** runs after Module Tests. It runs every test project in `forge/Forge.sln` with
  coverage (test results appear as a check run, and a coverage summary is posted on the pull
  request), makes a dry run of the Docker build with `nuke --type docker --dry-run true`, and
  builds the documentation site from `docs-template`.

### Build

Build runs the tests, makes a dry run of the Docker build, and on a push to `main` builds and
pushes the image. A push to `main` moves `latest` and writes no versioned tag and no GitHub
release. A manual run of Build does the tests and the dry run only; it does not push.
When a push to `main` changes files under `documentation/`, Build also dispatches
`Update-Documentation`, which Docs listens for.

### Release and Release-from-Tag

Both run the Module Tests workflow and the test projects, then build and push the image under the
version tag and `latest`, create the GitHub release and dispatch `Update-Documentation`. Release
is started by hand; Release-from-Tag starts when a `v*` tag is pushed. See
[Release Management](releases.md) for versions, pre-releases and what a release contains.

### One publisher at a time

Build, Release and Release-from-Tag share one concurrency group, `buildagent-publish`, with
`cancel-in-progress: false`. At most one of them runs at a time, and a run already in progress
always finishes. GitHub keeps only one pending run per group, so a newer pending run replaces an
older one: if a queued release disappears, run it again.

### Module Tests

Module Tests runs the PowerShell module's Pester tests on `windows-latest` under Windows
PowerShell 5.1 and PowerShell 7. Both must pass: CI, Release and Release-from-Tag each depend on
it. `docker` is mocked, so a Windows runner is enough.

### Docs

Docs first runs the same three documentation checks as the CI Docs Check job (DocsCheck, the site
home page check and the parameter tables check); if one fails, nothing is deployed. It then checks
out the repository with submodules, builds the site from `docs-template`
(`pnpm --dir docs-template build`) and deploys `docs-template/artifacts` to GitHub Pages.

### Pinned tools

The workflows and the Dockerfile use the same tool versions: pnpm 10.16.0 (`ci.yml`, `docs.yml`,
`ARG PNPM_VERSION`), GitVersion 6.5.1 and Nuke 10.1.0 (`.github/actions/common`,
`ARG GITVERSION_VERSION`, `ARG NUKE_VERSION`). Change them together.

---

## Creating official releases

### Option 1: manual release

1. Go to the **Actions** tab of the repository.
2. Select the **Release** workflow.
3. Choose **Run workflow**.
4. Set **Mark as Pre-Release** for a beta or release candidate.
5. Choose **Run workflow**.

The workflow takes one working input, the pre-release flag. The version always comes from
GitVersion: a **Release Version** input exists, but a run that supplies it fails with an explicit
error instead of ignoring it. There is no release-notes input; the notes are generated from the
commit history.

### Option 2: tag-based release

```bash
# Create and push a version tag
git tag v1.2.3
git push origin v1.2.3

# For pre-releases
git tag v1.0.0-beta.1
git push origin v1.0.0-beta.1
```

A tag with a suffix such as `-beta` or `-rc` is marked as a pre-release.

---

## Workflows for your own project

The examples below run in your repository and use the Build Agent image. They are starting
points; for the workflows this repository itself runs, read the files in `.github/workflows`.

## 🐳 Docker Image

This workflow builds and pushes a Docker image using the Build Agent. It checks out your repository, runs the `build docker` command, and passes required secrets for authentication.

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
        run: build docker
        env:
          GITHUB_TOKEN: ${{ secrets.GITHUB_TOKEN }}
          RegistryToken: ${{ secrets.REGISTRY_TOKEN }}
```

## 🟢 Node.js App

This workflow builds a Node.js application using the Build Agent. It checks out your repository, runs the `build node` command, and passes required secrets for authentication.

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
        run: build node
        env:
          GITHUB_TOKEN: ${{ secrets.GITHUB_TOKEN }}
          RegistryToken: ${{ secrets.REGISTRY_TOKEN }}
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
        run: build node-in-docker
        env:
          GITHUB_TOKEN: ${{ secrets.GITHUB_TOKEN }}
          RegistryToken: ${{ secrets.REGISTRY_TOKEN }}
```

## 📝 Changelog Generation

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
            build forge --change-log-source "${{ github.event.inputs.changelog_source }}"
          else
            build forge --target GenerateChangeLog
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
