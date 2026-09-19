# Agent contract — this repository

This file holds the project rules for this repository. It is binding for every agent session in this repository, regardless of tool or model.

## Shared contract

**Read [`AGENTS.shared.md`](C:/Users/Ben/.agent-kit/AGENTS.shared.md) completely before this file.** It holds the rules every repository using the kit shares, and they bind here as fully as anything written below. This file adds only what is specific to this repository; where the two conflict, the more specific instruction wins (`AGENTS.shared.md`, *Safe start*).

## Repository identity

Docker-BuildAgent owns the build-agent container image, the Forge/NUKE build system,
the public command and configuration surfaces, the PowerShell integration, the scoped
container update health/rollback capability, and the documentation sources used to
build and publish them. It does not own consuming repositories or the `docs-template/`
checkout, which is a pinned Docusaurus-Template submodule.

Product architecture, build commands, and development workflow remain canonical in
`.github/copilot-instructions.md`; read it completely before changing product code.
`PSModule.requirements.md` is the canonical contract for the PowerShell module surface.
The root `design/` chain records project-wide decisions that those product documents
cannot; it does not replace the published documentation or the module specification.

## Git and delivery

- **Never force-push or rewrite published history.** If a pushed commit needs changing, add a follow-up commit.
