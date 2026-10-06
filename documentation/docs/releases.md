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
| Manual run | `release.yml` (**Release**) | For the highest stable release | Yes |
| Push of a `v*` tag | `release-tag.yml` (**Release-from-Tag**) | For the highest stable release | Yes |

A push to `main` writes no version: it moves `latest` and nothing else. A versioned image tag,
a `BuildAgent.Tool` package, a `Docker-BuildAgent` module version, a GitHub release and a version
tag are written only by a release. A pre-release never moves `latest`.

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
- **Does**: runs the module tests and the test projects, then runs the release entry point
  `forge/Publish` through the `publish-release` action (`.github/actions/publish-release`). It
  checks the version, compares the public surface with the previous release, composes the notes,
  builds every artifact, claims the version and publishes.
- **Publishes**: the versioned image on ghcr.io, the global tool `BuildAgent.Tool` on nuget.org,
  the module `Docker-BuildAgent` on the PowerShell Gallery, the GitHub release with its
  `surface-manifest.json` asset and the version tag, and, for the highest stable release,
  `latest`.
- **Needs**: the secrets `REGISTRY_TOKEN`, `NUGET_API_KEY` and `PSGALLERY_API_KEY`, in the
  `release` environment the job runs in or in the repository.

The three publishing workflows (Build, Release and Release-from-Tag) share one `buildagent-publish`
concurrency group. At most one of them publishes at a time and a run already in flight finishes
rather than being cancelled. GitHub keeps only one pending run per group, so a newer queued run
replaces an older queued one; if your queued release disappears, run it again.

## Creating a release

### Method 1: manual run

1. In the repository's **Actions** tab, select the **Release** workflow.
2. Choose **Run workflow**.
3. For a pre-release, give a **pre-release label** of letters and digits, such as `rc1`. Leave it
   empty for a stable release.
4. Run it.

The core version (`2.1.0`) always comes from GitVersion; the label makes it `2.1.0-rc1`. The
workflow also shows a **version** input. It is not supported: any value fails the run. Leave it
empty, or push a tag to choose the version yourself.

### Method 2: pushing a tag

```bash
git tag v2.1.0
git push origin v2.1.0
```

The tag names the version. A tag with a label, such as `v2.1.0-rc1`, is a pre-release. The label
is one identifier of letters and digits: `v2.1.0-rc.1` and `v2.1.0-rc-1` are refused, because the
PowerShell Gallery cannot hold them.

### Pre-releases

A pre-release is published to every sink like a stable release: the image tag `2.1.0-rc1`, the
tool and the module at `2.1.0-rc1`, and a GitHub release marked as a pre-release. It never moves
`latest`, and it is never the baseline the next release is compared with.

### Release notes

The notes come from the page `documentation/docs/release-notes/<version>.md` when it exists (the
pre-release `2.1.0-rc1` uses `2.1.0.md`). The page's front matter, its title and its "Not yet
released" notice are dropped, and its relative links are made absolute at the released commit.
Without a page, the notes list the subjects of the commits since the previous release tag.

Either way, the breaking changes and deprecations the surface comparison detects are added to the
**Breaking Changes** and **Deprecations** sections, and an empty section says "None.". A page that
lacks either heading fails the release before anything is published.

### A version is published once

A version that already exists is never republished. A claim, a versioned image tag or a git tag
for the version makes it taken. The one exception is the tag you pushed: a tag on the commit being
released, with no claim and no published artifact for its version, is the release being made.
Do not delete a tag, a release or an image to try again.

A release is also refused when its major is below the highest published major, or when its
public surface breaks the previous stable release without a major increase. 2.0.0, the first
release that records its surface, has nothing to compare with and is judged by the
[migration guide](./migration.md).

## Versioning

The version comes from **GitVersion** in `ContinuousDelivery` mode, configured in
`GitVersion.yml` at the repository root. Read that file for the increment rules in force; this
page does not copy them, because a copy goes stale.

A release uses GitVersion's major, minor and patch only; a pre-release label comes from the
**Release** input or the pushed tag, never from GitVersion's branch labels.

## What a release contains

1. **The image** on the GitHub Container Registry under the version tag, and under `latest` for
   the highest stable release. It is built on an amd64 runner; there is no multi-architecture
   build.
2. **The global tool** `BuildAgent.Tool` on nuget.org, and **the module** `Docker-BuildAgent` on
   the PowerShell Gallery, both at the version.
3. **A GitHub release** with the notes and the `surface-manifest.json` asset, and **a git tag**
   for the version.

Publishing runs in that order, and the GitHub release is published only after every artifact is
written. The release workflows run the test projects with coverage and the module tests before
they publish. They do not scan images for vulnerabilities.

The entry point refuses to run anywhere but CI, so there is no local release command.

## Troubleshooting

**Release creation fails**

The error names its code, such as `VersionAlreadyExists`, `SurfaceGateFailed` or
`SinkPublishFailed [GlobalTool]`.

- Check that `REGISTRY_TOKEN` can push to ghcr.io, and that `NUGET_API_KEY` and
  `PSGALLERY_API_KEY` are set and valid.
- Check that the tests pass.

**The version already exists**

Do not republish it. Pick a new version: push a new commit, or a new tag.

**The notes list no changes**

- Write a release-notes page for the version, or check that there are commits since the last
  release tag.
- Check that the full git history is available (`fetch-depth: 0`).

**A release fails partway**

Do not delete the tag, the release or the images, and do not re-create them. Re-run the failed
workflow run for the same commit. The run finds its own claim and resumes: it skips every
artifact that is already published with the identity the claim recorded, and publishes the rest.
A run that finds the release already published moves only `latest`. An artifact that holds the
version with a different identity stops the run with `SinkArtifactMismatch`; that needs a person.

Canonical contract (build command): [design/20-contract.md](https://github.com/The-Running-Dev/Docker-BuildAgent/blob/main/design/20-contract.md)
