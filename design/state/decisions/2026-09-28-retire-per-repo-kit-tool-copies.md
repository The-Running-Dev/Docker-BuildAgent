# decision/2026-09-28-retire-per-repo-kit-tool-copies
Date: 2026-09-28
Anchor: 2026-09-28 — Retire the per-repo `tools/` kit copies; the machine-wide kit's tools are authoritative
Status: accepted
StatedIn: unit/document/design-20-contract § Artifacts of a unit kind

## Claim
This repository holds no copy of the AgentKit tools: the design-state check, the projector and the session hooks run from the machine-wide kit checkout, and the contract's `script` and `command` rows resolve to no files.
