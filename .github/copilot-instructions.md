# GitHub Copilot Instructions for Docker-BuildAgent

Canonical contract (image, build command, global tool, project configuration, Docker-template discovery): [design/20-contract.md](../design/20-contract.md)

Canonical contract (PowerShell module): [PSModule.requirements.md](../PSModule.requirements.md)

This file explains the architecture and the development workflow; the contract owns the public surfaces. It is the one place that lists the repository layout and the commands for building and testing it. Other pages link here rather than repeat them.

## Project Overview

Docker-BuildAgent is a build-agent container image. It carries a set of NUKE-based build types (compiled from the `forge/` projects), a `build` command that runs them, and the tooling they need. Callers run it with `docker run <image> build <type> [args]`, through the PowerShell module, or through the `update` global tool for container updates.

The image declares no `ENTRYPOINT`. `build` is a script on `PATH` that forwards to `scripts/nuke/build.ps1`, which runs the compiled build type.

## Build types

Five build types are accepted by `build <type>`: `docker`, `node`, `node-in-docker`, `node-template` and `forge`.

| Type | Implementation | What it does |
|---|---|---|
| `docker` | `forge/Docker/` | Builds a container image, and optionally pushes it and creates a GitHub release |
| `node` | `forge/Node/` | Builds a Node.js application and copies the output to the artifacts directory |
| `node-in-docker` | `forge/NodeInDocker/` | Runs the Node build, then the Docker build |
| `forge` | `forge/Forge/` | Generates the change log |
| `node-template` | `scripts/nuke/build.ps1` (no forge project) | Builds a documentation site from a Node template repository |

The parameters of each type are listed in `documentation/docs/parameters.md`, and the types in `documentation/docs/build-types.md`.

## Forge project map

`forge/Forge.sln` holds every project below and its test project. The "Contract section" column names the part of [design/20-contract.md](../design/20-contract.md) that owns the project's public behavior.

| Project | Purpose | Test project | Contract section |
|---|---|---|---|
| `Common` | Base build class, components, services, parameters, entities, notifications and utilities shared by every build type | `Common.Tests` | Build parameters |
| `Config` | Reads, checks and resolves the project configuration file and the precedence of parameter sources | `Config.Tests` | Project configuration and precedence; Persisted schemas (Project configuration file); Error semantics (Config) |
| `Docker` | The `docker` build type and Docker template discovery | `Docker.Tests` | `build <type>` inside the image; Docker template discovery |
| `Node` | The `node` build type | `Node.Tests` | `build <type>` inside the image |
| `NodeInDocker` | The `node-in-docker` build type; it references `Node` and `Docker` | `NodeInDocker.Tests` | `build <type>` inside the image |
| `Forge` | The `forge` build type (change log generation) | `Forge.Tests` | `build <type>` inside the image |
| `Surface` | Derives, validates, serializes and compares the surface manifest | `Surface.Tests` | Types (Surface manifest); Persisted schemas (Surface manifest asset); Error semantics (Surface model) |
| `Release` | Release version, release claim, release notes composition and validation; the release pipeline, its sink publishers and packagers | `Release.Tests` | Types (Release version, Release claim); Error semantics (Release pipeline) |
| `Publish` | The release entry point the release workflows run through `.github/actions/publish-release`; it refuses to run outside CI | none (its parts are tested in `Release.Tests`) | Types (Release version); Error semantics (Release pipeline) |
| `Update` | The container updater: health check, rollback, lock and update log | `Update.Tests` | Types (Container update, Prior image pin and prior container); Persisted schemas (Update log); Error semantics (Updater) |
| `Tool` | The global tool `BuildAgent.Tool`, command `build-agent`; today it handles the `update` command and calls `Update`. Packing needs `-p:ReleaseVersion=<version>` | none | Global tool |
| `DocsCheck` | Checks that the documentation names real paths, commands, parameters and build types, and the canonical-contract rules | `DocsCheck.Tests` | Error semantics (Docs check) |

How the projects relate:

