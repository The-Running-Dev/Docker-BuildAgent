# decision/2026-10-03-latest-moves-last
Date: 2026-10-03
Anchor: 2026-10-03 — `latest` moves last, after the release is published
Status: accepted
StatedIn: unit/document/design-10-design § 2. A maintainer releases a version (CI dispatch or a version tag push)

## Claim
The release publishes the versioned sinks, then the git tag, then the release, and moves `latest` only after the release is published; a failed `latest` move is fixed by re-running it.
