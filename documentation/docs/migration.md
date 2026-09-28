---
id: migration
title: "Migration Guide"
sidebar_position: 10
---

# Migration Guide

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

**Failures:** if the configured image cannot be obtained, `Invoke-Build` fails with
`ImageUnavailable` (exit status 5). It never falls back to another version. A workspace path
that is absent or not a directory fails with `WorkspaceInvalid` (exit status 3). A non-zero
container exit is surfaced as `BuildFailed` carrying that status unchanged.
