---
id: compatibility
title: "Compatibility and Support"
sidebar_position: 11
---

# Compatibility and Support

This page states what Docker-BuildAgent promises to keep stable, how versions work, and where
support ends. The promise binds from v2.0.0.

Canonical contract: [design/20-contract.md](https://github.com/The-Running-Dev/Docker-BuildAgent/blob/main/design/20-contract.md)

The contract is the authority. Where this page and the contract disagree, the contract is right
and this page is the defect.

## Protected surfaces

A breaking change is a removed or renamed command, parameter or key, or a changed default or
documented behaviour, on a protected surface. The protected surfaces are:

- **The image**, at least its invocation: the entrypoint, the accepted build types, the mounts,
  and the environment inputs.
- **The build command** (`build`).
- **The global tool.**
- **Project configuration.**
- **Docker-template discovery.**
- **The PowerShell module.**

Image contents are protected only where the contract names them. Bundled tool versions are not
protected unless the contract names them, so a tool inside the image can be upgraded in any
release.

## Versioning

- The image, the global tool and the PowerShell module share one version and are released
  together.
- A breaking change to a protected surface needs a new major version.
- A versioned image tag, from v2.0.0, is immutable: its content never changes and it is never
  deleted.
- `latest` is movable. A push to `main` moves `latest` and nothing else, so it follows the newest
  `main` and can change under you at any time. Pin a version when you need a build to
  reproduce.

To select a versioned image, put the version in the tag. 2.0.0 is the first version this
promise covers and it is not published yet, so until it is, use a version that exists:

```bash
docker run --rm -v ./:/workspace ghcr.io/the-running-dev/build-agent:2.0.0 build docker
```

The PowerShell module runs the image that matches its own version unless you set
`-DockerImage` explicitly. See the [migration guide](./migration.md) for moving from `latest`
to a pinned version.

## Which versions receive fixes

Fixes ship only in the current major version. Earlier versioned images stay pullable, because
their tags are immutable, but they are not patched.

"Supported" on this page means the compatibility promise above. It is not a response-time or
fix-window commitment.

## Deprecation policy

A surface is deprecated in a minor release. A deprecated surface warns when used. It is
removed no earlier than the next major release. Every deprecation is listed in the
deprecations section of that release's notes, and so is every breaking change in the
breaking-changes section, each present and marked empty when there is nothing to list.

## Supported hosts and CI

- **Hosts:** Linux with Docker Engine, and Windows with Docker Desktop running Linux
  containers. macOS is best-effort.
- **Global tool:** supported on Linux and Windows.
- **CI:** GitHub Actions on Linux and Windows, hosted and self-hosted. Other CI systems are
  best-effort.
- **PowerShell module:** PowerShell 5.1 or later, tested on 5.1 and 7.
- **Image architecture:** amd64.
- **Network:** builds need registry and dependency-network access. Standard proxy settings are
  honoured. Internal mirrors and offline builds are unsupported.

### Update verification

The container update check and rollback behaviour is verified on Linux. Windows live-daemon
coverage is a gap: update behaviour against a live Docker Desktop daemon on Windows is not
verified, and this page does not claim it is.

## Docker socket

Some builds need the host Docker socket. Mounting it into the build container assumes a trusted
host and trusted code. A host compromised through the mounted socket is not a defect in the
product. Do not mount the socket for code you do not trust.
