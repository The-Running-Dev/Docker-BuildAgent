# Brief — Docker-BuildAgent

> Written by me, not by a model. A model may interrogate it (`/brief-check`) but not author it.

## Problem

Docker-BuildAgent is a public build product used by my own repositories and available to outside users, but its compatibility boundary is not controlled end to end.

- Every published image already receives both a movable `latest` tag and a versioned tag, yet consumers and the documentation predominantly select `latest`. A consumer therefore accepts every change published from `main`, while the repository states no binding rule for immutable tags or for how breaking public-surface changes affect the version.
- The product has several competing descriptions and no single owner for each public surface. The README, product instructions, published documentation, PowerShell documents, and historical design reports overlap and already disagree with the tree. The AgentKit design, contract, and slice documents are empty, so nothing records the compatibility rules that code alone cannot state.
- Runtime configuration is fragmented across command-line arguments, environment variables, mapping files, and PowerShell configuration. There is no supported YAML or JSON project configuration and no generic sample that a user can adopt as a template.
- The unified `build <type>` wrapper is not a distributable global tool. The public entry point and its compatibility relationship to the underlying build types are not settled. How the entry point reaches each build type internally is not part of the problem.
- A failed container update has no owned health-validation and rollback path in this product. Docker-BuildAgent will own that capability rather than leaving recovery as an implicit responsibility of a consuming repository.

## Who it is for

- Maintainers of repositories that use Docker-BuildAgent as their build environment.
- Public users who rely on the published image, command surface, configuration formats, global tool, or PowerShell module.

Public use is supported, not merely tolerated. Supported public surfaces receive the same compatibility promise as first-party consumers. "Supported" means that compatibility promise; it is not a response-time or fix-window commitment.

## Compatibility promise

- The compatibility rules bind from v2.0.0. Releases before it are not judged against them.
- A breaking change is a removed or renamed command, parameter, or configuration key, or a changed default or documented behaviour, on a protected surface. Image contents are protected only where the contract names them; bundled tool versions are not protected unless named.
- The protected surfaces are the image, the `build` command, the global tool, project configuration, Docker-template discovery, and the PowerShell module. For the image, the contract protects at least how it is invoked: its entrypoint, the build types it accepts, and the mounts and environment inputs it reads.
- The image, global tool, and PowerShell module share one version and are released together.
- Fixes ship only in the current major version. Earlier versioned images remain pullable but are not patched.
- A surface is deprecated in a minor release, warns when used, and is removed no earlier than the next major version.
- A versioned image tag published from v2.0.0 onward never changes content and is never deleted or expired.

## Non-goals

- Editing or owning consuming repositories from this repository. Docker-BuildAgent owns the artifact and its compatibility contract; each consumer owns when and how it adopts a version.
- Changing the pinned `docs-template/` content from this repository.
- Multi-architecture images. The supported image architecture remains amd64 for this work.
- Windows-based container images.
- Adding build types beyond the current five.
- Becoming a general-purpose CI platform or a general-purpose container orchestrator. The runtime responsibility is limited to the accepted update-health and rollback capability.
- Background watching of containers, or health monitoring and alerting beyond the update being performed.
- Rolling back anything other than the image and its container: volumes, data, host configuration, and consumer state are not restored.
- Supporting configuration formats beyond the existing surfaces plus YAML and JSON.
- Removing or deprecating any existing configuration surface in this work.
- Removing `latest` or discouraging its use.
- Auditing, rewriting, or re-tagging image tags published before v2.0.0.
- Container runtimes other than Docker.
- Publishing the image to registries other than the one it is published to today.
- Image signing, provenance attestation, or a software bill of materials in this work.
- Replacing the existing build system or PowerShell integration wholesale.
- A support service level for public users.

## Definition of done

