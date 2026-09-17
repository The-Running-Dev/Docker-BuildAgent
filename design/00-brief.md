# Brief — Docker-BuildAgent

> Written by me, not by a model. A model may interrogate it (`/brief-check`) but not author it.

## Problem

Docker-BuildAgent is a public build product used by my own repositories and available to outside users, but its compatibility boundary is not controlled end to end.

- Every published image already receives both a movable `latest` tag and a versioned tag, yet consumers and the documentation predominantly select `latest`. A consumer therefore accepts every change published from `main`, while the repository states no binding rule for immutable tags or for how breaking public-surface changes affect the version.
- The product has several competing descriptions and no single owner for each public surface. The README, product instructions, published documentation, PowerShell documents, and historical design reports overlap and already disagree with the tree. The AgentKit design, contract, and slice documents are empty, so nothing records the compatibility rules that code alone cannot state.
- Runtime configuration is fragmented across command-line arguments, environment variables, mapping files, and PowerShell configuration. There is no supported YAML or JSON project configuration and no generic sample that a user can adopt as a template.
- The unified `build <type>` wrapper is not a distributable global tool and still routes through separate build processes. The public entry point and its compatibility relationship to the underlying build types are not settled.
- A failed container update has no owned health-validation and rollback path in this product. Docker-BuildAgent will own that capability rather than leaving recovery as an implicit responsibility of a consuming repository.

## Who it is for

- Maintainers of repositories that use Docker-BuildAgent as their build environment.
- Public users who rely on the published image, command surface, configuration formats, global tool, or PowerShell module.

Public use is supported, not merely tolerated. Supported public surfaces receive the same compatibility promise as first-party consumers.

## Non-goals

- Editing or owning consuming repositories from this repository. Docker-BuildAgent owns the artifact and its compatibility contract; each consumer owns when and how it adopts a version.
- Changing the pinned `docs-template/` content from this repository.
- Multi-architecture images. The supported image architecture remains amd64 for this work.
- Adding build types beyond the current five.
- Becoming a general-purpose CI platform or a general-purpose container orchestrator. The runtime responsibility is limited to the accepted update-health and rollback capability.
- Supporting configuration formats beyond the existing surfaces plus YAML and JSON.

## Definition of done

- Published versioned image tags are immutable, `latest` is explicitly documented as movable, and public documentation shows how to select a versioned image.
- Public-surface compatibility follows semantic versioning: a breaking change requires a major version, and the protected surfaces are named in the contract.
- The `build` command, project configuration, Docker-template discovery, global tool, and PowerShell module each have one canonical contract; other documents reference those contracts rather than restating them.
- Published documentation names no path, project, command, parameter, or supported behavior that the tree lacks.
- YAML and JSON project configuration are supported under one documented precedence and validation contract, and a generic sample configuration is provided as a usable template.
- The packaged global tool exposes all five supported build types without weakening the existing compatibility promise.
- An update can preserve the prior image, validate the updated container's health, restore the prior image after a failed health check, record the rollback, and report the outcome.
- Issues #1, #11, #12, and #14 are represented by accepted slices and close only when their corresponding criteria are verified.
- The AgentKit design, contract, decisions, slices, and tracker agree on the public surface and remaining work.

## Environment

- The container and build surface are supported on Docker-capable hosts. Individual host operating systems are not separately promised merely because Docker runs on them.
- Container builds require registry and dependency-network access and may require the host Docker socket.
- The PowerShell module retains its declared PowerShell 5.1-or-later requirements.
- GitHub Actions on Linux remains a supported CI environment.
- Each invocation is isolated. Shared mutable build state and coordinated concurrent builds are outside this brief.
- The supported image architecture for this work is amd64.

## Lifespan

Docker-BuildAgent is maintained for years. Compatibility, migration, deprecation, release discipline, and recovery behavior are durable obligations rather than best-effort conventions.
