# decision/2026-11-07-update-reproduces-runner-settings
Date: 2026-11-07
Anchor: 2026-11-07 — An update reproduces the settings a self-hosted runner and its Docker-in-Docker sidecar are started with
Status: accepted
StatedIn: unit/document/design-20-contract § Container update

## Claim
The replacement carries over privileged mode, the memory limit with its reservation and swap limit, the CPU limit, tmpfs mounts, the stop timeout and network aliases; every other unreproduced setting still refuses by name, and restoring returns the container, not the state its mounts hold.