- `Docker`, `Node`, `NodeInDocker` and `Forge` are the executables that implement build types. They reference `Common` and `Config`.
- `Surface` references `Common`. `Release` references `Common` and `Surface`. `DocsCheck` references `Surface`.
- `Tool` references `Update`. `Publish` references `Release` and `Surface`.
- `Release` and `Surface` run in a workflow only through `Publish`, in the release workflows. They are exercised by their tests and, for `Surface`, by `DocsCheck`.

## Core Architecture

### Base Class Pattern

All build types inherit from `Base<TParams, TNotifications>` (`forge/Common/Base.cs`), which derives from NUKE's `NukeBuild`. It provides:

- A per-build dependency injection container (`Microsoft.Extensions.DependencyInjection`). See `documentation/docs/architecture/dependency-injection.md`.
- Console logging with the `forge` formatter.
- Parameter hydration and the project configuration gate.
- Services for Git, GitHub, Docker, Node.js and change log configuration.
- Notifications (Discord).

### Component Interfaces

Interfaces in `forge/Common/Components/` that add targets to a build type:

- `ICleanComponent`: the `Clean` target, which removes the artifacts directory contents.
- `IDockerComponent`: the `BuildDockerImage` and `PushToRegistry` targets.
- `INodeComponent`: the `GenerateEnvironment`, `BuildApplication` and `CopyToArtifacts` targets.
- `IGitHubComponent`: the `PublishToGitHub` target (release and tag).

`Base` itself provides `Setup`, and each build type defines `Build` as the target that ties its targets together. `documentation/docs/targets.md` lists the targets of each build type and their order.

### Parameter Inheritance Hierarchy

```
ForgeParams (base parameters)
├── DockerParams (container-specific)
├── NodeParams (Node.js-specific)
└── NodeInDockerParams (combined Docker + Node)
```

Every public property of a parameter class is a build parameter and a project configuration key, so changing one changes a protected surface. Read the "Build parameters" section of `design/20-contract.md` first.

### Common code (`forge/Common/`)

- `Services/`: `GitService`, `GitHubService`, `DockerService`, `NodeService`, `ChangeLogConfigService`.
- `Utilities/`: `Files.cs` (environment file generation and map file parsing), `Common.cs` (version detection and paths), `Docker.cs`, `Git.cs`, `GitHub.cs`, `Node.cs`, `EnvironmentLoader.cs`.
- `Entities/`: `BuildConfig` (the `.build/` file names), version, change log and release entities.
- `Extensions/`: file system, logging, object and string extensions.

## Project configuration file

A project can keep build settings in `buildagent.yml`, `buildagent.yaml` or `buildagent.json` at its root. The file holds `schemaVersion`, `buildType` and a `parameters` map keyed by the kebab-case parameter names. Secrets (`RegistryUser`, `RegistryToken` and `NotificationsWebHookUrl`) are rejected in the file. Reading and checking the file is the job of `forge/Config`, and each build type's `Main` passes a `ProjectConfigurationGate` to `Build`. The file format, precedence and errors are in `documentation/docs/project-configuration.md`, and samples are in `samples/`.

## Build Configuration System (`.build/` directory)

A consuming project can add these files under `.build/`. They are the names defined in `forge/Common/Entities/BuildConfig.cs`.

```
.build/
├── .app.env.map         # Maps values into the application's .env file (project root)
├── .build.env.map       # Maps values into the build's environment file (.build/.build.env)
├── .build.copy          # Files and directories to copy to artifacts
└── .build.scripts       # Commands to run during the build
```

### Environment mapping syntax

```bash
# .build.env.map example
RegistryToken=env:GITHUB_TOKEN
ImageTag=const:latest
```

- `env:VARIABLE` reads the value from an environment variable.
- `const:value` sets a constant value.
- A bare `VARIABLE` with no prefix also reads an environment variable.

`ParseEnvironment` in `forge/Common/Utilities/Files.cs` implements this. There is no default-value syntax, and a line that resolves to nothing fails the build.

### Scripts and copy lists

```bash
# .build.scripts: one command per line
npm ci
npm run build:prod

# .build.copy: one path per line
dist/
package.json
```

The Node package manager is detected from lock files in the project root: `pnpm-lock.yaml` selects pnpm, `yarn.lock` selects yarn, anything else selects npm.

