# decision/2026-09-20-docker-objects-named-by-hash
Date: 2026-09-20
Anchor: 2026-09-20 — Product-owned Docker objects are named by hash and attributed by label
Status: accepted
StatedIn: unit/document/design-20-contract § Prior image pin and prior container

## Claim
The lock, prior container and prior image pin are named `buildagent-lock-<h>`, `buildagent-prior-<h>` and `buildagent-prior:<h>`, with `<h>` the first 32 hex characters of the SHA-256 of the target name.
