---
id: development-guide
title: Development Guide
sidebar_position: 3
---

How to set up, build, test and change Docker-BuildAgent. The repository layout, the Forge
project map and the command tables live in one place,
[`.github/copilot-instructions.md`](https://github.com/The-Running-Dev/Docker-BuildAgent/blob/main/.github/copilot-instructions.md).
This page does not repeat them, so read that file for what is where and for which command runs what.

## Prerequisites

- **.NET 8 SDK.** CI builds with 8.x. The build-agent image carries the 8, 9 and 10 SDKs.
- **Docker**, running, to build the image or run a build the way a consumer does.
- **PowerShell** 5.1 or later, to run the local build script and the module tests. The module
  tests need Pester 5 or later.
- **Git**, with submodules. The `docs-template/` directory is a pinned submodule, so clone with
  `git clone --recurse-submodules`, or run `git submodule update --init` in an existing clone.
- **Node.js 22**, only to build the documentation site. CI uses 22.

Only .NET and Git are needed for the first check, and it needs no Docker host. From the repository
root, after cloning with submodules:

```bash
dotnet run --project forge/DocsCheck -c Release -- .
```

It exits 0 when the documentation matches the code.

## Build and test

From the repository root:

```bash
# Compile every Forge project and test project
dotnet build forge/Forge.sln

# Run every test project. CI runs the same solution (with coverage and in Release).
dotnet test forge/Forge.sln

# Check the documentation names and the canonical-contract rules
dotnet run --project forge/DocsCheck -c Release -- .
```

`dotnet test forge/Forge.sln` is what the CI test step runs, so a green local run means the
.NET tests will pass in CI. CI also runs the DocsCheck command above and the Pester tests for the
PowerShell module on Windows PowerShell 5.1 and PowerShell 7:

```powershell
Invoke-Pester -Path scripts/powershell-module/Docker-BuildAgent.Tests.ps1 -CI -Output Detailed
```

Run all three before opening a pull request. To run one test project, point `dotnet test` at its
folder, for example `dotnet test forge/Common.Tests`.

## Run a build locally

The root `build.ps1` compiles `forge/Forge.sln` into `artifacts/` and then runs a build type
from there:

```powershell
.\build.ps1 -type docker --dry-run true
```

`build.sh` is the Linux, macOS and CI equivalent. It always runs the `docker` build type and passes its
arguments through. Run locally, neither script pushes an image.

`changelog.ps1` regenerates `CHANGELOG.md` from the Git history and copies it to
`documentation/src/pages/`. It runs the `forge` build type in the published image when Docker is
available, and otherwise through `dotnet run --project forge/Forge/Forge.csproj`.

The build types and their parameters are in [Build Types](../build-types.md) and
[Parameters](../parameters.md).

## Build and run the image

The `Dockerfile` copies in the compiled build from `artifacts/`, so compile first, then build
the image:

```bash
dotnet build forge/Forge.sln -o artifacts -c Release
docker build -t build-agent:dev .
docker run --rm \
  -v /var/run/docker.sock:/var/run/docker.sock \
  -v ./:/workspace \
  build-agent:dev build docker --dry-run true
```

The image build also needs the `docs-template/` submodule to be checked out.

## Tests

The tests use xUnit and Moq. Each Forge project except `Tool` has a sibling `<Project>.Tests`
project, and every test project is part of `forge/Forge.sln`. A test looks like this one, from
[`forge/Common.Tests/DependencyInjection/ServiceLocatorTests.cs`](https://github.com/The-Running-Dev/Docker-BuildAgent/blob/main/forge/Common.Tests/DependencyInjection/ServiceLocatorTests.cs):

```csharp
[Fact]
public void Initialize_WithNullProvider_ThrowsArgumentNullException()
{
    Assert.Throws<ArgumentNullException>(() => ServiceLocator.Initialize(null!));
}
```

Prefer a mock of a service interface (`Mock<IGitService>`) over the concrete service. See
[Dependency Injection](./dependency-injection.md) for how services are registered.

## Add a build type

A build type is a project under `forge/` whose class derives from `Base<TParams, TNotifications>`,
with a parameter class that derives from `ForgeParams`. Use an existing one such as
[`forge/Node`](https://github.com/The-Running-Dev/Docker-BuildAgent/tree/main/forge/Node) and its
test project as the model, and look at what else names a build type: the build wrapper, the PowerShell module's
list of types, the project configuration catalog in `forge/Config`, and the documentation. Every
public property of a parameter class becomes a build parameter and a configuration key, so a new
property is a change to a protected surface. Read the "Build parameters" section of
`design/20-contract.md` first.

## Change the documentation

The pages are in `documentation/docs/`, and `design/docs-classification.txt` says which document
owns which public surface. A page that explains a surface carries a `Canonical contract:` line
naming the document that owns it, and a new page needs an entry in the classification file. The
DocsCheck command above fails when a page names a path, command, parameter or build type that does
not exist, so run it after any documentation change.

Two files are generated, so edit their sources and regenerate them. CI checks both with `-Check`:

```powershell
# documentation/src/pages/index.md is generated from README.md
pwsh scripts/sync-site-home.ps1

# the tables in documentation/docs/parameters.md are generated from the parameter classes
pwsh scripts/Update-ParameterDocs.ps1
```

To preview the site with hot reload, run `./scripts/build-docs-local.ps1`. It needs `pnpm` and the
`docs-template/` submodule, and serves http://localhost:3000. `docs.ps1` and `docs-image.ps1` in the
repository root are older helpers and are not the supported flow.

## Contributing

1. Branch from `main`.
2. Make the change with its tests.
3. Run `dotnet test forge/Forge.sln`, the DocsCheck command and the two `-Check` commands above. The
   full list is in [CONTRIBUTING.md](https://github.com/The-Running-Dev/Docker-BuildAgent/blob/main/CONTRIBUTING.md).
4. Open a pull request and wait for the CI checks.

Commit messages use a lowercase conventional prefix, for example `fix: ...`, `docs: ...`,
`feat: ...`. Do not rewrite history that has been pushed; add a follow-up commit instead.

## Continuous integration

The workflows are in `.github/workflows/`. [CI/CD](../ci-cd.md) describes what each one does, and
[Releases](../releases.md) describes how a release is made.

| Workflow | Runs on |
|---|---|
| `ci.yml` (CI) | Pull requests and manual dispatch: module tests, docs check, then build, test and a Docker dry run |
| `build.yml` (Build) | Pushes to `main` and manual dispatch |
| `release.yml` (Release) | Manual dispatch |
| `release-tag.yml` (Release-from-Tag) | A pushed `v*` tag |
| `docs.yml` (Docs) | Pushes to `main` that change the documentation, manual dispatch and a repository dispatch event |
| `module-tests.yml` (Module Tests) | Called by `ci.yml`, `release.yml` and `release-tag.yml` |
| `claude-code-review.yml` | Pull requests |

Canonical contract (build command): [design/20-contract.md](https://github.com/The-Running-Dev/Docker-BuildAgent/blob/main/design/20-contract.md)
