# decision/2026-10-06-release-entry-point
Date: 2026-10-06
Anchor: 2026-10-06 — Releases run through `forge/Publish`; a tag on the run's own commit is the release it makes, and a pre-release publishes everywhere but never moves `latest`
Status: accepted
StatedIn: unit/document/design-10-design § Control flow 2

## Claim
Both release workflows publish through `forge/Publish`; a version tag at the run's own commit with no claim and no sink holding the version is the release being made; a pre-release label is one alphanumeric identifier, publishes to every sink and as a GitHub pre-release, never moves `latest` and is never a baseline; `BaselineMissing` is accepted for 2.0.0 and its pre-releases alone.