- Only CI publishes releases. Publishing a version that already exists fails, including through the manual version input. `latest` is explicitly documented as movable, and public documentation shows how to select a versioned image.
- Public-surface compatibility follows the rules in *Compatibility promise*, and the protected surfaces are named in the contract. Where a surface can be compared mechanically between releases, a removal or rename without a major version fails the release.
- One published page states the compatibility promise for public users — protected surfaces, versioning, which versions receive fixes, deprecation, supported hosts and CI, and the Docker socket trust warning — and names the contract as canonical.
- Every release's notes carry a breaking-changes section and a deprecations section, even when empty; a release cannot publish without them.
- The deprecation policy is published, and a migration guide from 1.x to 2.0.0 covers every breaking change in 2.0.0 and moving from `latest` to a pinned version.
- The `build` command, project configuration, Docker-template discovery, global tool, and PowerShell module each have one canonical contract. `PSModule.requirements.md` remains the PowerShell module's. Other documents reference those contracts; published documentation may explain in its own words but names the canonical contract it explains.
- Every document that overlaps a public surface is classified as a canonical contract, a reference naming its canonical source, or removed. History remains in git, not in the published site.
- The published documentation — the documentation site, the README, and PowerShell help — names no path, project, command, parameter, or supported behavior that the tree lacks, and this is checked on every change for what can be checked mechanically.
- YAML and JSON project configuration are supported under one documented precedence across every configuration source — arguments over environment variables over the project file over defaults, with mapping files and PowerShell configuration placed explicitly — and one validation contract. A project containing both a YAML and a JSON configuration is an error, unknown keys are an error, and every configuration file declares its schema version.
- A sample configuration for each of the five build types is provided in both YAML and JSON, and each passes validation unedited.
- Automated tests show configuration validation rejecting an unknown key, both formats present, a missing or unsupported schema version, and a malformed file, and show every sample passing; the counts of each are stated.
- The packaged global tool exposes all five supported build types as a protected surface under the compatibility rules. Changes to existing entry points in 2.0.0 are recorded in its release notes.
- The global tool is installable from the public .NET tool feed and the PowerShell module from the PowerShell Gallery, both published by CI.
- A consumer can update a named long-running container with a command this product supplies. The update preserves the prior image, validates the updated container against its own declared health check within a configurable timeout, and restores the prior image when that check does not pass in time or the container declares none. Automatic restore is the default and can be disabled. Each update writes a log entry that survives restarts, and the command's exit status reports the outcome; notification is optional.
- An update refuses before changing anything when another update of the same container is in progress, or when the prior image cannot be kept.
- Automated tests on the supported hosts exercise a healthy update, an unhealthy update that restores the prior image, and each refusal.
- Secrets supplied by a consumer are never written to logs or left in images the product builds.
- The PowerShell module is tested on both Windows PowerShell 5.1 and PowerShell 7.
- Issues #1, #11, #12, and #14 are represented by accepted slices, their acceptance text is rewritten from those slices, and they close only when their corresponding criteria are verified.
- The AgentKit design, contract, decisions, slices, and tracker agree on the public surface and remaining work.

## Environment

- Supported hosts are Linux running Docker Engine and Windows running Docker Desktop with Linux containers. macOS hosts are best-effort. Individual host operating systems are not otherwise promised merely because Docker runs on them.
- The global tool runs on the host and is supported on Linux and Windows.
- Container builds require registry and dependency-network access and may require the host Docker socket. Standard proxy settings are honoured for every download a build makes; internal mirrors and offline builds are not supported.
- Mounting the host Docker socket assumes a trusted host and trusted code being built. Host compromise through the socket is not a product defect, and the documentation says so.
- Public users pull the image anonymously, subject to the registry's limits. Refusing to republish an existing version does not depend on the registry enforcing it.
- The PowerShell module retains its declared PowerShell 5.1-or-later requirements.
- GitHub Actions on Linux and Windows, hosted and self-hosted, are supported CI environments. Other CI systems are best-effort.
- Each build invocation is isolated. Shared mutable build state and coordinated concurrent builds are outside this brief. A container update is the one exception: it changes shared host state, and only one update of a given container runs at a time.
- The supported image architecture for this work is amd64.
- Image size carries no budget, and a change in size is not a compatibility break.

## Lifespan

Docker-BuildAgent is maintained for years. Compatibility, migration, deprecation, release discipline, and recovery behavior are durable obligations rather than best-effort conventions.
