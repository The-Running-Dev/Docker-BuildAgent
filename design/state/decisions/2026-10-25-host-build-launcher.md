# decision/2026-10-25-host-build-launcher
Date: 2026-10-25
Anchor: 2026-10-25 — `build-agent build` forwards the build's environment by name, takes `BUILD_AGENT_IMAGE` as its only override, and the repository root is a GitHub Action over it
Status: accepted
StatedIn: unit/document/design-20-contract § Global tool

## Claim
`build-agent build` forwards by name only the set, non-empty host variables among the published parameter environment names, the names the workspace's env map files read, and `CI`, `GH_TOKEN` and non-path `GITHUB_*`; `BUILD_AGENT_IMAGE` is its only image override, and without it a tool with no release version refuses with 5. A `docker run` status of 125 passes through unchanged with a mount diagnostic. The repository root `action.yml` installs `BuildAgent.Tool` at the tag it is used at, or its `version` input, and runs `build-agent build`.
