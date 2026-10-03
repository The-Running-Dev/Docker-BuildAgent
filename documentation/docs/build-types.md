---
id: build-types
title: Build Types and Commands
sidebar_position: 2
---

Docker-BuildAgent has one `build` command with five types. Each type suits a kind of project.

## Unified Build Command

All builds use the same command pattern:

```bash
build <type> [parameters]
```

Available types: `docker`, `node`, `node-in-docker`, `node-template`, `forge`

## Choose a build

| You have | Run | You get |
|---|---|---|
| A project with a `Dockerfile`, or a type that has a [template](./docker-templates.md) | `build docker` | An image, pushed in CI (and released with `--create-github-release true`) |
| A Node.js application that is not containerized | `build node` | The built application in the artifacts directory |
| A Node.js application that ships as an image | `build node-in-docker` | The Node build, then the image |
| A documentation site | `build node-template` | The site built from a template repository |
| A Git history | `build forge` | `CHANGELOG.md` |

## What each build needs

| Build | Docker host | Your project provides | To push or release in CI |
|---|---|---|---|
| `build docker` | Yes: mount `docker.sock` or set `DOCKER_HOST` | A `Dockerfile`, or none when a template matches | `RegistryToken` and `GITHUB_TOKEN` |
| `build node` | No | A `build:prod` npm script, or `.build/.build.scripts` | Not applicable |
| `build node-in-docker` | Yes | The same as `build node` | `RegistryToken` and `GITHUB_TOKEN` |
| `build node-template` | No | A documentation directory; the template comes from a repository | Not applicable |
| `build forge` | No | The full Git history (`fetch-depth: 0` in GitHub Actions) and a `.build/` directory (the build stops if it is missing) | Not applicable |

Every build runs in the image, and every build mounts the project at `/workspace`.

The flags, environment variables and configuration keys of `docker`, `node`, `node-in-docker` and
`forge` are in [Parameters](./parameters.md), one table per type; `node-template` has no table. The order each type runs its steps in is in
[Targets](./targets.md). What a failed build returns is in [Exit Codes](./exit-codes.md).

Files that a build reads live in the `.build` directory of the project:

| File | Used by | Purpose |
|---|---|---|
| `.build/.build.scripts` | `node`, `node-in-docker` | The commands that build the application, one per line |
| `.build/.build.copy` | `node`, `node-in-docker` | The files to copy to the artifacts directory |
| `.build/.build.env.map` | every type but `node-template` | The variables the build needs and where each value comes from |
| `.build/.app.env.map` | `node`, `node-in-docker` | The variables written to the application's `.env` file |

A map line is `Name=const:value` for a fixed value or `Name=env:VARIABLE` for a value read from the environment.
The build stops with `Build Env Incomplete` when a variable in `.build/.build.env.map` has no value.

---

## build docker

Builds a Docker image from the project and, in CI, pushes it to a registry and publishes a GitHub release.

What it does:

- Builds the image from the project's Dockerfile, or from a [template](./docker-templates.md) when the project has none
- Tags the image `latest`, and also with the version when a GitHub release is requested. The version comes from GitVersion.
- Pushes the tags to the registry in CI, or when `--force-push true` is given
- Creates the GitHub release and the Git tag `v` followed by the version, when `--create-github-release true` is given

Usage:

```bash
docker run \
  -v /var/run/docker.sock:/var/run/docker.sock \
  -v ./:/workspace \
  -it ghcr.io/the-running-dev/build-agent:latest \
  build docker
```

Common flags: `--dry-run true` simulates the build without pushing, `--create-github-release true` creates the
release and `--force-push true` pushes outside CI.

---

## build node

Builds a Node.js application into the artifacts directory.

What it does:

- Runs the commands in `.build/.build.scripts`. Without that file it removes `node_modules`, then runs
  `<package manager> install` and `<package manager> run build:prod`.
- Detects the package manager from the lock file: `pnpm-lock.yaml` means pnpm, `yarn.lock` means yarn, anything else npm; pnpm wins if both lock files exist
- Writes the application's `.env` file from `.build/.app.env.map`
- Copies the files listed in `.build/.build.copy` to the artifacts directory

Usage:

```bash
docker run \
  -v ./:/workspace \
  -it ghcr.io/the-running-dev/build-agent:latest \
  build node
```

---

## build node-in-docker

Runs the Node.js build, then builds and publishes the image.

What it does:

- Phase 1 is the `build node` steps: clean the artifacts directory, write the environment file, build the
  application and copy the artifacts.
- Phase 2 is the `build docker` steps: build the image, push it, then create the GitHub release and Git tag.