## Which command runs what

Run these from the repository root.

| Goal | Command |
|---|---|
| Compile everything | `dotnet build forge/Forge.sln` |
| Run all .NET tests | `dotnet test forge/Forge.sln` |
| Run one test project | `dotnet test forge/Common.Tests` |
| Check the documentation | `dotnet run --project forge/DocsCheck -c Release -- .` |
| Run the PowerShell module tests (Pester 5) | `Invoke-Pester -Path scripts/powershell-module/Docker-BuildAgent.Tests.ps1 -CI -Output Detailed` |
| Run a build type locally | `.\build.ps1 -type docker --dry-run true` (compiles `forge/Forge.sln` into `artifacts/`, then runs the type) |
| Build the image | `dotnet build forge/Forge.sln -o artifacts -c Release`, then `docker build -t build-agent:dev .` |
| Before opening a pull request | The .NET tests, the DocsCheck command and the Pester tests above |
| CI on a pull request (`ci.yml`) | Three jobs: module tests on Windows PowerShell 5.1 and PowerShell 7; Docs Check (the DocsCheck command, then `pwsh scripts/sync-site-home.ps1 -Check`), which runs in parallel with the module tests; and, after the module tests, Build & Validate (`dotnet test forge/Forge.sln` with coverage, `nuke --type docker --dry-run true` and a docs-template build) |
| Release | The `release.yml` workflow (manual) or a pushed `v*` tag (`release-tag.yml`). See `documentation/docs/releases.md` |

The `Dockerfile` copies the compiled build from `artifacts/` and needs the `docs-template/` submodule checked out.

## Build Execution Patterns

### Container-Based Execution (Recommended)

```bash
# Docker build with image tagging and registry push
docker run --rm -v ./:/workspace ghcr.io/the-running-dev/build-agent:latest build docker --registry-url ghcr.io --registry-user username

# Node.js build with artifact output
docker run --rm -v ./:/workspace ghcr.io/the-running-dev/build-agent:latest build node --artifacts-dir ./dist

# Node + Docker combined build
docker run --rm -v ./:/workspace ghcr.io/the-running-dev/build-agent:latest build node-in-docker

# Documentation site from a template
docker run --rm -v ./:/workspace ghcr.io/the-running-dev/build-agent:latest build node-template -AppDir documentation

# Change log generation
docker run --rm -v ./:/workspace ghcr.io/the-running-dev/build-agent:latest build forge --change-log-source all
```

A build that needs the Docker daemon also needs the Docker socket mounted (`-v /var/run/docker.sock:/var/run/docker.sock`).

### Using the PowerShell module

```powershell
Import-Module .\scripts\powershell-module\Docker-BuildAgent.psd1

# DockerImage, DockerHost and WorkspacePath are all mandatory
Set-BuildAgentConfig `
    -DockerImage "ghcr.io/the-running-dev/build-agent:latest" `
    -DockerHost "tcp://host.docker.internal:2375" `
    -WorkspacePath $PWD `
    -ArtifactsDir "./artifacts"

Invoke-Build -type "docker" -args @{ imageTag = "myapp"; registryUrl = "ghcr.io" }
Invoke-Build -type "node" -args @{ artifactsDir = "./dist" }
Invoke-Build -type "node-in-docker" -args @{}
```

`-DockerHost` must match `tcp://host:port`, `unix:///path` or `npipe:////./pipe/name`. Hashtable keys become kebab-case arguments (`imageTag` becomes `--image-tag`). The module's contract is `PSModule.requirements.md`.

### Direct execution of a build type

Build the solution first (the local `build.ps1` does this), then run the type through the local script:

```powershell
.\build.ps1 -type docker --dry-run true
.\build.ps1 -type node --artifacts-dir ./dist
.\build.ps1 -type node-in-docker --dry-run true
```

`-type` and `-isProd` are the script's own parameters. Everything else is forwarded to the build as `--kebab-case` arguments.

## PowerShell Automation (`scripts/`)

