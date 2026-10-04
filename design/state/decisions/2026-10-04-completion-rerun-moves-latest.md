# decision/2026-10-04-completion-rerun-moves-latest
Date: 2026-10-04
Anchor: 2026-10-04 — A re-run for a published release's own commit moves only `latest`
Status: accepted
StatedIn: unit/document/design-10-design § 2. A maintainer releases a version (CI dispatch or a version tag push)

## Claim
A release run that finds its version published for its own commit, with every versioned sink matching and the version the highest published, moves `latest` alone and writes nothing else.
