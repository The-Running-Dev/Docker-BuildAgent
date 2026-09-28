---
id: migration
title: "Migration Guide"
sidebar_position: 10
---

# Migration Guide

This guide covers every breaking change in 2.0.0 and the move from a floating tag to a pinned
version. What is protected from now on is stated in [Compatibility and Support](./compatibility.md).

Canonical contract: [design/20-contract.md](https://github.com/The-Running-Dev/Docker-BuildAgent/blob/main/design/20-contract.md)

Canonical contract (PowerShell module): [PSModule.requirements.md](https://github.com/The-Running-Dev/Docker-BuildAgent/blob/main/PSModule.requirements.md)

## Breaking changes in 2.0.0

- [PowerShell module: default image is version-pinned](#powershell-module-default-image-is-version-pinned-200)
- [Launcher failures carry stable codes](#launcher-failures-carry-stable-codes-200)
- [Map-derived environment no longer overwrites a set variable](#map-derived-environment-no-longer-overwrites-a-set-variable-200)

## PowerShell module: default image is version-pinned (2.0.0)

**Before:** with the module's defaults, `Invoke-Build` ran
`ghcr.io/the-running-dev/build-agent:latest`.

**Now:** the default is the module's own version, for example
`ghcr.io/the-running-dev/build-agent:2.0.0`. The module and the image it runs stay in step.

**What to do:**

- Nothing, if you want the image that matches your module version.
- To stay on `latest`, or to use any other reference, set it explicitly. The value is used
  verbatim:

  ```powershell
  Set-BuildAgentConfig `
      -DockerImage "ghcr.io/the-running-dev/build-agent:latest" `
      -DockerHost "tcp://host.docker.internal:2375" `
      -WorkspacePath $PWD
  ```

## Launcher failures carry stable codes (2.0.0)

**Before:** launcher failures had no stable identifier, and a configured image that could not
be obtained could be replaced by another version.

**Now:** `Invoke-Build` and `Set-BuildAgentConfig` raise terminating errors whose
`FullyQualifiedErrorId` is the code and whose `Exception.Data['ExitCode']` is the exit status:

| Code | Exit status | Cause |
|---|---|---|
| `WorkspaceInvalid` | 3 | The workspace path is absent or not a directory. |
| `DockerUnavailable` | 5 | The Docker daemon cannot be reached. |
| `ImageUnavailable` | 5 | The configured image cannot be obtained. It never falls back to another version. |
| `BuildFailed` | the container's own | The container exited non-zero; its status is carried unchanged. |

**What to do:** match on the code rather than on message text, and treat an image that cannot
be pulled as a failure to fix, not a fallback to expect.

## Map-derived environment no longer overwrites a set variable (2.0.0)

**Before:** values generated from `.build.env.map` were loaded over the process environment, so
a generated value replaced a variable you had already set.

**Now:** a variable already set in the process environment keeps its value. The generated value
applies only where nothing is set. Configuration precedence, highest first: invocation
arguments, module configuration, process environment, map-derived environment, project
configuration file, declared defaults.

**What to do:** if you relied on a generated value replacing a variable that is already set,
unset that variable before the build, or set the value you want explicitly.

## Moving from `latest` to a pinned version

`latest` is movable: it follows the newest push to `main`. A pinned version is immutable, so a
build that names one reproduces. To move:

1. Pick the release you want from the release notes, for example `2.0.0`.
2. Replace the tag in every reference to the image:

   ```bash
   docker run --rm -v ./:/workspace ghcr.io/the-running-dev/build-agent:2.0.0 build docker
   ```

3. For the PowerShell module, install the module version that matches the image. It runs that
   image by default, so no `-DockerImage` is needed. Set `-DockerImage` only to use another
   reference.
4. Upgrade deliberately: change the pinned version, read that release's breaking-changes
   section, and run your build.