- `scripts/powershell-module/`: the `Docker-BuildAgent` module (`.psm1`, `.psd1`), the Pester tests, and `Update-ModuleParameters.ps1`, which parses `forge/Common/Parameters/*.cs` and writes a `parameters.json` into the module folder. The module does not ship that file today.
- `scripts/nuke/build.ps1`: the in-image wrapper behind `build <type>`. Its parameters are `-type` (positional, mandatory), `-workingDir`, `-artifactsDir` (the directory of compiled build DLLs, default `/nuke/forge`), the node-template parameters (`-appDir`, `-nodeTemplateRepositoryUrl`, `-nodeTemplateDirPath`, `-skipInstall`, `-packageManager`, `-isProduction`) and the remaining arguments, which pass to the build verbatim.
- `scripts/nuke/nuke-helpers.psm1`: helper functions used by both build scripts (`Initialize-Build`, `Invoke-DotNetBuild`, `Invoke-Forge`, `Copy-Directory`, `Get-PackageManager`, `Invoke-SafeCommand`, and others). See `documentation/docs/architecture/powershell-helpers.md`.
- `scripts/nuke/bin/build`: the executable on `PATH` in the image.

## GitHub Actions

The workflows are in `.github/workflows/`; shared steps are in `.github/actions/common` (environment setup) and `.github/actions/tests` (tests and coverage). `documentation/docs/ci-cd.md` describes each workflow.

| Workflow | Trigger |
|---|---|
| `ci.yml` | Pull requests, manual |
| `build.yml` | Push to `main` that changes more than `documentation/**` and `.github/**`, manual |
| `release.yml` | Manual |
| `release-tag.yml` | A pushed `v*` tag |
| `docs.yml` | Documentation changes on `main`, manual, repository dispatch |
| `module-tests.yml` | Called by `ci.yml`, `release.yml` and `release-tag.yml` |
| `claude-code-review.yml` | Pull requests |

Builds read `GITHUB_TOKEN` and the parameters `RegistryToken` and `NotificationsWebHookUrl` from the environment. The workflows pass them from repository secrets.

## Documentation System

Pages live in `documentation/docs/`. The site itself is built from the pinned `docs-template/` submodule (Docusaurus), by `docs.yml` and as a check in `ci.yml`. `design/docs-classification.txt` says which document owns which public surface, and the DocsCheck command fails when a page names something that does not exist. It reads only `README.md`, `documentation/docs/**`, `documentation/src/pages/*` and the module sources in `scripts/powershell-module/`; other files are not checked. Run it after any documentation change.

Two files are generated; edit their sources, not them:

- `documentation/src/pages/index.md` comes from `README.md`. Run `pwsh scripts/sync-site-home.ps1` after editing the README; CI runs it with `-Check`.
- The tables in `documentation/docs/parameters.md` come from the `*Params` classes. Run `pwsh scripts/Update-ParameterDocs.ps1` after changing a parameter; `-Check` reports drift.

## Testing Strategy

- Unit tests use xUnit and Moq, one test project per forge project (except `Tool`), all in `forge/Forge.sln`.
- Service tests mock the service interfaces. `forge/Common.Tests` also covers the base class and the dependency injection setup.
- The PowerShell module has Pester 5 tests in `scripts/powershell-module/Docker-BuildAgent.Tests.ps1`.
- `forge/DocsCheck.Tests` keeps the documentation classification and the checker honest.

## Security Considerations

- Keep tokens in environment variables. Build output hides GitHub tokens and URLs through the `StripForDisplay` patterns in `BuildConfig`, and the parameter display leaves out `RegistryToken`.
- The project configuration file may not carry secrets.
- The image does not set a `USER`, so builds run as the image's default user (root). Treat the image as a trusted build environment, and mount only the workspace and, when needed, the Docker socket.
- The module prints "arguments hidden for security" instead of the argument list.

## Conventions

- Error handling in PowerShell goes through `Invoke-SafeCommand`, and log lines use ASCII prefixes such as `[OK]`, `[ERROR]` and `[WARN]` so they render in Windows PowerShell 5.1.
- The `build` wrapper propagates the build's exit status. Statuses are listed in the contract.
- Commit messages use a lowercase conventional prefix (`fix:`, `docs:`, `feat:`). Do not rewrite pushed history.
