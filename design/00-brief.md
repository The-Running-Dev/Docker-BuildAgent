# Brief — Docker-BuildAgent

> Written by me, not by a model. A model may interrogate it (`/brief-check`) but not author it.

## Problem
Docker-BuildAgent exposes one versioned container and a five-route `build <type>` interface,
but the executable routing, published documentation, solution topology, CI coverage, failure
semantics, and operational evidence do not agree. A build can report successful Docker
publication after a registry operation fails, controlled failure paths can retain a generated
secret-bearing environment file, and logs can disclose command arguments or webhook material.
Maintainers and consuming repositories therefore cannot treat a successful run as sufficient
evidence that the intended build or release completed safely.

## Who it is for
Docker-BuildAgent maintainers and engineers integrating consuming repositories. There is no
fixed user count. Users are expected to understand CI, containers, and repository-level build
configuration; they should not need to understand Forge internals to rely on the public build
contract.

## Non-goals
The binding list. Everything here is out of scope for every agent, permanently, until this
file changes.

- Adding build types or expanding release automation beyond the existing five-route boundary.
- Turning the image into a general-purpose application runtime, hosted build API, scheduler,
  persistent queue, cache, or job database.
- Providing isolation for arbitrary untrusted code executed inside the agent.
- Providing exactly-once external side effects or automatic rollback.

## Definition of done
Checkable statements, not aspirations.

- The public router, solution inventory, PowerShell surface, generated contract, and published
  documentation agree on the five supported build routes and their effective execution paths;
  CI fails when that agreement drifts.
- Docker login, tag, push, and other required publication failures propagate to a nonzero build
  result and no success message is emitted for an operation that did not complete.
- Captured CI evidence demonstrates that command logging, notifications, and generated
  environment handling do not disclose credentials, and transient secret-bearing files are
  removed on every controlled completion path.
- CI executes every repository test project and an integration dry-run covering the public
  container entrypoint.
- A manual or tag-triggered official release, artifact verification, and rollback to a prior
  immutable image tag have been exercised and recorded.

## Environment
Each invocation is an ephemeral, containerized run for one mounted, trusted consumer repository.
The system keeps no internal job queue or durable run database. Independent CI runs may execute
concurrently, but an invocation owns only its workspace and in-memory state. It requires the
repository's build inputs plus the toolchain packaged in the image, and may require network
access to package registries, a Docker daemon and registry, GitHub, and optional Discord
notifications. Repository files and build artifacts are the relevant data scale.

## Lifespan
Maintained for years.
