# decision/2026-09-20-main-push-moves-latest-only
Date: 2026-09-20
Anchor: 2026-09-20 — A push to `main` moves `latest` only; versioned tags come only from releases
Status: accepted
StatedIn: unit/document/design-10-design § 2. A maintainer releases a version (CI dispatch or a version tag push)

## Claim
A push to `main` moves only the `latest` tag; versioned image tags, tool packages and module versions are written only by the release path.
