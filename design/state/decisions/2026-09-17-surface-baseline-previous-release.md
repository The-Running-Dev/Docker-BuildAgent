# decision/2026-09-17-surface-baseline-previous-release
Date: 2026-09-17
Anchor: 2026-09-17 — Surface baseline is the manifest attached to the previous published release
Status: accepted
StatedIn: unit/document/design-10-design § 2. A maintainer releases a version (CI dispatch or a version tag push)

## Claim
A release compares its surface manifest against the one attached to the highest published release below it, and removal requires deprecation in that baseline.
