---
id: releases
title: "Release Management"
sidebar_position: 10
---

# Release Management

This guide explains how Docker-BuildAgent publishes images and releases, and how versions are
chosen. For what a version promises its users, see [Compatibility and Support](./compatibility.md).

## Publishing at a glance

| Event | Workflow | Moves `latest` | Writes a version |
|---|---|---|---|
| Pull request, or manual run | `ci.yml` (**CI**) | No | No |
| Push to `main` | `build.yml` (**Build**) | Yes | No |
| Manual run | `release.yml` (**Release**) | Yes | Yes |
| Push of a `v*` tag | `release-tag.yml` (**Release-from-Tag**) | Yes | Yes |

A push to `main` writes no version: it moves `latest` and nothing else. A versioned image tag,
a GitHub release and a version tag are written only by a release.

`latest` is movable. A versioned tag, from v2.0.0, is immutable: its content never changes and it
is never deleted. Pin a version when a build must reproduce.

### CI

- **Trigger**: pull requests and manual runs. It does not run on pushes to feature branches.
- **Does**: module tests, the documentation check, the test projects with coverage, a dry run
  of the Docker build, and a build of the documentation site.
- **Publishes**: nothing.

### Build

- **Trigger**: every push to `main` that changes more than `documentation/**` or `.github/**`,
  and manual runs.
- **Does**: runs the tests, then builds and pushes the image as `latest`.
- **Publishes**: `latest` only. No GitHub release, no versioned tag.

### Release and Release-from-Tag

- **Trigger**: a manual run of **Release**, or pushing a `v*` tag, which runs **Release-from-Tag**.
- **Does**: runs the module tests and the test projects, then builds the image, pushes the
  versioned tag and `latest`, and creates the GitHub release with its changelog.
- **Publishes**: the versioned image, `latest` and the GitHub release.

The three publishing workflows (Build, Release and Release-from-Tag) share one `buildagent-publish`
concurrency group. At most one of them publishes at a time and a run already in flight finishes
rather than being cancelled. GitHub keeps only one pending run per group, so a newer queued run
replaces an older queued one; if your queued release disappears, run it again.

## Creating a release

### Method 1: manual run

1. In the repository's **Actions** tab, select the **Release** workflow.
2. Choose **Run workflow**.
3. Set **Mark as Pre-Release** if the release is a beta or release candidate.
4. Run it.

The workflow also shows a **version** input. It is not supported yet: any value fails the run,
because the version always comes from GitVersion. Leave it empty. The release notes are always
generated from the commit history; there is no input for them.

### Method 2: pushing a tag

```bash
git tag v1.2.3
git push origin v1.2.3
```

A tag with a suffix such as `-alpha`, `-beta` or `-rc` (for example `v2.0.0-rc.1`) is marked as a
pre-release.

### A version is published once

A version that already exists is never republished. A versioned image tag or a git tag for the
version makes it taken. Do not delete a tag, a release or an image to try again.

:::note Not enforced by the workflows yet
The code that enforces this rule (`forge/Release`) and the compatibility check that compares the
public surface with the previous release (`forge/Surface`) are in the repository and tested, but
no workflow calls either one. Publishing still goes through the existing release target, which
creates or updates the GitHub release. Until they are wired in, "published once" is the rule to
follow, and no release is stopped automatically for breaking a protected surface.
:::

## Versioning

The version comes from **GitVersion** in `ContinuousDelivery` mode, configured in
`GitVersion.yml` at the repository root. Read that file for the increment rules in force; this
page does not copy them, because a copy goes stale.

Pre-release versions follow GitVersion's branch labels, for example `1.1.0-rc.1`.

## What a release contains

1. **A GitHub release** with the version tag and a changelog generated from the commit history
   since the previous release.
2. **Docker images** published to the GitHub Container Registry under the version tag and under
   `latest`. Images are built on an amd64 runner; there is no multi-architecture build.
3. **A git tag** for the version.

The release workflows run the test projects with coverage and the module tests before they
publish. They do not scan images for vulnerabilities.

## Build parameters

```bash
# Create a release
nuke --type docker --create-github-release true

# Create a pre-release
nuke --type docker --create-github-release true --pre-release true

# Dry run (no push, no tag)
nuke --type docker --create-github-release true --dry-run true
```

There is no `--version` parameter: the version is taken from GitVersion.

### Environment variables

Required to publish:

- `GITHUB_TOKEN`: GitHub authentication token
- `RegistryToken`: container registry authentication

Optional:

- `NotificationsWebHookUrl`: Discord notifications

## Troubleshooting

**Release creation fails**

- Check that `GITHUB_TOKEN` has the permissions the workflow requests.
- Check that `RegistryToken` is valid for the container registry.
- Check that the tests pass.

**The version already exists**

Do not republish it. Pick a new version: push a new commit, or a new tag.

**The changelog is empty**

- Check that there are commits since the last release.
- Check that the full git history is available (`fetch-depth: 0`).

**A release fails partway**

Do not delete the tag, the release or the images, and do not re-create them. A published version
is immutable, and no failure path deletes anything. How a partly published version is resumed is
not yet settled (contract item U-13). Until it is, open an issue describing which of the image,
the tag and the release were written, and release a new version.

Canonical contract (build command): [design/20-contract.md](https://github.com/The-Running-Dev/Docker-BuildAgent/blob/main/design/20-contract.md)