The image always gets both a `latest` tag and a version tag. The full order of steps is in
[Targets](./targets.md#node-in-docker).

Usage:

```bash
# Basic usage
docker run \
  -v /var/run/docker.sock:/var/run/docker.sock \
  -v ./:/workspace \
  -it ghcr.io/the-running-dev/build-agent:latest \
  build node-in-docker

# With custom parameters
docker run \
  -v /var/run/docker.sock:/var/run/docker.sock \
  -v ./:/workspace \
  -it ghcr.io/the-running-dev/build-agent:latest \
  build node-in-docker \
  --artifacts-dir ./dist \
  --image-tag my-app \
  --registry-url ghcr.io/myorg \
  --create-github-release true
```

The version tag comes from GitVersion, and there is no flag to set it. The release tag is `v` followed by the version.

Examples:

```bash
# Production build with registry push
build node-in-docker \
  --artifacts-dir ./build \
  --image-tag myapp \
  --registry-url ghcr.io/myorg \
  --registry-user $GITHUB_ACTOR \
  --registry-token $GITHUB_TOKEN \
  --create-github-release true

# Dry run
build node-in-docker \
  --image-tag myapp \
  --dry-run true \
  --verbosity Verbose

# Custom Dockerfile and artifacts location
build node-in-docker \
  --docker-file Dockerfile.prod \
  --artifacts-dir ./dist/app \
  --templates-dir ./docker-templates \
  --image-tag myapp
```

Project structure:

```text
your-project/
├── package.json              # Node.js project configuration
├── .build/
│   ├── .build.scripts        # Optional: custom build commands
│   ├── .build.copy           # Files to copy to the artifacts directory
│   └── .build.env.map        # Variables the build needs
├── Dockerfile                # Optional: a template is used when it is missing
├── set-environment.ps1       # Optional: runs before the build to set variables
└── artifacts/                # Default output directory
    └── (built files)
```

Secrets are given by flag or as variables, not in the project configuration file. The registry credentials and the
notification webhook are the variables `RegistryUser`, `RegistryToken` and `NotificationsWebHookUrl`.
The registry token also authenticates the GitHub release, so the release needs `RegistryToken` even when no image is
pushed. For example, in `.build/.build.env.map`:

```env
RegistryUser=env:GITHUB_ACTOR
RegistryToken=env:GITHUB_TOKEN
```

---

## build node-template

Builds a documentation site from a template repository, usually Docusaurus.

What it does:

- Clones the template repository, by default Docusaurus-Template. A `#branch` suffix on the URL selects a branch.
- Copies the template files into the application directory without overwriting existing files
- Runs `template-setup.ps1` in the working directory if it exists there, then deletes it
- Detects the package manager as `build node` does, installs dependencies and runs `<package manager> run build:prod`

This type is a PowerShell script. Its options take a single dash, and they are not in [Parameters](./parameters.md).

| Option | Default | Meaning |
|---|---|---|
| `-AppDir` | `documentation` | The application directory, relative to the working directory |
| `-WorkingDir` | the current directory | The project root |
| `-NodeTemplateRepositoryUrl` | `https://github.com/The-Running-Dev/Docusaurus-Template.git` | The template repository |
| `-NodeTemplateDirPath` | `/node-template` | Where the template is cloned. An existing directory there is removed first. |
| `-PackageManager` | detected | `npm`, `pnpm` or `yarn` |
| `-SkipInstall` | off | Skips the dependency install |
| `-IsProduction` | on | Runs `build:prod`. Pass `-IsProduction:$false` to skip it. |

Usage:

```bash
docker run \
  -v ./:/workspace \
  -it ghcr.io/the-running-dev/build-agent:latest \
  build node-template -AppDir documentation
```

Examples:

```bash
# Basic usage with auto-detection
build node-template

# Custom directory with specific package manager
build node-template -AppDir docs-ui -PackageManager pnpm

# Skip install and build for development
build node-template -SkipInstall -IsProduction:$false

# Use custom template repository
build node-template -NodeTemplateRepositoryUrl https://github.com/my-org/custom-template.git
```

---

## build forge

Generates the change log from the Git history.

What it does:

- Writes `CHANGELOG.md` in the project root and puts the new entries before any existing content
- Takes the commits since the last tag, the complete history, or the commits since a tag you name
- Groups the commits by date, latest first, with dates written as `yyyy.MM.dd`

Usage:

```bash
# Generate changelog since last tag
docker run \
  -v ./:/workspace \
  -it ghcr.io/the-running-dev/build-agent:latest \
  build forge

# Generate complete history
docker run \
  -v ./:/workspace \
  -it ghcr.io/the-running-dev/build-agent:latest \
  build forge --change-log-source all

# Generate changelog since specific tag
docker run \
  -v ./:/workspace \
  -it ghcr.io/the-running-dev/build-agent:latest \
  build forge --change-log-source v1.0.0
```

The values of `--change-log-source` are in [Parameters](./parameters.md#change-log-source). The targets are `Setup`,
`GenerateChangeLog` and `Build`, as listed in [Targets](./targets.md#forge).

Output format, with the date of the run in the heading:

```markdown
## Since v1.4.0 (2025.08.04)

### 2025.08.04

- Update build script to include 'forge' as a build type option
- Refactor NodeService to handle different shell commands
- Update documentation directory path

### 2025.08.03

- Work in Progress
```

With `--change-log-source all` the heading reads `History` in place of `Since <tag>`. Each entry is the commit
message alone. The format has no option for hashes, authors or other date styles.

---

## Behavior Shared by All Types

- Every type but `node-template` first runs `set-environment.ps1` from the project root, when it exists, and adds
  `--root` with the working directory unless you pass it.
- `.build/.build.env.map` is read before any step. The order that decides which value wins when a setting is given
  in several places is in [Project Configuration File](./project-configuration.md#precedence).
- A [project configuration file](./project-configuration.md) can supply parameters. It is checked before any
  step runs, and an invalid file ends the build with status 2.
- `--dry-run true` simulates the Docker steps, and `--force-push true` pushes and releases outside CI.
- Notifications go to the webhook in `NotificationsWebHookUrl`, in CI only unless `--force-notifications true` is given.

## Related Documentation

- [Parameters](./parameters.md) lists every flag, variable and configuration key.
- [Targets](./targets.md) lists the steps of each type.
- [Exit Codes](./exit-codes.md) lists what a build returns.
- [Docker Templates](./docker-templates.md) explains the Dockerfile templates.
- [Customization](./customization.md) covers custom build scripts and configuration.
- [CI/CD](./ci-cd.md) has GitHub Actions examples.

Canonical contract (build command): [design/20-contract.md](https://github.com/The-Running-Dev/Docker-BuildAgent/blob/main/design/20-contract.md)
