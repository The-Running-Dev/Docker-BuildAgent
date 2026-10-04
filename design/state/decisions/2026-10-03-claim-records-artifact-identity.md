# decision/2026-10-03-claim-records-artifact-identity
Date: 2026-10-03
Anchor: 2026-10-03 — The release claim records each artifact's identity, and a resume skips a sink only on a match
Status: accepted
StatedIn: unit/document/design-10-design § Release claim

## Claim
The claim records the image digest and the SHA-256 of both packages, and a resume skips a sink holding the version only when its identity matches; a mismatch fails as `SinkArtifactMismatch` with nothing written.
